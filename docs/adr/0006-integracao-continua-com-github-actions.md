# 0006 - Integração contínua com GitHub Actions

## Status
Aceito

## Contexto
O roadmap previa CI/CD como etapa final e, até então, não existia nenhuma
automação de build/teste (nem `.github/workflows`). O projeto precisa de uma
verificação automática de que a solução compila e que os 166 testes das três
camadas passam a cada mudança.

A solution principal (`PasswordManager.slnx`) inclui o projeto WinUI 3
(`src/PasswordManager.UI`), que exige tooling do Windows (Windows App SDK,
`Microsoft.Windows.SDK.BuildTools`) e mapeamento de plataforma (x86/x64/ARM64).
Portanto o runner do CI precisa ser Windows.

## Decisão

- Usar **GitHub Actions** com runner `windows-latest`.
- Gatilhos: `push` em qualquer branch e `pull_request`.
- Escopo desta etapa: **build + testes**.
  - `dotnet build PasswordManager.slnx` em `Debug` (mesmo comando verificado
    no AGENTS.md).
  - `dotnet test PasswordManager.slnx` em `Debug` com `--no-build`, rodando
    os três projetos de teste (Domain, Application, Infrastructure).
- SDK fixado pelo `actions/setup-dotnet` lendo o `global.json`
  (10.0.302, `rollForward: latestFeature`); um segundo passo do
  `setup-dotnet` com `8.0.x` garante o runtime do .NET 8 para a execução
  dos testes (os projetos testam em `net8.0`).
- Resultados de teste (TRX) publicados como artefato de build
  (`actions/upload-artifact`) sempre que o job terminar, para facilitar o
  diagnóstico de falhas.
- **Não** publica MSIX nem assina pacote nesta etapa (ADR 0006 não cobre
  release/entrega — fica como evolução futura).

## Consequências

- **Positivas**:
  - Validação automática de build + testes (166 testes) em cada push/PR.
  - O build da UI (WinUI 3) é exercitado no runner Windows, cobrindo também
    as dependências nativas do projeto.
  - Determinismo do SDK via `global.json` + `setup-dotnet`.
- **Pontos de atenção**:
  - O job exige runner Windows por causa da UI; os testes em si rodariam em
    qualquer sistema operacional.
  - Se o workload do WinUI/MSIX falhar no runner (tooling específico não
    instalado), o fallback é separar o CI em dois jobs: testes nos três
    projetos não-UI (headless-safe) e build da UI isolado.
  - Sem cobertura de código e sem publish MSIX nesta etapa — candidatos a
    ADRs/etapas futuras.
