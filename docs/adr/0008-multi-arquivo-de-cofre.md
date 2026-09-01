# 0008 - Múltiplos arquivos de cofre (multi-vault) com arquivos locais

## Status
Aceito — 2026-09-01. Refinado para **Opção B (multi-arquivo SQLite)** conforme decisão do produto nesta data. Em implementação.

## Contexto
Até a Fase B o app suporta **um único cofre** por instalação: um único registro `VaultStore` (`VaultRepository.cs:24 SingletonRecordId`) em um único arquivo `LocalAppData\PasswordManager\vault.db` (`App.xaml.cs:354`). `IVaultSessionService.ExistsAsync()` retorna `bool` e `UnlockViewModel` decide entre criar/desbloquear com base nisso. Não há lista de cofres, troca, criação de segundo cofre ou exclusão.

A nova feature exige que o usuário, **antes do desbloqueio**, veja os arquivos de save locais, crie um novo arquivo, renomeie/exclua e alterne entre eles — cada cofre com **senha mestra e salt próprios**, criptografia idêntica (ADR 0004) e blob único (ADR 0003) por arquivo.

Duas opções foram avaliadas:

* **A) Multi-linha no mesmo `vault.db`** — 1 DB, N linhas em `VaultStore` com coluna `Nome`. Menor churn, mas cofres ficam invisíveis no Explorer e backup exige copiar DB inteiro.
* **B) Multi-arquivo `Vaults/*.db`** — 1 arquivo SQLite por cofre em `LocalAppData\PasswordManager\Vaults\`, catálogo (`vaults.json`) com metadados não sensíveis (id, nome, nome do arquivo, atualizadoEm). Cofre = arquivo real no filesystem.

## Decisão
Optou-se por **B — Multi-arquivo**. Motivos:

1. Corresponde ao modelo mental pedido ("arquivos salvos local" visíveis/tocáveis).
2. Isola corrupção (um arquivo corrompido não afeta os demais) e permite backup granular (copiar um `.db`).
3. Reusa o mesmo contrato de criptografia/persistência por arquivo — ADR 0003/0004 continuam válidos por arquivo.
4. Caminho evolutivo limpo: fábrica de `VaultDbContext` por arquivo + registry JSON.

### Princípios confirmados pelo produto
* Nome padrão sequencial `vault-1`, `vault-2`, … (gerado automaticamente), com opção de **renomear livremente** pelo usuário.
* **Renomear e excluir sem precisar desbloquear** (metadados do registry não são criptografados).
* **Configurações globais** — `settings.json` continua único e global (`AppSettings` não é per-cofre).

## Arquitetura

### Armazenamento
```
%LocalAppData%\PasswordManager\
  settings.json                 # global (tema, idioma, timeouts, gerador)
  vaults.json                   # registry: { vaults: [{id, nome, arquivo, criadoEm, atualizadoEm}], ativoId }
  Vaults\
    vault-1.db                  # SQLite por cofre (1 linha VaultStore por arquivo)
    vault-2.db
    meu-cofre-pessoal.db        # após renomear, arquivo pode ser renomeado para slug do nome
```
* Cada `*.db` continua com schema `VaultStore(Id, SchemaVersion, Salt, EncryptedBlob, UpdatedAt)` + migration `InitialCreate`. O `SingletonRecordId` deixa de ser singleton — cada arquivo tem seu próprio `Id` (um GUID fixo por arquivo, diferente entre arquivos).
* `vaults.json` é a fonte de verdade do catálogo; `UpdatedAt` no registry espelha `VaultRecord.UpdatedAt` para ordenação/exibição sem descriptografar.
* Nomes são **únicos case-insensitive**; slug do arquivo (`vault-1.db` ou `slug-do-nome.db`) é derivado do nome com sanitização + sufixo numérico em colisão. Validação: 1–64 caracteres, sem `\ / : * ? " < > |` e sem nomes reservados do Windows (`CON`, `PRN`, etc.).

### Camadas

* **Application — novos contratos:**
  ```csharp
  record VaultDescriptor(Guid Id, string Nome, string Arquivo, DateTime CriadoEm, DateTime AtualizadoEm);
  interface IVaultRegistry {
    Task<IReadOnlyList<VaultDescriptor>> ListarAsync(Ct);
    Task<VaultDescriptor> CriarAsync(string nome, string senhaMestra, Ct); // gera arquivo + registro no registry + vault vazio criptografado
    Task RenomearAsync(Guid id, string novoNome, Ct); // rename lógico + rename de arquivo com lock
    Task ExcluirAsync(Guid id, Ct);   // delete arquivo + remove do registry; se id==ativo, tranca sessão
    Task DefinirAtivoAsync(Guid id, Ct);
    Guid? AtivoId { get; }
    VaultDescriptor? Ativo { get; }
    string ObterCaminhoDoBanco(Guid id);
  }
  interface IVaultDbContextFactory { VaultDbContext Create(string caminhoDb); }
  ```
  `IVaultRepository` deixa de ser singleton: métodos passam a receber `caminho`/`id` via factory ou via `IVaultRegistry.Ativo`. Mantidos overloads legados como adapter durante migração.

* **Infrastructure:**
  * `VaultRegistry` (JSON em `vaults.json`, com lock de arquivo + tolerância a corrupção: fallback para lista vazia).
  * `VaultDbContextFactory` + `VaultRepository` por arquivo.
  * `VaultDatabaseMigrator` continua aplicável por arquivo (`ApplyMigrations(context)` por `*.db`).

* **Presentation/UI:**
  * `UnlockViewModel` passa a expor `Cofres`, `CofreSelecionado`, comandos `CriarNovoArquivo`, `RenomearArquivo`, `ExcluirArquivo`, `SelecionarArquivo`. `InitializeAsync` carrega `ListarAsync()`.
  * `UnlockPage` exibe lista de saves (esquerda) + formulário de senha (direita). Trocar seleção = `Lock()` + `DefinirAtivoAsync`.
  * `VaultPage` ganha acesso a renomear/excluir via menu; exclusão do ativo volta para `UnlockPage`.

### Migração de dados legados
Na primeira execução após a feature:
1. Se existe `vault.db` legado e não existe `Vaults/` nem `vaults.json`: criar `Vaults/`, mover `vault.db` → `Vaults/vault-1.db`, criar entrada `{id: Guid do registro legado ou novo Guid, nome: "vault-1", arquivo: "vault-1.db"}` e `ativoId = id`. Se `vault.db` não existe, criar registry vazio.
2. Baseline de `__EFMigrationsHistory` por arquivo já tratado em `VaultDatabaseMigrator.RegisterBaselineDeBancoLegado` — aplica por arquivo.

### Segurança
* Cada `*.db` tem seu próprio `Salt` (16 bytes) e blob AES-256-GCM independente. Troca de senha mestra afeta só o arquivo ativo.
* `vaults.json` e nomes de arquivo **não são criptografados** (não contêm segredos). Senhas/chaves nunca vão para o registry.
* `Lock()` zera chave (`CryptographicOperations.ZeroMemory`) ao trocar de cofre.

### Fora do escopo nesta ADR
* Import CSV (continua adiado, ADR 0005).
* Criptografia do registry ou proteção do nome do cofre.
* Sincronização/backup automático entre cofres.

## Consequências

**Positivas:**
* Atende diretamente ao requisito de múltiplos saves visíveis no filesystem.
* Falhas isoladas por arquivo; backup/restauração granular.
* Mesmas garantias de ADR 0003/0004 por arquivo, sem mudar parâmetros cripto.

**Negativas / pontos de atenção:**
* `VaultDbContext` deixa de ser singleton — precisa de factory e descarte (`Dispose`) ao trocar de cofre; risco de file-lock no SQLite se não descartado.
* `vaults.json` precisa de escrita atômica (escrever temporário + `Move`).
* Validação de nome/slug e colisão de arquivo exigem testes dedicados.
* CI precisa cobrir multi-arquivo (`VaultRegistryTests`, `VaultRepository` por caminho).

## Alternativas consideradas
* **A) Multi-linha no mesmo `vault.db`** — rejeitada como solução final por esconder arquivos do usuário e acoplar falha de um cofre ao DB único, mas registrada como fallback incremental se B bloquear.
* **C) Um `.vault` por cofre como persistência primária** — rejeitada: `.vault` tem magic+versão+salt+pacote, mas não tem `SchemaVersion`/`UpdatedAt` do EF e perderia migrations.

## Referências
* ADR 0003 (blob único), ADR 0004 (Argon2id/AES-GCM), ADR 0005 (.vault)
* `src/PasswordManager.Infrastructure/Persistence/VaultRepository.cs:24`
* `src/PasswordManager.UI/App.xaml.cs:339 ConfigureServices`
* `src/PasswordManager.Presentation/ViewModels/UnlockViewModel.cs:64`
