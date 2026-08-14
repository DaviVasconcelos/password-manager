using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using PasswordManager.UI.ViewModels;
using System;

namespace PasswordManager.UI.Views;

/// <summary>
/// Página de desbloqueio/criação do cofre. Navega para
/// <see cref="VaultPage"/> após desbloquear com sucesso.
/// </summary>
public sealed partial class UnlockPage : Page
{
    public UnlockViewModel ViewModel { get; }

    public UnlockPage()
    {
        ViewModel = App.Services.GetRequiredService<UnlockViewModel>();
        InitializeComponent();
        ViewModel.Desbloqueado += OnDesbloqueado;

        SenhaMestraBox.PasswordChanged += (_, _) => ViewModel.SenhaMestra = SenhaMestraBox.Password;
        ConfirmacaoBox.PasswordChanged += (_, _) => ViewModel.ConfirmacaoSenha = ConfirmacaoBox.Password;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        try
        {
            await ViewModel.InicializarAsync();
        }
        catch (Exception ex)
        {
            ViewModel.Erro = ex.Message;
        }
    }

    private void OnDesbloqueado()
    {
        Frame.Navigate(typeof(VaultPage));
    }
}