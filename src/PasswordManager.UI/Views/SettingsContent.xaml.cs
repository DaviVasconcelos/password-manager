using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using PasswordManager.UI.ViewModels;

namespace PasswordManager.UI.Views;

/// <summary>
/// Conteúdo do diálogo de configurações (auto-lock, limpeza da área de
/// transferência e gerador de senha).
/// </summary>
public sealed partial class SettingsContent : UserControl
{
    public SettingsViewModel ViewModel { get; }

    public SettingsContent()
    {
        ViewModel = App.Services.GetRequiredService<SettingsViewModel>();
        InitializeComponent();
    }
}