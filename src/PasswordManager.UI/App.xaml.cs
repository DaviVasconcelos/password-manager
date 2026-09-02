using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using PasswordManager.Application.Abstractions;
using PasswordManager.Application.PasswordGeneration;
using PasswordManager.Application.Settings;
using PasswordManager.Application.VaultSession;
using PasswordManager.Application.VaultRegistry;
using PasswordManager.Infrastructure.Cryptography;
using PasswordManager.Infrastructure.ExportImport;
using PasswordManager.Infrastructure.Persistence;
using PasswordManager.Infrastructure.Settings;
using PasswordManager.Infrastructure.VaultRegistry;
using PasswordManager.UI.Localization;
using PasswordManager.UI.Services;
using PasswordManager.UI.ViewModels;
using System;
using System.IO;
using System.Linq;

namespace PasswordManager.UI
{
    /// <summary>
    /// Composition root da aplicação: registra as dependências (crypto,
    /// persistência, export/import e sessão) e as ViewModels no container
    /// de DI.
    /// </summary>
    public partial class App : Microsoft.UI.Xaml.Application
    {
        private static IServiceProvider? _serviceProvider;
        private Window? _window;
        private AppInstance? _singleInstance;
        private bool _windowClosedHandled;

        /// <summary>
        /// Container de DI da aplicação (inicializado em OnLaunched).
        /// </summary>
        public static IServiceProvider Services =>
            _serviceProvider ?? throw new InvalidOperationException(ObterMensagemProvedorNaoInicializado());

        private static string ObterMensagemProvedorNaoInicializado()
        {
            try
            {
                var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse("Resources");
                var mensagem = loader.GetString("App_Erro_ProvedorNaoInicializado");
                return string.IsNullOrEmpty(mensagem)
                    ? "O provedor de serviços ainda não foi inicializado."
                    : mensagem;
            }
            catch
            {
                // Fallback para a literal pt-BR se o PRI não estiver disponível (ex.: testes).
                return "O provedor de serviços ainda não foi inicializado.";
            }
        }

        /// <summary>
        /// Handle da janela principal, usado para inicializar os file
        /// pickers (FileSavePicker/FileOpenPicker) no WinUI 3.
        /// </summary>
        public static IntPtr MainWindowHandle { get; private set; }

        /// <summary>
        /// Initializes the singleton application object. This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            AplicarIdiomaSalvo();
            AplicarTemaSalvo();
            InitializeComponent();
        }

        /// <summary>
        /// Aplica o idioma persistido em settings.json (auto/pt-BR/en-US).
        /// Em "auto" usa o idioma do SO com fallback determinístico pt-BR/en-US.
        /// Lê o arquivo diretamente para funcionar antes da DI estar pronta.
        /// </summary>
        private static void AplicarIdiomaSalvo()
        {
            try
            {
                var idioma = LerIdiomaDoDisco() ?? "auto";
                AplicarOverrideIdioma(idioma);
            }
            catch
            {
                // Em testes o PRI/idiomas podem não estar disponíveis; o fallback do ResourceLoader cobre.
            }
        }

        private static string? LerIdiomaDoDisco()
        {
            try
            {
                var caminho = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PasswordManager", "settings.json");
                if (!File.Exists(caminho))
                    return null;

                var json = File.ReadAllText(caminho);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("idioma", out var el) && el.ValueKind == System.Text.Json.JsonValueKind.String)
                    return el.GetString();
                if (doc.RootElement.TryGetProperty("Idioma", out var el2) && el2.ValueKind == System.Text.Json.JsonValueKind.String)
                    return el2.GetString();
            }
            catch
            {
            }

            return null;
        }

        private static void AplicarOverrideIdioma(string idioma)
        {
            // "auto" = limpar override e deixar o MRT escolher via Languages do SO (fallback DefaultLanguage=en-US).
            // Para idioma explícito, usar o código tal qual (ex: es-ES, fr-FR) — sem mapeamento fixo,
            // assim novos idiomas adicionados via Strings/<lang> funcionam sem mudar código.
            string? alvo = null;
            bool isAuto = string.Equals(idioma, "auto", StringComparison.OrdinalIgnoreCase);
            if (isAuto)
                alvo = string.Empty;
            else
                alvo = idioma;

            try
            {
                Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = alvo;
            }
            catch
            {
            }

            try
            {
                Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = alvo;
            }
            catch
            {
            }

            try
            {
                string cultureAlvo = alvo;
                if (isAuto)
                {
                    var sys = Windows.Globalization.ApplicationLanguages.Languages.FirstOrDefault();
                    if (!string.IsNullOrEmpty(sys))
                        cultureAlvo = sys;
                    else
                        cultureAlvo = "en-US";
                }

                var culture = new System.Globalization.CultureInfo(cultureAlvo);
                System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culture;
                System.Globalization.CultureInfo.DefaultThreadCurrentCulture = culture;
                System.Threading.Thread.CurrentThread.CurrentUICulture = culture;
                System.Threading.Thread.CurrentThread.CurrentCulture = culture;
            }
            catch
            {
            }

            // WinUI 3 não empacotado: x:Uid usa o MRT Core (Windows.ApplicationModel.Resources.Core).
            try
            {
                var rm = Windows.ApplicationModel.Resources.Core.ResourceManager.Current;
                if (rm?.DefaultContext?.QualifierValues != null)
                {
                    string qualifierAlvo = alvo;
                    if (isAuto)
                    {
                        var sys = Windows.Globalization.ApplicationLanguages.Languages.FirstOrDefault();
                        qualifierAlvo = !string.IsNullOrEmpty(sys) ? sys : "en-US";
                    }

                    if (rm.DefaultContext.QualifierValues.ContainsKey("Language"))
                        rm.DefaultContext.QualifierValues["Language"] = qualifierAlvo;
                    else
                        rm.DefaultContext.QualifierValues.Add("Language", qualifierAlvo);
                }
            }
            catch
            {
            }


        }

        /// <summary>
        /// Aplica o tema persistido em settings.json (sistema/claro/escuro).
        /// Lê o arquivo diretamente para funcionar antes da DI estar pronta.
        /// </summary>
        private static void AplicarTemaSalvo()
        {
            try
            {
                var tema = LerTemaDoDisco() ?? AppSettings.TemaSistema;
                AplicarTema(tema);
            }
            catch
            {
            }
        }

        private static string? LerTemaDoDisco()
        {
            try
            {
                var caminho = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PasswordManager", "settings.json");
                if (!File.Exists(caminho))
                    return null;

                var json = File.ReadAllText(caminho);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("tema", out var el) && el.ValueKind == System.Text.Json.JsonValueKind.String)
                    return el.GetString();
                if (doc.RootElement.TryGetProperty("Tema", out var el2) && el2.ValueKind == System.Text.Json.JsonValueKind.String)
                    return el2.GetString();
            }
            catch
            {
            }

            return null;
        }

        /// <summary>
        /// Aplica o tema na janela principal (e guarda como pendente se a
        /// janela ainda não existir). "sistema" = Default (segue o SO).
        /// </summary>
        public static void AplicarTema(string tema)
        {
            var requested = ConverterParaElementTheme(tema);
            _temaPendente = requested;

            try
            {
                // Prioridade: janela real exibida (criada via new MainWindow() em OnLaunched).
                // O singleton de DI (AddSingleton&lt;MainWindow&gt;) é uma instância diferente
                // e não está na tela — atualizá-la não tem efeito visível.
                if (Current is App app && app._window?.Content is FrameworkElement rootReal)
                {
                    // Garantir execução na UI thread.
                    if (rootReal.DispatcherQueue.HasThreadAccess)
                    {
                        AplicarTemaNoElemento(rootReal, requested);
                    }
                    else
                    {
                        rootReal.DispatcherQueue.TryEnqueue(() => AplicarTemaNoElemento(rootReal, requested));
                    }
                    return;
                }

                if (_serviceProvider is not null)
                {
                    try
                    {
                        var mainWindow = _serviceProvider.GetService<MainWindow>();
                        if (mainWindow?.Content is FrameworkElement root)
                        {
                            AplicarTemaNoElemento(root, requested);
                            return;
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }

        private static void AplicarTemaNoElemento(FrameworkElement root, ElementTheme requested)
        {
            root.RequestedTheme = requested;
            // Garantir propagação ao Frame interno (caso o tema esteja setado no Frame)
            // e à página atual (WinUI às vezes não propaga para Page já carregada).
            if (root is Microsoft.UI.Xaml.Controls.Grid grid && grid.Children.Count > 0
                && grid.Children[0] is FrameworkElement frame)
            {
                frame.RequestedTheme = requested;
                if (frame is Microsoft.UI.Xaml.Controls.Frame f && f.Content is FrameworkElement page)
                    page.RequestedTheme = requested;
            }
        }

        private static ElementTheme _temaPendente = ElementTheme.Default;

        internal static ElementTheme ObterTemaPendente() => _temaPendente;

        private static ElementTheme ConverterParaElementTheme(string tema)
        {
            if (string.Equals(tema, AppSettings.TemaClaro, StringComparison.OrdinalIgnoreCase))
                return ElementTheme.Light;
            if (string.Equals(tema, AppSettings.TemaEscuro, StringComparison.OrdinalIgnoreCase))
                return ElementTheme.Dark;
            return ElementTheme.Default;
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            // Single-instance: registra chave única. Se já existe, redireciona e encerra esta instância.
            try
            {
                var inst = AppInstance.FindOrRegisterForKey("PasswordManagerMain");
                if (!inst.IsCurrent)
                {
                    // Redireciona a ativação para a instância principal e sai.
                    var cur = AppInstance.GetCurrent();
                    var activationArgs = cur.GetActivatedEventArgs();
                    // Fire-and-forget assíncrono com bloqueio síncrono para garantir redirecionamento antes do Exit.
                    try
                    {
                        if (activationArgs is not null)
                            inst.RedirectActivationToAsync(activationArgs).AsTask().GetAwaiter().GetResult();
                        else
                            inst.RedirectActivationToAsync(cur.GetActivatedEventArgs()).AsTask().GetAwaiter().GetResult();
                    }
                    catch
                    {
                        // Fallback: só encerra, a instância principal já existe.
                    }
                    Exit();
                    return;
                }
                _singleInstance = inst;
                _singleInstance.Activated += OnSingleInstanceActivated;
            }
            catch
            {
                // Se AppLifecycle falhar (ex.: teste), segue sem single-instance.
            }

            _serviceProvider = ConfigureServices();

            // Reaplicar após a DI estar pronta: garante que o valor do IAppSettingsService
            // (já validado) prevaleça sobre a leitura crua do ctor.
            try
            {
                var settingsService = _serviceProvider.GetRequiredService<IAppSettingsService>();
                AplicarOverrideIdioma(settingsService.Get().Idioma);
                AplicarTema(settingsService.Get().Tema);
            }
            catch
            {
            }

            _window = new MainWindow();
            // Aplicar tema pendente na nova janela (caso AplicarTema tenha sido chamado antes dela existir).
            try
            {
                if (_window.Content is FrameworkElement root)
                    root.RequestedTheme = _temaPendente;
            }
            catch
            {
            }

            // Garantir encerramento do processo ao fechar a janela (WinUI 3 unpackaged não encerra sozinho).
            _window.Closed += OnWindowClosed;

            MainWindowHandle = WinRT.Interop.WindowNative.GetWindowHandle(_window);
            _window.Activate();
        }

        private void OnSingleInstanceActivated(object? sender, AppActivationArguments args)
        {
            // Traz a janela existente para frente quando uma segunda instância tenta abrir.
            try
            {
                if (_window is null)
                    return;

                _window.DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
                        var winId = Win32Interop.GetWindowIdFromWindow(hwnd);
                        var appWindow = AppWindow.GetFromWindowId(winId);
                        if (appWindow is not null && appWindow.Presenter is OverlappedPresenter presenter)
                        {
                            if (presenter.State == OverlappedPresenterState.Minimized)
                                presenter.Restore();
                        }
                        _window.Activate();
                        // Forçar foreground no Win32 (caso esteja em segundo plano).
                        try { NativeMethods.SetForegroundWindow(hwnd); } catch { }
                    }
                    catch { _window.Activate(); }
                });
            }
            catch { }
        }

        private void OnWindowClosed(object sender, WindowEventArgs args)
        {
            if (_windowClosedHandled)
                return;
            _windowClosedHandled = true;

            try
            {
                if (_singleInstance is not null)
                    _singleInstance.Activated -= OnSingleInstanceActivated;
            }
            catch { }

            // Parar timers e trancar cofre para zerar chave em memória.
            try
            {
                if (_serviceProvider?.GetService<VaultViewModel>() is VaultViewModel vm)
                    vm.PararTimers();
            }
            catch { }
            try
            {
                if (_serviceProvider?.GetService<IVaultSessionService>() is IVaultSessionService session)
                    session.Lock();
            }
            catch { }

            // Encerra o message loop do WinUI 3 (unpackaged não encerra sozinho).
            try { Exit(); }
            catch
            {
                try { Environment.Exit(0); } catch { }
            }
        }

        private static class NativeMethods
        {
            [System.Runtime.InteropServices.DllImport("User32.dll")]
            public static extern bool SetForegroundWindow(nint hWnd);
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PasswordManager");
            Directory.CreateDirectory(appDataDir);

            var vaultsDir = Path.Combine(appDataDir, "Vaults");
            var vaultsJsonPath = Path.Combine(appDataDir, "vaults.json");
            Directory.CreateDirectory(vaultsDir);

            services.AddSingleton<ICryptoService, CryptoService>();
            services.AddSingleton<IPasswordGenerator, PasswordGenerator>();
            services.AddSingleton<IPasswordStrengthEvaluator, PasswordStrengthEvaluator>();

            services.AddSingleton<IVaultDbContextFactory, VaultDbContextFactory>();

            services.AddSingleton<IVaultRegistry>(sp =>
            {
                var registry = new FileSystemVaultRegistry(vaultsJsonPath, vaultsDir);
                // Inicialização síncrona no startup: cria pastas, migra vault.db legado -> Vaults/vault-1.db
                registry.InicializarAsync().GetAwaiter().GetResult();
                return registry;
            });

            // Legado: mantém VaultDbContext singleton para compatibilidade de testes/DI
            // (não é mais o caminho primário; multi-arquivo usa factory por arquivo).
            services.AddSingleton<VaultDbContext>(sp =>
            {
                var factory = sp.GetRequiredService<IVaultDbContextFactory>();
                var legacyPath = Path.Combine(appDataDir, "vault.db");
                return factory.Create(legacyPath);
            });

            services.AddSingleton<IAppSettingsService>(_ =>
                new AppSettingsService(Path.Combine(appDataDir, "settings.json")));

            services.AddSingleton<ILocalizationService, LocalizationService>();
            services.AddSingleton<IClipboardService, ClipboardService>();
            services.AddSingleton<ITimerFactory, DispatcherQueueTimerFactory>();
            services.AddSingleton<IIdiomaProvider, ApplicationLanguagesProvider>();

            services.AddSingleton<IVaultRepositoryFactory, VaultRepositoryFactory>();

            // Compatibilidade: IVaultRepository resolve para o ativo quando possível
            services.AddSingleton<IVaultRepository>(sp =>
            {
                try
                {
                    var factory = sp.GetRequiredService<IVaultRepositoryFactory>();
                    return factory.CreateForActive();
                }
                catch
                {
                    var f = sp.GetRequiredService<IVaultDbContextFactory>();
                    var legacyPath = Path.Combine(appDataDir, "vault.db");
                    return new VaultRepository(f, legacyPath, sp.GetRequiredService<ICryptoService>());
                }
            });

            services.AddSingleton<IExportImportService, ExportImportService>();
            services.AddSingleton<IVaultSessionService>(sp =>
                new VaultSessionService(
                    sp.GetRequiredService<IVaultRepository>(),
                    sp.GetRequiredService<IVaultRegistry>(),
                    sp.GetRequiredService<IVaultRepositoryFactory>(),
                    sp.GetRequiredService<ICryptoService>(),
                    sp.GetRequiredService<IExportImportService>()));

            services.AddTransient<UnlockViewModel>();
            services.AddTransient<VaultViewModel>();
            services.AddTransient<ItemEditorViewModel>();
            services.AddTransient<SettingsViewModel>();

            services.AddSingleton<MainWindow>();

            return services.BuildServiceProvider();
        }
    }
}