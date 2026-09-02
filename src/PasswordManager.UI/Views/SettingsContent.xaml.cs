using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PasswordManager.UI.ViewModels;

namespace PasswordManager.UI.Views;

/// <summary>
/// Conteúdo do diálogo de configurações (auto-lock, limpeza da área de
/// transferência e gerador de senha).
/// </summary>
public sealed partial class SettingsContent : UserControl
{
    public SettingsViewModel ViewModel { get; }

    /// <summary>
    /// Disparado quando há interação do usuário dentro das configurações
    /// (pointer/key ou alteração de valor via Popup do ComboBox/Slider).
    /// A VaultPage assina para reiniciar o timer de inatividade.
    /// </summary>
    public event Action? Atividade;

    public SettingsContent()
    {
        ViewModel = App.Services.GetRequiredService<SettingsViewModel>();
        InitializeComponent();
        PointerMoved += (_, _) => Atividade?.Invoke();
        PointerPressed += (_, _) => Atividade?.Invoke();
        KeyDown += (_, _) => Atividade?.Invoke();
        ViewModel.PropertyChanged += (_, _) => Atividade?.Invoke();
    }
}