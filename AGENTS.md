# AGENTS.md

Local password manager (Clean Architecture, C#). WinUI 3 desktop app; SQLite/EF Core storage (ADR 0003) and AES-256-GCM/Argon2id crypto (ADR 0004) implemented. Read `docs/adr/*` before touching domain, persistence, or crypto code.

## Design da UI (Figma)

- Plano de implementação do redesign: `docs/design/design-plan.md`. Fonte de verdade do design: `docs/design/figma-snapshot.md` (regenerável).
- Para atualizar o snapshot após mudanças no Figma: `powershell -ExecutionPolicy Bypass -File scripts/fetch-figma-design.ps1`. Requer env var `FIGMA_PERSONAL_ACCESS_TOKEN` (nunca commitar o token). Header da Figma REST API: `X-Figma-Token`.
- O snapshot `figma-snapshot.json` (cru, 10+ MB) é gitignored; só o markdown é versionado.

## Commands

- Build: `dotnet build PasswordManager.slnx` (Debug). Verified passing; tests at `dotnet test PasswordManager.slnx`.
- Single test project: `dotnet test tests/PasswordManager.Domain.Tests/PasswordManager.Domain.Tests.csproj` (xUnit + FluentAssertions). Same for `tests/PasswordManager.Infrastructure.Tests/PasswordManager.Infrastructure.Tests.csproj` and `tests/PasswordManager.Application.Tests/PasswordManager.Application.Tests.csproj`.
- The solution is `.slnx`, not `.sln`. It requires the SDK pinned in `global.json` (10.0.302, `rollForward: latestFeature`). Projects target `net8.0`; SDK 10 builds them fine — do not "fix" the TFM.
- `src/PasswordManager.UI.slnx` is a UI-only solution (MSIX deploy); the UI needs Windows/App SDK tooling and platform mapping (x86/x64/ARM64), so it is not runnable headless from CLI.
- CI/CD: GitHub Actions, `.github/workflows/ci.yml` (ADR 0006). `windows-latest`; on every push/PR it runs `dotnet build PasswordManager.slnx` (Debug) + `dotnet test PasswordManager.slnx` (Debug, `--no-build`) on the 3 test projects, and uploads TRX artifacts. `setup-dotnet` reads `global.json` (SDK 10.0.302) and also installs `8.0.x` to guarantee the .NET 8 runtime for tests. No MSIX publish/signing yet.

## Architecture

- Clean Architecture: `src/PasswordManager.Domain` (no deps), `Application` (interfaces + `VaultSessionService` + password generation), `Infrastructure` (CryptoService + EF Core persistence), `UI` (WinUI 3 + MVVM).
- `Vault` is the DDD aggregate root (ADR 0001). `VaultItem`/`VaultFolder` are child entities mutated ONLY through `Vault` methods (`AddItem`, `RemoveItem`, `UpdateItem`, `AddFolder`, `RemoveFolder`, `RenameFolder`, `AssignItemToFolder`, `MergeFrom`). Never expose or mutate `_items`/`_folders` directly; there is no `IVaultItemRepository`.
- Persistence/deserialization goes through `internal` `Rehydrate` factories on entities (enabled by `InternalsVisibleTo` in Domain). `RemoveFolder` must NOT delete its items — it only clears `FolderId`.
- Storage is a single encrypted JSON blob (ADR 0003): `serialize whole Vault ->` `encrypt with AES-256-GCM ->` one SQLite row (insert on `CreateAsync`, update on `SaveAsync`/`ChangeMasterPasswordAsync`). Implemented by `VaultRepository` (Infrastructure). Search/filter happens in memory after decrypt.
- `IVaultRepository` receives the already-derived AES key, never the master password: key derivation is an Application concern (`VaultSessionService` derives via `ICryptoService` using the stored salt). The session retains only the derived key in memory and zeroes it on lock (`CryptographicOperations.ZeroMemory`). `CreateAsync` throws if a vault already exists; `SaveAsync` requires an existing record (keeps the salt); `ChangeMasterPasswordAsync` rotates salt + blob.
- `IVaultSessionService` centralizes session + CRUD (itens/pastas com auto-save) + `BuscarItens` (filtro em memória) + `ExisteCofreAsync`. `IPasswordGenerator` (RandomNumberGenerator, sem viés de módulo) e `IPasswordStrengthEvaluator` (enum `ForcaSenha`) ficam em `Application/PasswordGeneration`. A UI fala só com o serviço de sessão; o `Vault` carregado fica na sessão e as mutações são persistidas a cada operação.
- Crypto (ADR 0004): `ICryptoService` implemented by `CryptoService` (Infrastructure) — Argon2id (`Konscious.Security.Cryptography.Argon2`) defaults to 64 MiB/3 iter./4 parallel, salt 16 bytes, key 32 bytes; AES-256-GCM package is `nonce(12) + tag(16) + ciphertext`. Argon2 params are constructor-injectable for fast tests — never weaken them in production code. Any GCM tag failure throws `CryptographicIntegrityException` (wrong master password vs. tampered data are indistinguishable on purpose).
- Export/Import (ADR 0005): `IExportImportService` (Application) + `ExportImportService` (Infrastructure). Arquivo `.vault` autocontido: `magic "PMVT"` + versão + salt novo por exportação + pacote AES-GCM (reusa `VaultDataMapper` + `ICryptoService`), criptografado com a senha mestra re-digitada. `IVaultSessionService.ExportAsync`/`ImportAsync` orquestram; no import o usuário escolhe substituir ou mesclar (`Vault.MergeFrom`: pastas por nome case-insensitive, itens deduplicados por título+usuário, novos GUIDs); import com sessão trancada só cria cofre na primeira execução. I/O de arquivo (file pickers WinUI 3) fica na UI — camadas trocam bytes. Sem CSV nesta etapa.

## Conventions

- All code comments, exception messages, test names, and docs are in Brazilian Portuguese (pt-BR). Write new code/tests that way.
- `CryptographicIntegrityException` lives at `Application/Abstractions/CryptographicIntegrityException.cs` but is in namespace `PasswordManager.Application.Exceptions` — keep the namespace when importing.
- Entities use private parameterless ctors + static factories; `Guid` keys, `DateTime.UtcNow` timestamps, private setters.
- `VaultRepository` stores a fixed non-NULL singleton `Guid` (see `SingletonRecordId`). NEVER use `Guid.Empty` as a persisted key: Microsoft.Data.Sqlite binds `Guid.Empty` as BLOB while the column stores GUIDs as TEXT, so `WHERE Id = Guid.Empty` silently matches nothing (SQLite type affinity).
- Tests: `xUnit` + `FluentAssertions`. Test method names are `Method_Scenario_ExpectedResult` in pt-BR, e.g. `RemoveItem_ComIdInexistente_DeveLancarExcecao`.

## Current state

- Implemented: Domain entities (`Vault` + `MergeFrom`) + 57 domain tests; Infrastructure `CryptoService` + `VaultRepository` (SQLite/EF Core, ADR 0003) + `ExportImportService` (.vault, ADR 0005) + `AppSettingsService` (settings JSON) + 58 infrastructure tests; Application `VaultSessionService` (criar/desbloquear/trancar/trocar senha mestra/salvar/CRUD/busca/exportar/importar) + `PasswordGenerator`/`PasswordStrengthEvaluator` + `AppSettings` (validação) + fakes + 66 application tests. UI funcional (WinUI 3 + CommunityToolkit.Mvvm + DI): `UnlockPage` (criar/desbloquear/importar backup na primeira execução) e `VaultPage` (lista, busca, filtro por pasta, CRUD via diálogos, copiar senha com limpeza em 30 s configurável, gerador + força com defaults configuráveis, auto-lock por inatividade, trocar senha mestra, exportar/importar `.vault` com substituir ou mesclar) + `SettingsContent` (Configurações). Persistência real em `LocalAppData\PasswordManager\vault.db` via `EnsureCreated` e configurações em `LocalAppData\PasswordManager\settings.json`. No EF migration yet (tool `dotnet-ef` not installed; schema is created with `EnsureCreated`). CI/CD ativo (ADR 0006): GitHub Actions em `windows-latest` compila a solution e roda os 181 testes em cada push/PR, publicando os TRX como artefato.

## Roadmap (documentado)

Ordem de execução sugerida: A → B → C → D. Itens concluídos ficam marcados e as decisões tomadas ficam registradas em cada item.

### Fase A — Robustez/UX do núcleo

1. **IMPLEMENTADO** — Tela de Configurações (timeout de auto-lock, tempo de limpeza do clipboard — antes fixo em 30 s — e defaults do gerador de senha). **Decisão**: configurações persistem em **JSON local simples** (sem segredos), fora do cofre criptografado — `IAppSettingsService` (Application) + `AppSettingsService` (Infrastructure) gravando `LocalAppData\PasswordManager\settings.json`; `SettingsViewModel` + `SettingsContent` (diálogo) na UI; gerador de senha usa os defaults configurados via `ItemEditorViewModel`.
2. **IMPLEMENTADO** — Auto-lock por inatividade usando o `Lock()` existente. **Decisão**: tranca **somente por inatividade** (não por perda de foco ou minimizar), timeout padrão **2 minutos**. `VaultViewModel` reinicia um `DispatcherQueueTimer` a cada atividade (pointer/key na `VaultPage`).
3. **IMPLEMENTADO** — Trocar senha mestra na UI. `ChangeMasterPasswordAsync` agora exige a **senha atual** (verificada por derivação de chave + comparação em tempo constante com a chave retida; senha errada lança `CryptographicIntegrityException`) e a nova senha; diálogo na `VaultPage` pede atual + nova + confirmação.
4. Tema claro/escuro/sistema. **Decisão**: **ADIADO** — implementar depois da Fase A.

### Fase B — Engenharia

5. Migrations EF Core no lugar de `EnsureCreated` (schema versionado). **Decisão**: **investir agora** (exige instalar a tool `dotnet-ef`).
6. Testes de ViewModels: desacoplar os ViewModels de tipos WinUI para torná-los testáveis e cobrir com xUnit/FluentAssertions. **Decisão**: **incluir**.
7. Recursos/i18n (UI atualmente com textos hardcoded em pt-BR).

### Fase C — Features de produto

8. Import CSV (Bitwarden/LastPass/1Password). **Decisão**: **ADIADO para futuramente** — ADR 0005 segue sem CSV.
9. TOTP/2FA. **Decisão**: **incluir**; o secret fica criptografado dentro do item do cofre; a UI gera o código de 6 dígitos; geração de QR é enhancement futuro (entrada manual do secret primeiro).
10. Favoritos / tags / health check de senhas (força, reuso, expiração).

### Fase D — Distribuição

11. Empacotamento MSIX + assinatura e build da UI no pipeline (CI hoje só cobre os test projects).
12. Auto-update / backup automático / lembretes de backup.
