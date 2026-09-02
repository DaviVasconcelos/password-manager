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
        AddHandler(PointerMovedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler((s, e) => Atividade?.Invoke()), true);
        AddHandler(PointerPressedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler((s, e) => Atividade?.Invoke()), true);
        AddHandler(KeyDownEvent, new Microsoft.UI.Xaml.Input.KeyEventHandler((s, e) => Atividade?.Invoke()), true);
        ViewModel.PropertyChanged += (_, _) => Atividade?.Invoke();
        // TextChanged/ValueChanged dentro do conteúdo não borbulham PropertyChanged imediatamente
        Loaded += (_, _) => HookInputs(this);
    }

    private void HookInputs(Microsoft.UI.Xaml.DependencyObject root)
    {
        var count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root, i);
            if (child is TextBox tb) tb.TextChanged += (_, _) => Atividade?.Invoke();
            if (child is PasswordBox pb) pb.PasswordChanged += (_, _) => Atividade?.Invoke();
            if (child is ComboBox cb) cb.SelectionChanged += (_, _) => Atividade?.Invoke();
            if (child is Slider sl) sl.ValueChanged += (_, _) => Atividade?.Invoke();
            if (child is ToggleSwitch ts) ts.Toggled += (_, _) => Atividade?.Invoke();
            HookInputs(child);
        }
    }
}