# 0009 - TOTP/2FA em itens do cofre (entrada manual, SHA-1/30s/6 dígitos)

## Status
Proposto — escopo travado na v1 (entrada manual do secret, sem QR). Implementação fatiada em T2–T8.

## Contexto
O roadmap prevê TOTP/2FA com o secret criptografado dentro do item do cofre
e geração do código de 6 dígitos na UI, começando pela entrada manual do
secret (geração de QR fica como evolução futura). Hoje `VaultItem`
(`src/PasswordManager.Domain/Entities/VaultItem.cs`) não tem nenhum campo
para o secret, e a serialização (`VaultData`/`VaultDataMapper` em
`src/PasswordManager.Infrastructure/Persistence/Serialization/VaultData.cs`)
não o conhece. O secret precisa herdar as mesmas garantias do resto do item
(blob único criptografado por arquivo — ADR 0003 — com Argon2id +
AES-256-GCM — ADR 0004) sem exigir migration de schema do EF nem bump de
versão do `.vault` (ADR 0005).

## Decisão

### Parâmetros v1 (travados)
- Algoritmo: **HMAC-SHA1** (`System.Security.Cryptography.HMACSHA1` da BCL,
  sem pacote NuGet novo), passo de **30 s**, **6 dígitos**, época Unix —
  conforme RFC 6238/RFC 4226. Compatível com Google/Microsoft Authenticator.
- Validação com tolerância de **±1 passo** (divergência de relógio).
- **Fora de escopo na v1**: leitura de QR, parse de URI `otpauth://`,
  SHA-256/SHA-512, dígitos configuráveis.

### Armazenamento
- Novo campo opcional `VaultItem.TotpSecret` (`string?`, `null`/vazio = item
  sem TOTP). Mutação só pelo agregado (`DefinirTotpSecret`/`RemoverTotpSecret`
  no item, via `Vault.AddItem`/`UpdateItem`), preservando o ADR 0001.
- Formato canônico do secret: Base32 (`A-Z2-7`), normalizado ao salvar
  (trim, maiúsculas, ignora espaços e `-`, padding opcional); bytes
  decodificados entre 10 e 64. A validação canônica vive no Domain; a
  Presentation/UI só espelha a mensagem de erro.
- O campo viaja dentro do JSON do blob (DTO `VaultItemData` ganha
  `TotpSecret?` nullable e aditivo): cofres/`.vault` antigos sem o campo
  desserializam como `null`. **Sem migration EF e sem bump de versão do
  `.vault`**. `Vault.MergeFrom` preserva o secret ao clonar itens.

### Camadas
- **Application — novo contrato** `ITotpService` (+ `ITimeProvider` com
  `SystemTimeProvider`/`FakeTimeProvider` para testes):
  `Gerar(secretBase32, instante)`, `SegundosRestantes(instante)`,
  `Validar(secret, codigo, instante)`.
- **Sessão**: `AddItemAsync`/`ReloadItemAsync` ganham `totpSecret` opcional
  (overloads antigos delegam com `null`); lock/salt/blob inalterados.
- **Presentation/UI**: `ItemEditorViewModel` ganha campo de secret com erro
  ao vivo; `VaultViewModel` exibe código + countdown com 1 `ITimer` de 1 s
  (mesmo padrão dos timers existentes) e comando de copiar **o código**
  (nunca o secret). I/O continua na UI; file pickers e diálogos seguem o
  padrão atual.
- **i18n**: novas chaves `ItemEditor_Totp_*` / `VaultPage_Totp_*` em
  `pt-BR` e `en-US`, seguindo o padrão das chaves `UnlockPage_*` da Fase C.

### Segurança
- O secret nunca aparece em log nem é copiado para a área de
  transferência (só o código temporário). Repousa exclusivamente dentro do
  blob AES-256-GCM; a sessão continua retendo só a chave derivada, zerada
  com `ZeroMemory` no `Lock()`.

## Consequências

- **Positivas**:
  - 2FA funcional sem nova dependência e sem quebrar cofres/backups
    existentes (campo aditivo).
  - Lógica de tempo testável (relógio abstraído) e coberta pelos vetores
    oficiais do RFC 6238 Apêndice B (SHA-1).
  - Caminho evolutivo limpo: QR/`otpauth://`/SHA-256 entram depois sem
    mudar o armazenamento.
- **Negativas / pontos de atenção**:
  - Entrada manual é propensa a erro de digitação — mitigado com
    normalização + validação ao vivo + mensagem em pt-BR.
  - Relógios dessincronizados geram códigos "válidos" rejeitados pelos
    sites — mitigado com tolerância ±1 passo e countdown visível.
  - Códigos copiados vivem na área de transferência até expirarem no site;
    sem limpeza dedicada (o código expira sozinho em ≤ 30 s).

## Referências
- RFC 6238 (TOTP) / RFC 4226 (HOTP, truncamento dinâmico)
- ADR 0001 (agregado), ADR 0003 (blob único), ADR 0004 (Argon2id/AES-GCM),
  ADR 0005 (`.vault`), ADR 0008 (multi-arquivo)
- `src/PasswordManager.Domain/Entities/VaultItem.cs`
- `src/PasswordManager.Domain/Entities/Vault.cs`
- `src/PasswordManager.Infrastructure/Persistence/Serialization/VaultData.cs`
- `src/PasswordManager.Application/VaultSession/IVaultSessionService.cs`
- `src/PasswordManager.Presentation/ViewModels/ItemEditorViewModel.cs`
- `src/PasswordManager.Presentation/ViewModels/VaultViewModel.cs`
