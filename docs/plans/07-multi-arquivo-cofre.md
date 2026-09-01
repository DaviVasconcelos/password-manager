# Plano — Fase C: Múltiplos arquivos de cofre (Opção B, ADR 0008)

> **Roadmap:** `AGENTS.md` Fase C item 7 — **EM IMPLEMENTAÇÃO**. **ADR:** `docs/adr/0008-multi-arquivo-de-cofre.md`. **Decisão:** Opção B (1 arquivo SQLite por cofre em `Vaults/*.db` + `vaults.json`). Nomes padrão `vault-1`, `vault-2`, ... com renomeação livre; renomear/excluir sem desbloqueio; `settings.json` global.
> **Objetivo:** permitir que o usuário veja, crie, renomeie, exclua e alterne entre cofres **antes** do desbloqueio, cada um com senha/salt/blob independentes (ADR 0003/0004 por arquivo), com migração automática de `vault.db` legado.

## 0. Contexto e problema atual

| Área | Situação atual | Arquivo |
|---|---|---|
| **Persistência** | `VaultRepository` usa `SingletonRecordId` fixo (`26d49760-...`) e `SingleOrDefault(r => Id == Singleton)` em todos os métodos | `src/PasswordManager.Infrastructure/Persistence/VaultRepository.cs:24` |
| **Banco** | `VaultDbContext` registrado como `Singleton` com caminho fixo `LocalAppData\PasswordManager\vault.db` | `src/PasswordManager.UI/App.xaml.cs:352` |
| **Sessão** | `IVaultSessionService` expõe `ExistsAsync(): bool`, `CreateAsync`/`UnlockAsync` sem conceito de cofre ativo; `_chave`+`_vault` únicos | `src/PasswordManager.Application/VaultSession/IVaultSessionService.cs:26`, `VaultSessionService.cs:21` |
| **Unlock** | `UnlockViewModel.InitializeAsync` decide `ModoCriar = !ExistsAsync()` — não há lista | `src/PasswordManager.Presentation/ViewModels/UnlockViewModel.cs:64` |
| **Settings** | `AppSettings`/`settings.json` global, sem `ativoId` | `src/PasswordManager.Application/Settings/AppSettings.cs:9` |
| **Migrator** | `VaultDatabaseMigrator.ApplyMigrations` aplica baseline por conexão única | `src/PasswordManager.Infrastructure/Persistence/VaultDatabaseMigrator.cs:22` |

Consequência: produto só suporta 1 save por instalação. A feature exige catálogo + fábrica de `DbContext` por arquivo + UI de seleção antes do unlock.

## 1. Princípios

- Comentários, exceções e nomes de testes em **pt-BR** (`Metodo_Cenario_ResultadoEsperado`), `AGENTS.md:Conventions`.
- Cada etapa é um **PR/commit isolado** e compila/verifica (`dotnet build -p:Platform=x64`, `dotnet test -p:Platform=x64`).
- `Domain` não muda (`Vault` continua agregado raiz, ADR 0001). Mudanças concentradas em `Application/Infrastructure/Presentation/UI`.
- Metadados do catálogo (`vaults.json`: id, nome, arquivo, criadoEm, atualizadoEm, ativoId) **não são criptografados** (não contêm segredos); por isso renomear/excluir não exige desbloqueio.
- `settings.json` permanece **global** (tema, idioma, timeouts, gerador) — Fase C não cria settings por cofre.
- Nomes de cofre: validação centralizada, `vault-1`..`vault-N` como padrão, colisão tratada com sufixo; slug de arquivo sanitizado (sem `\ / : * ? " < > |`, sem nomes reservados Windows, 1–64 chars, trim, case-insensitive único).

## 2. Arquitetura alvo (resumo da ADR 0008)

```
%LocalAppData%\PasswordManager\
  settings.json              # global
  vaults.json                # { vaults: [{id, nome, arquivo, criadoEm, atualizadoEm}], ativoId }
  Vaults\
    vault-1.db
    vault-2.db
    meu-cofre.db             # após renomear, arquivo pode ser renomeado para slug
```
Cada `*.db` tem schema `VaultStore(Id, SchemaVersion, Salt, EncryptedBlob, UpdatedAt)` (InitialCreate) — 1 linha por arquivo. Migração legada: `vault.db` → `Vaults/vault-1.db` + entrada no registry.

Contratos novos (Application):

```csharp
record VaultDescriptor(Guid Id, string Nome, string Arquivo, DateTime CriadoEm, DateTime AtualizadoEm);
interface IVaultRegistry { IReadOnlyList<VaultDescriptor> Listar(); Task<VaultDescriptor> CriarAsync(string nome, string senha); Task RenomearAsync(Guid id, string novoNome); Task ExcluirAsync(Guid id); Task DefinirAtivoAsync(Guid id); Guid? AtivoId { get; }; string ObterCaminho(Guid id); }
interface IVaultDbContextFactory { VaultDbContext Create(string caminhoDb); }
```

`IVaultRepository` passa a operar por caminho (factory injeta `VaultDbContext` por arquivo); overloads legados mantidos como adapter delegando para registry.Ativo durante transição.

Fluxo troca: `Lock()` (`ZeroMemory`) → `DefinirAtivoAsync` → próximo `UnlockAsync` usa salt do arquivo ativo.

## 3. Etapas (cada uma é um PR/commit isolado para agente de IA)

### Etapa 7.1 — Contratos Application (sem comportamento)

**Criar:**
- `src/PasswordManager.Application/VaultRegistry/VaultDescriptor.cs` (record).
- `src/PasswordManager.Application/VaultRegistry/IVaultRegistry.cs` (métodos acima + `Task InicializarAsync()` para migração/lazy load).
- `src/PasswordManager.Application/Abstractions/IVaultDbContextFactory.cs` (`VaultDbContext Create(string caminhoDb)`).

**Alterar (mínimo):**
- `src/PasswordManager.Application/Abstractions/IVaultRepository.cs` — adicionar overloads com `string caminhoDb` ou `Guid vaultId` (manter antigos `[Obsolete]` delegando). Não quebrar callers ainda.

**Critério:** `dotnet build -p:Platform=x64` passa; nenhum teste quebrado (overloads não usados ainda).

### Etapa 7.2 — Infrastructure: registry + factory + migração legada

**Criar:**
- `src/PasswordManager.Infrastructure/Persistence/VaultDbContextFactory.cs` — `UseSqlite($"Data Source={caminho}")` + `ApplyMigrations` por arquivo.
- `src/PasswordManager.Infrastructure/VaultRegistry/FileSystemVaultRegistry.cs` — implementa `IVaultRegistry` com `vaults.json` (JSON `CamelCase`, `WriteIndented`). Escrita atômica (temp + `File.Move` overwrite). Tolerante a JSON corrompido (fallback lista vazia). Validação de nome/slug (`VaultNameValidator`: regex, 1–64, caracteres proibidos, nomes reservados `CON/PRN/AUX/NUL/COM1..9/LPT1..9`, trim, unique case-insensitive). Geração de nome padrão `vault-N` (max existente +1). `ObterCaminho(Guid)` → `Path.Combine(appData/Vaults, descriptor.Arquivo)`.
- `src/PasswordManager.Infrastructure/VaultRegistry/VaultRegistryMigrator.cs` — migra `vault.db` legado: se existe `vault.db` e `Vaults/` vazio, cria `Vaults/vault-1.db` (Move ou Copy+Delete), cria `vaults.json` com entrada `vault-1`. Se `vault.db` não existe, cria `Vaults/` + `vaults.json` vazio.

**Alterar:**
- `src/PasswordManager.Infrastructure/Persistence/VaultRepository.cs` — novo ctor `VaultRepository(Func<string,VaultDbContext> factory, ICryptoService)` ou `IVaultDbContextFactory`; métodos passam a receber caminho via registry. Manter `SingletonRecordId` como fallback por arquivo (cada arquivo tem seu próprio GUID — usar o mesmo `SingletonRecordId` por arquivo é aceitável, ou gerar Guid novo por arquivo no registry e usar como `VaultRecord.Id`).
- `src/PasswordManager.Infrastructure/Persistence/VaultDatabaseMigrator.cs` — garantir `ApplyMigrations` funciona por arquivo (já funciona; apenas garantir chamada por factory).

**Testes:**
- `tests/PasswordManager.Infrastructure.Tests/FileSystemVaultRegistryTests.cs` — ~12 testes: `CriarAsync_ComNomeVazio_DeveLancar`, `CriarAsync_ComNomeDuplicado_CaseInsensitive_DeveLancar`, `GerarNomePadrao_Sequencial_Vault1_Vault2`, `RenomearAsync_SemDesbloqueio_DeveRenomearArquivoESlug`, `ExcluirAsync_DeveRemoverArquivoERegistry`, `ListarAsync_ComJsonCorrompido_DeveRetornarListaVazia`, `DefinirAtivoAsync_ComIdInexistente_DeveLancar`, `Migrator_ComVaultDbLegado_DeveMoverParaVault1`.
- `tests/PasswordManager.Infrastructure.Tests/VaultDbContextFactoryTests.cs` — factory cria DBs isolados, `Migrate` por arquivo.

**Critério:** `VaultRepository` por caminho com round-trip criptografado isolado (2 DBs, chaves diferentes, um não abre com senha do outro).

### Etapa 7.3 — Application: VaultSessionService multi-vault

**Alterar:**
- `src/PasswordManager.Application/VaultSession/VaultSessionService.cs` — injetar `IVaultRegistry` + `IVaultDbContextFactory` (ou `IVaultRepository` já factory-aware). Guardar `Guid? _vaultAtivoId`. `CreateAsync(nome, senha)` → `registry.CriarAsync` + `repo.CreateAsync` no arquivo ativo + `DefinirSessao`. `UnlockAsync` → `registry.AtivoId` + `repo.LoadAsync(caminhoAtivo, chave)`. `SaveAsync`/`ChangeMasterPasswordAsync` operam no arquivo ativo. `Lock()` zera chave e descarta `DbContext` (Dispose). Novo método `TrocarCofreAsync(Guid id)` → `Lock()` + `DefinirAtivoAsync`.
- `src/PasswordManager.Application/VaultSession/IVaultSessionService.cs` — adicionar `Task<Vault> CreateAsync(string nome, string senha)`, `IReadOnlyList<VaultDescriptor> ListarCofresAsync()`, `Task RenomearCofreAsync(Guid, string)`, `Task ExcluirCofreAsync(Guid)`, `Task SelecionarCofreAsync(Guid)`, `VaultDescriptor? CofreAtivo`.

**Testes:**
- `tests/PasswordManager.Application.Tests/VaultSessionServiceMultiVaultTests.cs` — 8–10 testes: `CreateAsync_DeveCriarVault1_EVault2_ComChavesIsoladas`, `UnlockAsync_ComCofreAtivoDiferente_DeveUsarSaltCorreto`, `SaveAsync_DevePersistirNoArquivoAtivo`, `ChangeMasterPasswordAsync_DeveAfetarApenasArquivoAtivo`, `SelecionarCofreAsync_DeveTrancarEZerarChave`, `ExcluirCofreAsync_DoAtivo_DeveTrancar`, `RenomearCofreAsync_SemDesbloqueio_DeveRenomear`.

**Critério:** `dotnet test tests/PasswordManager.Application.Tests -p:Platform=x64` verde; `VaultSessionService` legado ainda compila (adapter).

### Etapa 7.4 — Bootstrap / DI + migração no startup

**Alterar:**
- `src/PasswordManager.UI/App.xaml.cs:339 ConfigureServices` — substituir `AddSingleton<VaultDbContext>` por `AddSingleton<IVaultDbContextFactory>`, `AddSingleton<IVaultRegistry>(sp => new FileSystemVaultRegistry(Path.Combine(appData,"vaults.json"), Path.Combine(appData,"Vaults")))`, chamar `registry.InicializarAsync()` (migração legada) antes de registrar `IVaultRepository`. Registrar `IVaultRepository` como `Scoped` ou `Transient` via factory. Garantir `Vaults/` existe (`Directory.CreateDirectory`).
- Garantir `VaultDatabaseMigrator.ApplyMigrations` chamado por arquivo no factory, não no singleton.

**Critério:** App inicia com `vault.db` legado → migra para `Vaults/vault-1.db` sem perda; app sem `vault.db` → lista vazia; `dotnet build -p:Platform=x64` verde.

### Etapa 7.5 — Presentation: UnlockViewModel com lista de cofres

**Alterar:**
- `src/PasswordManager.Presentation/ViewModels/UnlockViewModel.cs` — injetar `IVaultRegistry` (+ `IVaultSessionService` já). Expõe `ObservableCollection<VaultDescriptor> Cofres`, `VaultDescriptor? CofreSelecionado`, `string NovoNomeCofre`, `bool PodeCriarNovoArquivo`. Commands: `CriarNovoArquivoCommand` (gera `vault-N` ou usa `NovoNomeCofre`), `SelecionarCofreCommand`, `RenomearCofreCommand`, `ExcluirCofreCommand`. `InitializeAsync` → `registry.ListarAsync()` + selecionar `Ativo` ou primeiro. `CreateAsync`/`UnlockAsync` passam a usar `CofreSelecionado`. `ExcluirCofreAsync` → confirmação + `registry.ExcluirAsync` + recarregar lista; se excluiu o ativo, limpar `SenhaMestra` e notificar. Renomear valida via `VaultNameValidator` e exibe `Erro`.

**Testes:**
- `tests/PasswordManager.UI.Tests/UnlockViewModelMultiVaultTests.cs` — 12 testes: `InitializeAsync_ComDoisCofres_DeveListar`, `CriarNovoArquivo_ComNomeVazio_DeveGerarVaultN`, `SelecionarCofre_DeveTrancarSessaoAnterior`, `Renomear_SemDesbloqueio_DeveAtualizarLista`, `Excluir_Ativo_DeveTrancarELimparSelecao`, `Excluir_ComNomeDuplicado_DeveExibirErro`, `UnlockAsync_ComCofreSelecionado_DeveUsarSenhaCorreta`.

**Critério:** ViewModel 100% testável com fakes (`FakeVaultRegistry`, `FakeVaultSessionService`), sem `DispatcherQueue`/`Clipboard`.

### Etapa 7.6 — UI: UnlockPage com seletor de saves

**Alterar:**
- `src/PasswordManager.UI/Views/UnlockPage.xaml` (+ `.xaml.cs:21`) — layout 2 colunas: esquerda `ListView` dos saves (`Cofres`) com `DataTemplate` (nome + data), botão `+ Novo arquivo` (com `TextBox` para nome opcional), menu context `Renomear`/`Excluir`; direita `PasswordBox` + botões `Desbloquear`/`Criar` do cofre selecionado. Trocar seleção → `ViewModel.SelecionarCofreCommand`. Diálogos `ContentDialog` para renomear (validação ao vivo) e confirmar exclusão.
- `src/PasswordManager.UI/Strings/pt-BR/Resources.resw` + `en-US` — chaves `UnlockPage_NovoArquivo`, `UnlockPage_Renomear`, `UnlockPage_Excluir`, `UnlockPage_ConfirmarExclusao`, `UnlockPage_Erro_NomeDuplicado`, `UnlockPage_Erro_NomeInvalido`.

**Critério:** Navegação manual: criar `vault-1` → `vault-2` → renomear → excluir → desbloquear cada um com senha distinta.

### Etapa 7.7 — VaultPage: contexto do cofre ativo

**Alterar:**
- `src/PasswordManager.Presentation/ViewModels/VaultViewModel.cs` — expor `string NomeCofreAtivo` (via `IVaultRegistry.Ativo`). Commands `RenomearCofreAtivo`/`ExcluirCofreAtivo` (opcional, pode ficar só em UnlockPage; se incluir, `Excluir` → `Lock()` + `Frame.Navigate(typeof(UnlockPage))`).
- `src/PasswordManager.UI/Views/VaultPage.xaml.cs` — header mostra nome do cofre ativo; menu `...` com Renomear/Excluir.

**Critério:** Trocar de cofre em `UnlockPage` reflete no header de `VaultPage`.

### Etapa 7.8 — Testes finais, CI, docs e limpeza

**Fazer:**
- Garantir `dotnet ef migrations has-pending-model-changes` continua verde (não há nova migration de schema `VaultStore`; registry é JSON, não EF).
- Atualizar `AGENTS.md:Current state`/`Roadmap` item 7 para `IMPLEMENTADO` e contagem de testes (247 → ~270+).
- Atualizar `README.md` seção `Persistência em disco` e `Como Usar` (passo "Escolher/criar arquivo de cofre").
- Remover `using`/`SingletonRecordId` legado se não mais necessário (manter com `[Obsolete]` por 1 versão se preferir).
- `dotnet build PasswordManager.slnx -p:Platform=x64` 0 erros; `dotnet test PasswordManager.slnx -p:Platform=x64 --no-build` verde.

**DoD desta etapa (e da Fase C):**
- [ ] `vaults.json` + `Vaults/*.db` funcionando com 2+ cofres, cada um com senha própria
- [ ] `vault.db` legado migrado automaticamente para `Vaults/vault-1.db`
- [ ] Renomear/excluir sem desbloqueio, com validação e confirmação
- [ ] Trocar de cofre zera chave (`ZeroMemory`) e descarta `DbContext` (sem file-lock)
- [ ] `UnlockViewModel`/`UnlockPage` com lista de saves antes do unlock
- [ ] `settings.json` global (sem regressão de tema/idioma/auto-lock)
- [ ] ≥ 20 testes novos (registry 12 + session 8 + UnlockViewModel 12 + VaultRepository por arquivo 4)
- [ ] Docs atualizados (`AGENTS.md`, `README.md`, ADR 0008 marcado como implementado)

## 4. Dependências e limitações que podem atrapalhar

| # | Dependência / Limitação | Impacto | Mitigação |
|---|---|---|---|
| 1 | **`VaultDbContext` singleton** (`App.xaml.cs:352`) — hoje 1 instância para todo o app | Etapa 7.4 exige quebrar singleton → factory + lifecycle por arquivo. Se não descartar `DbContext` ao trocar, SQLite mantém file-lock e `ExcluirAsync` falha com `SQLite busy`. | `IVaultDbContextFactory` com `Create` retornando `new VaultDbContext(options)` por operação; `VaultRepository` cria/descarta contexto por método (`using var ctx = factory.Create(caminho)`). Testar `Excluir` logo após `Save` com 2 arquivos. |
| 2 | **`VaultDatabaseMigrator` baseline por conexão** (`VaultDatabaseMigrator.cs:28`) — verifica `sqlite_master` e `__EFMigrationsHistory` | Cada `*.db` precisa de baseline isolado; `vault.db` legado já tem `VaultStore` sem histórico → migrator deve rodar por arquivo no factory, não uma vez só no startup. | Chamar `ApplyMigrations` dentro de `VaultDbContextFactory.Create` após `UseSqlite`. Testar com DB legado `EnsureCreated` → `Migrate`. |
| 3 | **`vaults.json` concorrência** — escrita não atômica pode corromper registry se app fechar durante `Save` | Lista de cofres some; fallback para lista vazia perde referências mas não perde `*.db` (arquivos permanecem). | Escrita atômica: `File.WriteAllText(tmp) + File.Move(tmp, vaults.json, overwrite:true)`. Leitura tolerante: `try/catch JsonException → []`. Considerar `FileShare.None` + retry. |
| 4 | **Validação de nome / slug** — usuário pode digitar `con`, `aux`, `vault-1 ` com trailing, `a/b`, 100 chars | `Path.Combine` + `File.Move` falha ou cria arquivo inválido; colisão case-insensitive (`Vault-1` vs `vault-1`) no Windows | `VaultNameValidator` centralizado (Application): trim, 1–64, `Regex ^[^\\/:*?"<>|]+$`, blacklist Windows, `ToSlug()` → `Regex.Replace([^a-z0-9_-], "-")` + lowercase, colisão → sufixo `-2`. Testar com `CON`, `com1`, `vault-1` duplicado. |
| 5 | **Migração `vault.db` → `Vaults/vault-1.db`** — usuário pode ter `vault.db` + `Vaults/` já com `vault-1.db` | `Move` sobrescreve ou falha | Migrator: se `vault.db` existe e `Vaults/vault-1.db` já existe, renomear legado para `vault-1 (importado).db` ou `vault-3.db` (próximo livre). Logar. Cobrir com teste `Migrator_ComVaultDbEJaExisteVault1_NaoDeveSobrescrever`. |
| 6 | **Chave em memória ao trocar** — `VaultSessionService.Lock()` deve zerar antes de `DefinirAtivoAsync` | Se não zerar, chave do cofre A permanece em memória enquanto desbloqueia B (vazamento) | `SelecionarCofreAsync` sempre chama `Lock()` primeiro; teste `ZeroMemory` via verificação de `Unlocked==false` e tentativa de `SaveAsync` falhar. |
| 7 | **CI: `dotnet ef has-pending-model-changes`** (`ci.yml:31`) — model do EF não muda (registry é JSON), mas `VaultRecord` pode ganhar coluna `Nome` se alguém seguir ideia A por engano | CI falha se esquecer migration | Fase C **não** altera `VaultRecord` — manter schema idêntico por arquivo. Se precisar de metadado por DB, usar registry, não migration. Documentar em ADR 0008. |
| 8 | **Plataforma `x64` obrigatória** (`AGENTS.md:Commands`) — `UI.Tests` exige `p:Platform=x64` | Testes novos de ViewModel com registry podem falhar no CI se rodarem sem flag | Sempre rodar `dotnet test -p:Platform=x64`; `VaultRegistryTests` são Infrastructure (net8.0) e rodam sem flag, mas `UnlockViewModelMultiVaultTests` exigem `x64`. CI já separa `build-and-test` com flag. |
| 9 | **Localização (`Resources.resw`)** — novas chaves `UnlockPage_*` precisam existir em `pt-BR` e `en-US` | PRI fallback mostra chave literal se esquecer | Adicionar chaves nas duas pastas `Strings/` na etapa 7.6; teste de smoke `LocalizationService` cobre fallback. |
| 10 | **Exclusão do cofre ativo** — se excluir o arquivo em uso, `VaultPage` fica com `CurrentVault` dangling | Crash ao tentar `SaveAsync` | `ExcluirCofreAsync(id)` se `id==AtivoId` → `Lock()` + navegar para `UnlockPage` + remover do registry; `VaultViewModel` invalida `DisplayedItems`. |

## 5. Riscos e mitigações (resumo)

- **File-lock SQLite** → factory + `using` por operação (mitigação #1).
- **Corrupção de `vaults.json`** → escrita atômica + fallback vazio (mitigação #3).
- **Nome inválido** → validador único + testes de slug (mitigação #4).
- **Perda de `vault.db` na migração** → mover, não deletar sem confirmação de sucesso (mitigação #5).

## 6. Critérios de pronto (DoD) da Fase C

- [ ] `docs/adr/0008-multi-arquivo-de-cofre.md` marcado como implementado e reflete o código
- [ ] `vaults.json` + `Vaults/*.db` + `IVaultRegistry` + `IVaultDbContextFactory` implementados e testados
- [ ] `UnlockViewModel`/`UnlockPage` com lista de saves antes do unlock, criar `vault-N`, renomear/excluir sem desbloqueio
- [ ] Migração automática `vault.db` → `Vaults/vault-1.db` com teste
- [ ] `settings.json` permanece global (sem regressão)
- [ ] `dotnet build PasswordManager.slnx -p:Platform=x64` e `dotnet test PasswordManager.slnx -p:Platform=x64 --no-build` verdes (≥ 270 testes)
- [ ] `dotnet ef migrations has-pending-model-changes` verde
- [ ] `AGENTS.md` e `README.md` atualizados (item 7 `IMPLEMENTADO`)

## 7. Referências

- ADR 0008 (`docs/adr/0008-multi-arquivo-de-cofre.md:1`)
- ADR 0003 (`docs/adr/0003-armazenamento-do-cofre-como-blob-unico-criptografado.md:1`), ADR 0004, ADR 0005
- `src/PasswordManager.Infrastructure/Persistence/VaultRepository.cs:24`, `VaultDbContext.cs:6`, `VaultDatabaseMigrator.cs:22`
- `src/PasswordManager.Application/VaultSession/VaultSessionService.cs:21`, `IVaultSessionService.cs:26`
- `src/PasswordManager.Presentation/ViewModels/UnlockViewModel.cs:64`, `src/PasswordManager.UI/Views/UnlockPage.xaml.cs:21`, `src/PasswordManager.UI/App.xaml.cs:339`
