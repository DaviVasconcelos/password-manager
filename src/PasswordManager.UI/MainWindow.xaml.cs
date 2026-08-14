using Microsoft.UI.Xaml;
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
            ContentFrame.Navigate(typeof(UnlockPage));
        }
    }
}