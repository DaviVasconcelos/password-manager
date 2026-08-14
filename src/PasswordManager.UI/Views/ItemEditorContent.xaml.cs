using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PasswordManager.UI.ViewModels;
using System.ComponentModel;

namespace PasswordManager.UI.Views;

/// <summary>
/// Conteúdo do diálogo de criação/edição de item. Expõe o
/// <see cref="ItemEditorViewModel"/> para a página coletar os valores ao
/// confirmar o diálogo. A senha é sincronizada com o
/// <see cref="PasswordBox"/> via code-behind (o Password não é bindable).
/// </summary>
public sealed partial class ItemEditorContent : UserControl
{
    public ItemEditorViewModel ViewModel { get; }

    public ItemEditorContent()
    {
        ViewModel = App.Services.GetRequiredService<ItemEditorViewModel>();
        InitializeComponent();

        SenhaBox.PasswordChanged += (_, _) => ViewModel.Senha = SenhaBox.Password;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ItemEditorViewModel.Senha))
            SenhaBox.Password = ViewModel.Senha;
    }

    private void OnAlternarVisibilidadeClick(object sender, RoutedEventArgs e)
    {
        SenhaBox.PasswordRevealMode = SenhaBox.PasswordRevealMode == PasswordRevealMode.Hidden
            ? PasswordRevealMode.Visible
            : PasswordRevealMode.Hidden;
    }
}