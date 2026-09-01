# 0006 - Integração contínua com GitHub Actions

## Status
Aceito — atualizado em 2026-09-01 para distribuição MSI (WiX)

## Contexto
O roadmap previa CI/CD como etapa final e, até então, não existia nenhuma
automação de build/teste (nem `.github/workflows`). O projeto precisa de uma
verificação automática de que a solution compila e que os testes das três
camadas (hoje 4 projetos, 247 testes) passam a cada mudança.

A solution principal (`PasswordManager.slnx`) inclui o projeto WinUI 3
(`src/PasswordManager.UI`), que exige tooling do Windows (Windows App SDK,
`Microsoft.Windows.SDK.BuildTools`) e mapeamento de plataforma (x86/x64/ARM64).
Portanto o runner do CI precisa ser Windows. A distribuição é **MSI (WiX)**
unpackaged — `WindowsPackageType=None`, `EnableMsixTooling=false` (nunca MSIX).

## Decisão

- Usar **GitHub Actions** com runner `windows-latest`.
- Gatilhos: `push` em qualquer branch e `pull_request`.
- **Job `build-and-test`:**
  - `dotnet tool restore` (`dotnet-ef` 8.0.30 via `.config/dotnet-tools.json`)
  - `dotnet ef migrations has-pending-model-changes --project src/PasswordManager.Infrastructure` — falha se o modelo mudar sem nova migration
  - `dotnet build PasswordManager.slnx --configuration Debug -p:Platform=x64`
  - `dotnet test PasswordManager.slnx --configuration Debug --no-build -p:Platform=x64 --logger trx` (4 projetos: Domain 57 + Infrastructure 66 + Application 66 + UI 58 = 247)
  - `actions/upload-artifact` com `TestResults/**/*.trx` (retention 14 dias)
- **Job `build-msi` (Fase D, depende de `build-and-test`):**
  - `dotnet publish src/PasswordManager.UI -c Release -p:Platform=x64 -r win-x64 --self-contained -o publish` (`PublishTrimmed=false`, EF Core não é trimming-safe)
  - `installer/generate-AppFiles.ps1 -PublishDir publish -Output installer/AppFiles.wxs` + `wix build -arch x64 -d PublishDir=publish -o PasswordManager-0.1.0-x64.msi installer/Package.wxs installer/AppFiles.wxs` (WiX Toolset 5.0.2, `installer/Package.wxs` com `UpgradeCode` estável + `MajorUpgrade`, `Scope=perMachine`)
  - `actions/upload-artifact` com `PasswordManager-*.msi` (retention 30 dias, **unsigned / next-next**, sem certificado)
- SDK fixado pelo `actions/setup-dotnet` lendo o `global.json`
  (10.0.302, `rollForward: latestFeature`); um segundo passo do
  `setup-dotnet` com `8.0.x` garante o runtime do .NET 8 para a execução
  dos testes (os projetos target `net8.0`).

Fora do escopo: assinatura de código/certificado, versionamento via git tag, auto-update e publicação em store/winget (evolução da Fase D).

## Consequências

- **Positivas**:
  - Validação automática de build + testes (247 testes) em cada push/PR.
  - O build da UI (WinUI 3) é exercitado no runner Windows, cobrindo também
    as dependências nativas do projeto.
  - MSI gerado automaticamente a cada push/PR como artefato testável (sem precisar assinar).
  - Determinismo do SDK via `global.json` + `setup-dotnet`.
- **Pontos de atenção**:
  - O job exige runner Windows por causa da UI; os testes em si rodariam em
    qualquer sistema operacional.
  - Se o workload do WinUI falhar no runner (tooling específico não
    instalado), o fallback é separar o CI em dois jobs: testes nos três
    projetos não-UI (headless-safe) e build da UI isolado (já isolado em `build-msi`).
  - `AppFiles.wxs` é gerado dinamicamente e é `gitignored`; `Package.wxs` mantém `Version` sincronizada manualmente com git tag.
