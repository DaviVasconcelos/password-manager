using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
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

    public UnlockPage()
    {
        ViewModel = App.Services.GetRequiredService<UnlockViewModel>();
        InitializeComponent();
        ViewModel.Unlocked += OnDesbloqueado;

        SenhaMestraBox.PasswordChanged += (_, _) => ViewModel.SenhaMestra = SenhaMestraBox.Password;
        ConfirmacaoBox.PasswordChanged += (_, _) => ViewModel.ConfirmacaoSenha = ConfirmacaoBox.Password;
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
            PlaceholderText = "Senha mestra do backup",
            PasswordRevealMode = PasswordRevealMode.Peek
        };

        var painel = new StackPanel { Spacing = 8 };
        painel.Children.Add(new TextBlock
        {
            Text = "Digite a senha mestra usada para criptografar o arquivo. Ela será a senha do novo cofre.",
            TextWrapping = TextWrapping.Wrap
        });
        painel.Children.Add(senhaBox);

        var dialogo = new ContentDialog
        {
            Title = "Importar backup",
            Content = painel,
            PrimaryButtonText = "Importar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
            return null;

        return senhaBox.Password;
    }
}