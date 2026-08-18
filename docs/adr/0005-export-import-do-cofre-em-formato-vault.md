# 0005 - Export/Import do cofre em arquivo .vault

## Status
Aceito

## Contexto
O usuário precisa de um mecanismo de backup/restauração e de transferência
do cofre entre instalações. O ADR 0003 já previa que o mesmo par
Encrypt/Decrypt do `ICryptoService` seria reutilizado tanto para a
persistência local quanto para o export/import do arquivo `.vault`.

Considerações iniciais incluíam também exportar/importar em CSV (texto
plano, para interoperar com outros gerenciadores). Foi decidido, nesta
etapa, **não** implementar CSV: o formato texto plano expõe senhas sem
proteção e foge do escopo de um MVP de backup próprio. Fica registrado como
possível evolução futura.

## Decisão

### Formato do arquivo (.vault)
Arquivo autocontido com o layout:

```
[magic "PMVT" (4 bytes)] [versão (1 byte)] [salt Argon2id (16 bytes)] [pacote AES-256-GCM]
```

- O pacote é o mesmo produzido pelo `ICryptoService` (`nonce + tag +
  ciphertext`), reaproveitando o ADR 0004.
- A serialização reaproveita os DTOs `VaultData`/`VaultDataMapper`
  (Infrastructure), os mesmos usados na persistência local.
- O salt é **novo a cada exportação**, tornando o arquivo independente do
  salt persistido localmente (o cofre local pode trocar de senha mestra
  sem afetar backups já exportados).
- A chave do arquivo é derivada da **senha mestra re-digitada pelo usuário**
  no momento da exportação (opção escolhida para evitar confusão entre
  "senha de exportação" e "senha mestra").

### Arquitetura
- `IExportImportService` (Application) com `Export(Vault, string)` e
  `Import(byte[], string)`, operando sobre bytes; **o I/O de arquivo
  (file pickers WinUI 3) é responsabilidade da UI**, mantendo as camadas
  internas testáveis.
- `ExportImportService` (Infrastructure) implementa o contrato reutilizando
  `ICryptoService` e `VaultDataMapper`.
- `IVaultSessionService` ganha `ExportAsync` e `ImportAsync`, que orquestram
  o fluxo e persistem o resultado (`SaveAsync` ou `CreateAsync`).

### Semântica de import
- **Com a sessão desbloqueada**, o usuário escolhe:
  - **Substituir**: o cofre atual é trocado pelo conteúdo do arquivo.
  - **Mesclar** (`Vault.MergeFrom` no Domain): pastas com o mesmo nome
    (ignorando caixa) são reutilizadas, e as demais são criadas; itens com
    mesmo título e usuário (ignorando caixa) são ignorados para evitar
    duplicatas; a associação de item a pasta é remapeada pela pasta
    correspondente. Novos itens/pastas ganham novos GUIDs (o agregado só
    cria identidades via `Create`/`AddItem`/`AddFolder`, preservando os
    invariantes do ADR 0001/0002).
- **Com a sessão trancada**, o import só é permitido quando **ainda não
  existe cofre local** (primeira execução / restauração). Nesse caso o
  cofre do arquivo vira o cofre da instalação, com salt local novo, e a
  senha mestra passa a ser a senha do arquivo (que acabou de ser validada
  pela descriptografia).
- Importar com o cofre local trancado e existente **não é suportado**:
  o usuário deve desbloquear primeiro (substituir/merge sobre um cofre
  invisível seria arriscado).

## Consequências
- **Positivas**:
  - Mesma garantia de privacidade do ADR 0003: nenhum metadado (títulos,
    URLs, nomes de pasta) fica legível no arquivo sem a senha.
  - O arquivo é autocontido (salt embutido), permitindo restaurar em outra
    máquina sem informações adicionais.
  - A integridade do arquivo é garantida pela tag do AES-GCM: senha errada
    ou arquivo adulterado produzem `CryptographicIntegrityException`.
  - Reuso máximo de código (crypto + serialização), sem novo formato de
    serialização.
- **Negativas / pontos de atenção**:
  - Um backup exportado com a senha antiga só abre com a senha antiga:
    após trocar a senha mestra, é responsabilidade do usuário exportar um
    novo backup (o arquivo antigo não é atualizado automaticamente).
  - O merge não preserva os GUIDs originais do arquivo (novos IDs são
    gerados), mas preserva a estrutura de pastas por nome.
  - Sem CSV nesta etapa: a interoperabilidade com outros gerenciadores fica
    para uma evolução futura deliberada.
