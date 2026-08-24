using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using PasswordManager.UI.Localization;
using PasswordManager.UI.Views;
using Windows.UI;

namespace PasswordManager.UI
{
    /// <summary>
    /// Janela principal da aplicação, responsável apenas pela navegação
    /// entre as páginas (desbloqueio e cofre).
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Title = App.Services.GetRequiredService<ILocalizationService>().GetString("MainWindow.Title");
            ContentFrame.Navigate(typeof(UnlockPage));

            AtualizarBarraTitulo();
            if (Content is FrameworkElement raiz)
                raiz.ActualThemeChanged += (_, _) => AtualizarBarraTitulo();
        }

        /// <summary>
        /// Pinta a barra de título com os tokens do design system
        /// (sem isso, ela segue o tema do sistema e destoa do app).
        /// </summary>
        private void AtualizarBarraTitulo()
        {
            var escuro = (Content as FrameworkElement)?.ActualTheme != ElementTheme.Light;
            var fundo = CorTema("PMBackgroundBrush", escuro, Color.FromArgb(255, 0x1C, 0x1C, 0x1C), Color.FromArgb(255, 0xF3, 0xF3, 0xF3));
            var texto = CorTema("PMTextPrimaryBrush", escuro, Color.FromArgb(255, 0xFF, 0xFF, 0xFF), Color.FromArgb(255, 0x1A, 0x1A, 0x1A));
            var hover = escuro ? Color.FromArgb(255, 0x2D, 0x2D, 0x2D) : Color.FromArgb(255, 0xE0, 0xE0, 0xE0);

            var barra = AppWindow.TitleBar;
            barra.BackgroundColor = fundo;
            barra.ForegroundColor = texto;
            barra.InactiveBackgroundColor = fundo;
            barra.InactiveForegroundColor = texto;
            barra.ButtonBackgroundColor = fundo;
            barra.ButtonForegroundColor = texto;
            barra.ButtonInactiveBackgroundColor = fundo;
            barra.ButtonInactiveForegroundColor = texto;
            barra.ButtonHoverBackgroundColor = hover;
        }

        private static Color CorTema(string chave, bool escuro, Color escuroFallback, Color claroFallback)
        {
            if (App.Current.Resources.TryGetValue(chave, out object? valor) && valor is SolidColorBrush brush)
                return brush.Color;
            return escuro ? escuroFallback : claroFallback;
        }
    }
}