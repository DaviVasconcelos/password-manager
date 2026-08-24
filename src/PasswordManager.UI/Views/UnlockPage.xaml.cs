using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using PasswordManager.UI.Localization;
using PasswordManager.UI.ViewModels;
using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace PasswordManager.UI.Views;

/// <summary>
/// Página de desbloqueio/criação do cofre. Navega para
/// <see cref="VaultPage"/> após desbloquear com sucesso.
/// </summary>
public sealed partial class UnlockPage : Page
{
    public UnlockViewModel ViewModel { get; }

    private readonly ILocalizationService _localization;

    public UnlockPage()
    {
        ViewModel = App.Services.GetRequiredService<UnlockViewModel>();
        _localization = App.Services.GetRequiredService<ILocalizationService>();
        InitializeComponent();
        ViewModel.Unlocked += OnDesbloqueado;

        SenhaMestraBox.PasswordChanged += (_, _) => ViewModel.SenhaMestra = SenhaMestraBox.Password;
        ConfirmacaoBox.PasswordChanged += (_, _) => ViewModel.ConfirmacaoSenha = ConfirmacaoBox.Password;
        SenhaMestraBox.Focus(FocusState.Keyboard);
    }

    /// <summary>
    /// Enter no campo de senha: no modo desbloquear, confirma direto; no
    /// modo criar, avança para o campo de confirmação.
    /// </summary>
    private void OnSenhaKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter)
            return;

        e.Handled = true;

        if (ViewModel.ModoCriar)
        {
            ConfirmacaoBox.Focus(FocusState.Keyboard);
        }
        else if (ViewModel.UnlockCommand.CanExecute(null))
        {
            ViewModel.UnlockCommand.Execute(null);
        }
    }

    /// <summary>
    /// Enter no campo de confirmação cria o cofre.
    /// </summary>
    private void OnConfirmacaoKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter)
            return;

        e.Handled = true;

        if (ViewModel.CreateCommand.CanExecute(null))
            ViewModel.CreateCommand.Execute(null);
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        try
        {
            await ViewModel.InitializeAsync();
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

    private async void OnImportarBackupClick(object sender, RoutedEventArgs e)
    {
        var arquivo = await EscolherArquivoBackupAsync();
        if (arquivo is null)
            return;

        var senha = await PedirSenhaAsync();
        if (senha is null)
            return;

        var buffer = await FileIO.ReadBufferAsync(arquivo);
        var conteudo = new byte[buffer.Length];
        DataReader.FromBuffer(buffer).ReadBytes(conteudo);
        await ViewModel.ImportarAsync(conteudo, senha);
    }

    private async Task<StorageFile?> EscolherArquivoBackupAsync()
    {
        var picker = new FileOpenPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, App.MainWindowHandle);
        picker.FileTypeFilter.Add(".vault");

        return await picker.PickSingleFileAsync();
    }

    private async Task<string?> PedirSenhaAsync()
    {
        var senhaBox = new PasswordBox
        {
            PlaceholderText = _localization.GetString("UnlockPage_SenhaBackupBox.PlaceholderText"),
            PasswordRevealMode = PasswordRevealMode.Peek
        };

        var painel = new StackPanel { Spacing = 8 };
        painel.Children.Add(new TextBlock
        {
            Text = _localization.GetString("UnlockPage_TextoImportarBackup.Text"),
            TextWrapping = TextWrapping.Wrap
        });
        painel.Children.Add(senhaBox);

        var dialogo = new ContentDialog
        {
            Title = _localization.GetString("UnlockPage_DialogImportarBackup.Title"),
            Content = painel,
            PrimaryButtonText = _localization.GetString("UnlockPage_DialogImportarBackup.PrimaryButtonText"),
            CloseButtonText = _localization.GetString("UnlockPage_DialogImportarBackup.CloseButtonText"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
            return null;

        return senhaBox.Password;
    }
}