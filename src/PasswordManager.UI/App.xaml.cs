using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using PasswordManager.Application.Abstractions;
using PasswordManager.Application.PasswordGeneration;
using PasswordManager.Application.Settings;
using PasswordManager.Application.VaultSession;
using PasswordManager.Infrastructure.Cryptography;
using PasswordManager.Infrastructure.ExportImport;
using PasswordManager.Infrastructure.Persistence;
using PasswordManager.Infrastructure.Settings;
using PasswordManager.UI.ViewModels;
using System;
using System.IO;

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

        /// <summary>
        /// Container de DI da aplicação (inicializado em OnLaunched).
        /// </summary>
        public static IServiceProvider Services =>
            _serviceProvider ?? throw new InvalidOperationException(
                "O provedor de serviços ainda não foi inicializado.");

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
            InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            _serviceProvider = ConfigureServices();

            _window = new MainWindow();
            MainWindowHandle = WinRT.Interop.WindowNative.GetWindowHandle(_window);
            _window.Activate();
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PasswordManager");
            Directory.CreateDirectory(appDataDir);

            services.AddSingleton<ICryptoService, CryptoService>();
            services.AddSingleton<IPasswordGenerator, PasswordGenerator>();
            services.AddSingleton<IPasswordStrengthEvaluator, PasswordStrengthEvaluator>();

            services.AddSingleton<VaultDbContext>(_ =>
            {
                var bankPath = Path.Combine(appDataDir, "vault.db");

                var options = new DbContextOptionsBuilder<VaultDbContext>()
                    .UseSqlite($"Data Source={bankPath}")
                    .Options;

                var context = new VaultDbContext(options);
                context.Database.EnsureCreated();
                return context;
            });

            services.AddSingleton<IAppSettingsService>(_ =>
                new AppSettingsService(Path.Combine(appDataDir, "settings.json")));

            services.AddSingleton<IVaultRepository, VaultRepository>();
            services.AddSingleton<IExportImportService, ExportImportService>();
            services.AddSingleton<IVaultSessionService, VaultSessionService>();

            services.AddTransient<UnlockViewModel>();
            services.AddTransient<VaultViewModel>();
            services.AddTransient<ItemEditorViewModel>();
            services.AddTransient<SettingsViewModel>();

            services.AddSingleton<MainWindow>();

            return services.BuildServiceProvider();
        }
    }
}