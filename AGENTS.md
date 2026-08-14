# AGENTS.md

Local password manager (Clean Architecture, C#). WinUI 3 desktop app; SQLite/EF Core storage (ADR 0003) and AES-256-GCM/Argon2id crypto (ADR 0004) implemented. Read `docs/adr/*` before touching domain, persistence, or crypto code.

## Commands

- Build: `dotnet build PasswordManager.slnx` (Debug). Verified passing; tests at `dotnet test PasswordManager.slnx`.
- Single test project: `dotnet test tests/PasswordManager.Domain.Tests/PasswordManager.Domain.Tests.csproj` (xUnit + FluentAssertions). Same for `tests/PasswordManager.Infrastructure.Tests/PasswordManager.Infrastructure.Tests.csproj` and `tests/PasswordManager.Application.Tests/PasswordManager.Application.Tests.csproj`.
- The solution is `.slnx`, not `.sln`. It requires the SDK pinned in `global.json` (10.0.302, `rollForward: latestFeature`). Projects target `net8.0`; SDK 10 builds them fine — do not "fix" the TFM.
- `src/PasswordManager.UI.slnx` is a UI-only solution (MSIX deploy); the UI needs Windows/App SDK tooling and platform mapping (x86/x64/ARM64), so it is not runnable headless from CLI.

## Architecture

- Clean Architecture: `src/PasswordManager.Domain` (no deps), `Application` (interfaces + `VaultSessionService` + password generation), `Infrastructure` (CryptoService + EF Core persistence), `UI` (WinUI 3 + MVVM).
- `Vault` is the DDD aggregate root (ADR 0001). `VaultItem`/`VaultFolder` are child entities mutated ONLY through `Vault` methods (`AddItem`, `RemoveItem`, `UpdateItem`, `AddFolder`, `RemoveFolder`, `RenameFolder`, `AssignItemToFolder`). Never expose or mutate `_items`/`_folders` directly; there is no `IVaultItemRepository`.
- Persistence/deserialization goes through `internal` `Rehydrate` factories on entities (enabled by `InternalsVisibleTo` in Domain). `RemoveFolder` must NOT delete its items — it only clears `FolderId`.
- Storage is a single encrypted JSON blob (ADR 0003): `serialize whole Vault ->` `encrypt with AES-256-GCM ->` one SQLite row (insert on `CreateAsync`, update on `SaveAsync`/`ChangeMasterPasswordAsync`). Implemented by `VaultRepository` (Infrastructure). Search/filter happens in memory after decrypt.
- `IVaultRepository` receives the already-derived AES key, never the master password: key derivation is an Application concern (`VaultSessionService` derives via `ICryptoService` using the stored salt). The session retains only the derived key in memory and zeroes it on lock (`CryptographicOperations.ZeroMemory`). `CreateAsync` throws if a vault already exists; `SaveAsync` requires an existing record (keeps the salt); `ChangeMasterPasswordAsync` rotates salt + blob.
- `IVaultSessionService` centralizes session + CRUD (itens/pastas com auto-save) + `BuscarItens` (filtro em memória) + `ExisteCofreAsync`. `IPasswordGenerator` (RandomNumberGenerator, sem viés de módulo) e `IPasswordStrengthEvaluator` (enum `ForcaSenha`) ficam em `Application/PasswordGeneration`. A UI fala só com o serviço de sessão; o `Vault` carregado fica na sessão e as mutações são persistidas a cada operação.
- Crypto (ADR 0004): `ICryptoService` implemented by `CryptoService` (Infrastructure) — Argon2id (`Konscious.Security.Cryptography.Argon2`) defaults to 64 MiB/3 iter./4 parallel, salt 16 bytes, key 32 bytes; AES-256-GCM package is `nonce(12) + tag(16) + ciphertext`. Argon2 params are constructor-injectable for fast tests — never weaken them in production code. Any GCM tag failure throws `CryptographicIntegrityException` (wrong master password vs. tampered data are indistinguishable on purpose).

## Conventions

- All code comments, exception messages, test names, and docs are in Brazilian Portuguese (pt-BR). Write new code/tests that way.
- `CryptographicIntegrityException` lives at `Application/Abstractions/CryptographicIntegrityException.cs` but is in namespace `PasswordManager.Application.Exceptions` — keep the namespace when importing.
- Entities use private parameterless ctors + static factories; `Guid` keys, `DateTime.UtcNow` timestamps, private setters.
- `VaultRepository` stores a fixed non-NULL singleton `Guid` (see `SingletonRecordId`). NEVER use `Guid.Empty` as a persisted key: Microsoft.Data.Sqlite binds `Guid.Empty` as BLOB while the column stores GUIDs as TEXT, so `WHERE Id = Guid.Empty` silently matches nothing (SQLite type affinity).
- Tests: `xUnit` + `FluentAssertions`. Test method names are `Method_Scenario_ExpectedResult` in pt-BR, e.g. `RemoveItem_ComIdInexistente_DeveLancarExcecao`.
- No CI/CD exists yet (roadmap item); don't assume `.github/workflows`.

## Current state

- Implemented: Domain entities + 49 domain tests; Infrastructure `CryptoService` + `VaultRepository` (SQLite/EF Core, ADR 0003) + 40 infrastructure tests; Application `VaultSessionService` (criar/desbloquear/trancar/trocar senha mestra/salvar/CRUD/busca) + `PasswordGenerator`/`PasswordStrengthEvaluator` + fakes + 48 application tests. UI funcional (WinUI 3 + CommunityToolkit.Mvvm + DI): `UnlockPage` (criar/desbloquear) e `VaultPage` (lista, busca, filtro por pasta, CRUD via diálogos, copiar senha com limpeza em 30 s, gerador + força). Persistência real em `LocalAppData\PasswordManager\vault.db` via `EnsureCreated`. No EF migration yet (tool `dotnet-ef` not installed; schema is created with `EnsureCreated`).
