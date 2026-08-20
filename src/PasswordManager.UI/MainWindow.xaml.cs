using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using PasswordManager.UI.Localization;
using PasswordManager.UI.Views;

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
        }
    }
}