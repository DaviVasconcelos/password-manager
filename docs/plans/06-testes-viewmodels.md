# Plano — Item 6: Testes de ViewModels (desacoplar de tipos WinUI)

> **Roadmap:** `AGENTS.md` Fase B item 6. **Status:** PLANEJADO.  
> **Objetivo:** tornar `UnlockViewModel`, `VaultViewModel`, `ItemEditorViewModel` e `SettingsViewModel` testáveis em `xUnit`+`FluentAssertions` sem depender de `DispatcherQueue`, `Clipboard` ou `Windows.Globalization` estáticos, cobrindo lógica de negócio da UI (filtros, CRUD, timers, clipboard, troca de tema/idioma).

## 0. Contexto e problema atual

| ViewModel | Acoplamento que impede teste headless | Arquivo |
|---|---|---|
| `VaultViewModel` | `DispatcherQueue.GetForCurrentThread().CreateTimer()` (3 timers), `Clipboard.SetContent(DataPackage)`, `DataPackage` | `src/PasswordManager.UI/ViewModels/VaultViewModel.cs:30,76,240` |
| `SettingsViewModel` | `Windows.Globalization.ApplicationLanguages.*` e `Microsoft.Windows.Globalization.*` estáticos, `CultureInfo` com side-effect global | `src/PasswordManager.UI/ViewModels/SettingsViewModel.cs:98,154` |
| `ItemEditorViewModel` | Já testável (só depende de `IPasswordGenerator`, `IPasswordStrengthEvaluator`, `IAppSettingsService`, `ILocalizationService`) | `src/PasswordManager.UI/ViewModels/ItemEditorViewModel.cs:96` |
| `UnlockViewModel` | Já testável (só `IVaultSessionService` + `ILocalizationService`, usa `Task.Run`) | `src/PasswordManager.UI/ViewModels/UnlockViewModel.cs:58` |

Consequência: `PasswordManager.slnx` hoje só cobre `Domain/Application/Infrastructure` (189 testes). A UI (`PasswordManager.UI.csproj` `net8.0-windows10.0.19041.0`, `UseWinUI=true`) não roda headless no CI (`windows-latest` compila, mas `DispatcherQueue`/`Clipboard` exigem thread UI).

Decisão do roadmap: **incluir** — extrair abstrações finas + projeto `tests/PasswordManager.UI.Tests` (`net8.0`, sem `UseWinUI`), seguindo o padrão já usado em `ILocalizationService`/`LocalizationService` (ADR 0007).

## 1. Princípios

- Comentários, mensagens e nomes de testes em **pt-BR** (`Metodo_Cenario_ResultadoEsperado`), conforme `AGENTS.md:Conventions`.
- Abstrações mínimas (ISP): `IClipboardService`, `ITimer`/`ITimerFactory`, `IIdiomaService` ou `IApplicationLanguagesProvider` — não vazar `DispatcherQueueTimer`/`DataPackage` para ViewModel.
- Fakes em `tests/` (como `FakeVaultRepository`), não mocks pesados. `FakeTimer` com disparo manual.
- ViewModels continuam `CommunityToolkit.Mvvm` (`ObservableObject`, `RelayCommand`).
- Tema claro/escuro/sistema já implementado em `ea2a5f1` (`AppSettings.Tema`, `App.AplicarTema`, `SettingsViewModel.OpcoesTema`) — não reimplementar, apenas tornar testável via abstração de tema se necessário.

## 2. Etapas (cada uma é um PR/commit isolado para agente de IA)

### Etapa 6.1 — Abstrações de infraestrutura de UI

**Criar:**
- `src/PasswordManager.UI/Services/IClipboardService.cs` — `void SetText(string texto)`, `void Clear()`, `string? GetText()` (opcional p/ asserts)
- `src/PasswordManager.UI/Services/ClipboardService.cs` — implementação real com `DataPackage`+`Clipboard` (copiar de `VaultViewModel.cs:240`)
- `src/PasswordManager.UI/Services/ITimer.cs` — `TimeSpan Interval {get;set}`, `bool IsRunning`, `event EventHandler Tick`, `void Start()`, `void Stop()`
- `src/PasswordManager.UI/Services/ITimerFactory.cs` — `ITimer Create()`
- `src/PasswordManager.UI/Services/DispatcherQueueTimerAdapter.cs` — adapta `DispatcherQueueTimer` para `ITimer` (produção)
- `src/PasswordManager.UI/Services/IIdiomaProvider.cs` — `IReadOnlyList<string> ManifestLanguages`, `IReadOnlyList<string> Languages`, `string? PrimaryLanguageOverride {get;set}` — abstrai `Windows.Globalization.ApplicationLanguages`+`Microsoft.Windows.Globalization` (usado em `SettingsViewModel.cs:98` e `App.xaml.cs:126`)

**Critério de aceitação:** compila, nenhum ViewModel ainda alterado, `App.xaml.cs:ConfigureServices` ainda sem registro (registrado na 6.2).

### Etapa 6.2 — Refatorar `VaultViewModel` para depender de abstrações

**Alterar `src/PasswordManager.UI/ViewModels/VaultViewModel.cs`:**
- Construtor passa a receber `ITimerFactory timerFactory`, `IClipboardService clipboard` (além dos 3 existentes). Manter overload antigo `[Obsolete]` apenas se necessário para compatibilidade de `App.xaml.cs:ConfigureServices` — preferir quebrar e atualizar DI.
- Substituir 3 campos `DispatcherQueueTimer` por `ITimer` (`_timerLimparClipboard`, `_timerInatividade`, `_timerInfoBanner`) criados via `timerFactory.Create()`.
- `CopiarSenha`/`CopiarUsuario`/`OnTimerCleanClipboardTick` usam `clipboard.SetText`/`Clear` em vez de `new DataPackage()`+`Clipboard.SetContent`.
- `ReiniciarTimerInatividade`, `PararTimers`, `MostrarInfoBanner`, `Trancar` operam em `ITimer`.
- Extrair `TimeSpan.FromSeconds(4)` do banner para constante privada `DuracaoInfoBanner` (testável).

**Atualizar `src/PasswordManager.UI/App.xaml.cs:ConfigureServices`:**
- `services.AddSingleton<IClipboardService, ClipboardService>()`
- `services.AddSingleton<ITimerFactory, DispatcherQueueTimerFactory>()`
- `services.AddSingleton<IIdiomaProvider, ApplicationLanguagesProvider>()` (se criado)

**Critério:** `VaultViewModel` não importa `Microsoft.UI.Dispatching` nem `Windows.ApplicationModel.DataTransfer`. Testável com fake timer/clipboard.

### Etapa 6.3 — Refatorar `SettingsViewModel` para depender de `IIdiomaProvider`

**Alterar `src/PasswordManager.UI/ViewModels/SettingsViewModel.cs`:**
- Injetar `IIdiomaProvider idiomaProvider` no ctor.
- `ConstruirOpcoesIdioma()` lê de `idiomaProvider.ManifestLanguages` em vez de `Windows.Globalization.ApplicationLanguages.ManifestLanguages` (`SettingsViewModel.cs:100`).
- `ObterIdiomaEfetivo()` (static) vira método de instância usando `idiomaProvider.Languages`.
- Remover `try/catch` silenciosos de `Windows.Globalization` — provider já encapsula fallback.

**Critério:** `SettingsViewModel` não referencia `Windows.Globalization` diretamente. Testável com `FakeIdiomaProvider(manifest: ["pt-BR","en-US","es-ES"])`.

### Etapa 6.4 — Criar projeto de testes `PasswordManager.UI.Tests`

**Criar:**
- `tests/PasswordManager.UI.Tests/PasswordManager.UI.Tests.csproj` (`net8.0`, `IsPackable=false`, refs: `coverlet.collector`, `FluentAssertions 8.10.0`, `Microsoft.NET.Test.Sdk 17.14.1`, `xunit 2.9.3`, `xunit.runner.visualstudio 3.1.4`, `Using Include="Xunit"` — copiar de `tests/PasswordManager.Application.Tests/PasswordManager.Application.Tests.csproj`).
- `ProjectReference` para `src/PasswordManager.UI/PasswordManager.UI.csproj` **não é possível** sem `UseWinUI`/`net8.0-windows` — por isso o projeto de testes deve referenciar **apenas** `PasswordManager.Application`+ abstrações e **duplicar** os ViewModels via `InternalsVisibleTo` ou extrair ViewModels para `src/PasswordManager.UI.ViewModels` (`net8.0` sem WinUI). **Decisão recomendada (menor churn):** projeto de testes alvo `net8.0-windows10.0.19041.0` com `<UseWinUI>false</UseWinUI>` e `<EnableMsixTooling>false</EnableMsixTooling>` + referência direta ao `PasswordManager.UI` — roda no `windows-latest` do CI sem precisar empacotar MSIX. Validar no spike antes de fixar.
- Alternativa documentada no plano: se o CI reclamar de `Microsoft.WindowsAppSDK`, extrair ViewModels+Services para `src/PasswordManager.Presentation` (`net8.0`) e manter `PasswordManager.UI` como shell WinUI fino.

**Atualizar:**
- `PasswordManager.slnx:8` — adicionar `<Project Path="tests/PasswordManager.UI.Tests/PasswordManager.UI.Tests.csproj" />`
- `.github/workflows/ci.yml` — job já roda `dotnet test PasswordManager.slnx --no-build` cobrirá automaticamente o novo projeto; adicionar upload TRX com `if: always()` igual aos demais.

**Fakes compartilhados:**
- `tests/PasswordManager.UI.Tests/Fakes/FakeClipboardService.cs` (armazena último texto).
- `tests/PasswordManager.UI.Tests/Fakes/FakeTimer.cs` + `FakeTimerFactory.cs` ( `DispararTick()` manual, `IsRunning`, `Interval` ).
- `tests/PasswordManager.UI.Tests/Fakes/FakeIdiomaProvider.cs`.
- Reusar `tests/PasswordManager.Application.Tests/Fakes/FakeVaultSessionService` ou criar `FakeVaultSessionService` leve (com `Vault` em memória).

**Critério:** `dotnet build PasswordManager.slnx` e `dotnet test tests/PasswordManager.UI.Tests/PasswordManager.UI.Tests.csproj` passam localmente.

### Etapa 6.5 — Testes `VaultViewModel` (prioridade máxima)

Cobertura alvo ≥ 15 testes, nomes pt-BR:

- `Inicializar_DeveAplicarConfiguracoesERecarregarPastas`
- `AplicarConfiguracoes_DeveAtualizarTimeoutsEReiniciarTimerInatividade`
- `ReloadFolders_ComPastasExistentes_DeveReconstruirOpcoesPreservandoSelecao`
- `AddFilter_ComTermoBusca_DeveFiltrarPorTituloUsuarioUrlNotasCategoria` (+ por `FolderId`)
- `AddFilter_QuandoResultadoIgual_NaoDeveRecarregarDisplayedItems` (otimização `SequenceEqual`)
- `AddItemAsync_DeveCriarItemEAtribuirPastaEAtualizarFiltro`
- `ReloadItemAsync_DeveAtualizarItemEReatribuirPastaComForcarAtualizacao`
- `AdicionarPasta_RenomearPasta_RemoverPasta_DevemRecarregarPastas`
- `CopiarSenha_DeveCopiarParaClipboardEIniciarTimerLimpeza` + `OnTimerCleanClipboardTick_DeveLimparClipboardEResetarFlag`
- `CopiarUsuario_ComUsernameVazio_NaoDeveCopiar`
- `NotificarAtividade_DeveReiniciarTimerInatividade`
- `TimerInatividade_AoDisparar_DeveTrancarSessaoELimparFlagsEDispararEventoTrancado`
- `PararTimers_DevePararTodosOsTimers`
- `MostrarInfoBanner_NotificarExportacaoSucesso_NotificarImportacaoSucesso_DevemExibirBannerPor4s` + `FecharInfoBanner_DeveOcultar`
- `TrocarSenhaMestraAsync_DeveDelegarParaSessionService`
- `RemoverItemCommand_ComItemNulo_NaoDeveFalhar` e `ComItemValido_DeveRemoverELimparSelecao`

**Validação:** `dotnet test` com `--collect:"XPlat Code Coverage"` se habilitado.

### Etapa 6.6 — Testes `SettingsViewModel`, `ItemEditorViewModel`, `UnlockViewModel`

- `SettingsViewModel` (8–10 testes): `Carregar_DevePreencherCamposComSettingsPersistidos`, `SalvarAsync_ComValoresValidos_DevePersistirERetornarTrue`, `SalvarAsync_ComTimeoutInvalido_DeveRetornarFalseEPreencherErro`, `SalvarAsync_ComNenhumaClasseDeCaractere_NaoDeveSalvar`, `ConstruirOpcoesIdioma_ComManifestVazio_DeveGarantirPtBrEEnUs`, `RequerReinicio_QuandoIdiomaMuda_DeveSerTrue`.
- `ItemEditorViewModel` (6–8 testes): `CarregarParaCriacao_DeveGerarSenhaComDefaults`, `CarregarParaEdicao_DevePreencherCampos`, `OnSenhaChanged_DeveAtualizarForcaSenha`, `GerarSenha_ComNenhumaOpcao_NaoDeveGerar` (`PodeGerar`), `CarregarOpcoes_ComPastaSelecionada_DeveSelecionarCorreto`.
- `UnlockViewModel` (6 testes): `InitializeAsync_SemCofre_DeveEntrarEmModoCriar`, `CreateAsync_ComSenhasDiferentes_DevePreencherErro`, `UnlockAsync_ComSenhaIncorreta_DevePreencherErro_SenhaIncorreta`, `CreateAsync_ComSucesso_DeveDispararUnlocked`, `ImportarAsync_ComArquivoCorrompido_DevePreencherErro`.

### Etapa 6.7 — Integração CI e documentação

- Garantir `dotnet ef migrations has-pending-model-changes` (CI) não quebra com novo projeto.
- Atualizar `AGENTS.md:Current state` (contagem de testes: 189 → ~230) e `AGENTS.md:Roadmap` item 6 para `IMPLEMENTADO`.
- Atualizar `README.md` (se existir seção de testes) com `dotnet test tests/PasswordManager.UI.Tests/...`.

### Etapa 6.8 — Limpeza e follow-up (opcional, fora do escopo mínimo)

- Remover `using` não usados, garantir `Nullable` e `TreatWarningsAsErrors` se habilitado.
- Avaliar extrair `IThemeService` se testes de `App.AplicarTema` forem desejados (hoje já coberto em `ea2a5f1`).

## 3. Riscos e mitigações

| Risco | Mitigação |
|---|---|
| `PasswordManager.UI.Tests` não compila por `UseWinUI`/`WindowsAppSDK` | Spike na 6.4 com `net8.0-windows10.0.19041.0`+`UseWinUI=false`; fallback: extrair ViewModels para lib `net8.0` |
| `FakeTimer` com `DispatcherQueue` real em teste headless | Usar `ITimer` fake 100% — ViewModel nunca toca `DispatcherQueue` após 6.2 |
| `SettingsViewModel` depende de `ResourceLoader`/`PRI` em `ILocalizationService` | Reusar `FakeLocalizationService` já existente em `Application.Tests` |
| Cobertura de timers flaky | `FakeTimer.DispararTick()` síncrono, sem `Task.Delay` |

## 4. Critérios de pronto (DoD) do item 6

- [ ] `VaultViewModel` e `SettingsViewModel` sem `using Microsoft.UI.Dispatching`, `Windows.ApplicationModel.DataTransfer`, `Windows.Globalization`
- [ ] `tests/PasswordManager.UI.Tests` criado, referenciado em `PasswordManager.slnx` e executado no CI
- [ ] ≥ 30 testes novos (Vault 15 + Settings/ItemEditor/Unlock 15), todos pt-BR, `FluentAssertions`
- [ ] `dotnet build PasswordManager.slnx` e `dotnet test PasswordManager.slnx --no-build` verdes local + CI
- [ ] `AGENTS.md` atualizado (item 6 `IMPLEMENTADO`)

## 5. Referências

- `AGENTS.md:56` (definição do item 6), `AGENTS.md:51` (tema adiado → agora implementado em `ea2a5f1`)
- `src/PasswordManager.UI/ViewModels/*`, `src/PasswordManager.UI/App.xaml.cs:232`, `src/PasswordManager.Application/Settings/AppSettings.cs:50`
- ADR 0007 (`docs/adr/0007-internacionalizacao-com-resources-resw.md`) — padrão `ILocalizationService` a seguir para novas abstrações
