# AGENTS.md

Local password manager (Clean Architecture, C#). WinUI 3 desktop app; SQLite/EF Core storage planned (ADR 0003). Read `docs/adr/*` before touching domain or persistence code.

## Commands

- Build: `dotnet build PasswordManager.slnx` (Debug). Verified passing; tests at `dotnet test PasswordManager.slnx`.
- Single test project: `dotnet test tests/PasswordManager.Domain.Tests/PasswordManager.Domain.Tests.csproj` (xUnit + FluentAssertions).
- The solution is `.slnx`, not `.sln`. It requires the SDK pinned in `global.json` (10.0.302, `rollForward: latestFeature`). Projects target `net8.0`; SDK 10 builds them fine — do not "fix" the TFM.
- `src/PasswordManager.UI.slnx` is a UI-only solution (MSIX deploy); the UI needs Windows/App SDK tooling and platform mapping (x86/x64/ARM64), so it is not runnable headless from CLI.

## Architecture

- Clean Architecture: `src/PasswordManager.Domain` (no deps), `Application` (interfaces only), `Infrastructure` (empty, no EF Core yet), `UI` (WinUI 3 scaffold only).
- `Vault` is the DDD aggregate root (ADR 0001). `VaultItem`/`VaultFolder` are child entities mutated ONLY through `Vault` methods (`AddItem`, `RemoveItem`, `AddFolder`, `RemoveFolder`, `AssignItemToFolder`). Never expose or mutate `_items`/`_folders` directly; there is no `IVaultItemRepository`.
- Persistence/deserialization goes through `internal` `Rehydrate` factories on entities (enabled by `InternalsVisibleTo` in Domain). `RemoveFolder` must NOT delete its items — it only clears `FolderId`.
- Storage is a single encrypted JSON blob (ADR 0003): serialize whole `Vault`, encrypt with AES-256-GCM via `ICryptoService`, upsert one SQLite row. Search/filter happens in memory after decrypt.

## Conventions

- All code comments, exception messages, test names, and docs are in Brazilian Portuguese (pt-BR). Write new code/tests that way.
- `CryptographicIntegrityException` lives at `Application/Abstractions/CryptographicIntegrityException.cs` but is in namespace `PasswordManager.Application.Exceptions` — keep the namespace when importing.
- Entities use private parameterless ctors (EF Core) + static factories; `Guid` keys, `DateTime.UtcNow` timestamps, private setters.
- Tests: `xUnit` + `FluentAssertions` (only Domain.Tests references FluentAssertions). Test method names are `Method_Scenario_ExpectedResult` in pt-BR, e.g. `RemoveItem_ComIdInexistente_DeveLancarExcecao`.
- No CI/CD exists yet (roadmap item); don't assume `.github/workflows`.

## Current state

- Implemented: Domain entities + 34 domain tests. Stub: Application interfaces only, Infrastructure empty, UI is `App`+`MainWindow` scaffold. `tests/PasswordManager.Application.Tests` has no test files; `Infrastructure.Tests` has placeholder `UnitTest1`.
