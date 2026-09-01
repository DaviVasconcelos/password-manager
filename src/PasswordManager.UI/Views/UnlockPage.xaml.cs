using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using PasswordManager.Application.VaultRegistry;
using PasswordManager.UI.Localization;
using PasswordManager.UI.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace PasswordManager.UI.Views;

/// <summary>
/// Página de desbloqueio/criação do cofre. Navega para
/// <see cref="VaultPage"/> após desbloquear com sucesso.
/// Suporta múltiplos arquivos de cofre (ADR 0008) antes do desbloqueio.
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
        ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ViewModel.SenhaMestra) && string.IsNullOrEmpty(ViewModel.SenhaMestra) && !string.IsNullOrEmpty(SenhaMestraBox.Password))
                SenhaMestraBox.Password = string.Empty;
            if (e.PropertyName == nameof(ViewModel.ConfirmacaoSenha) && string.IsNullOrEmpty(ViewModel.ConfirmacaoSenha) && !string.IsNullOrEmpty(ConfirmacaoBox.Password))
                ConfirmacaoBox.Password = string.Empty;
            if (e.PropertyName == nameof(ViewModel.NovoNomeCofre) && string.IsNullOrEmpty(ViewModel.NovoNomeCofre) && !string.IsNullOrEmpty(NovoNomeBox.Text))
                NovoNomeBox.Text = string.Empty;
        };
        SenhaMestraBox.Focus(FocusState.Keyboard);
    }

    private void OnNovoNomeTextChanged(object sender, TextChangedEventArgs e)
    {
        ViewModel.NovoNomeCofre = NovoNomeBox.Text;
    }

    /// <summary>
    /// Seleção na lista de cofres: define o cofre ativo (multi-arquivo).
    /// </summary>
    private async void OnCofresSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.FirstOrDefault() is VaultDescriptor descriptor)
        {
            ViewModel.CofreSelecionado = descriptor;
            await ViewModel.SelecionarCofreAsync(descriptor.Id);
        }
        else if (sender is ListView lv && lv.SelectedItem is VaultDescriptor d2)
        {
            ViewModel.CofreSelecionado = d2;
            await ViewModel.SelecionarCofreAsync(d2.Id);
        }
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

    private void OcultarVaultsFlyout()
    {
        try { BtnVaultPicker.Flyout?.Hide(); } catch { }
    }

    private async void OnNovoArquivoClick(object sender, RoutedEventArgs e)
    {
        OcultarVaultsFlyout();

        var nome = await PedirNomeAsync();
        if (nome is null)
            return;

        var senhas = await PedirSenhaCriacaoAsync();
        if (senhas is null)
            return;

        ViewModel.NovoNomeCofre = nome;
        ViewModel.SenhaMestra = senhas.Value.senha;
        ViewModel.ConfirmacaoSenha = senhas.Value.confirmacao;
        SenhaMestraBox.Password = senhas.Value.senha;
        ConfirmacaoBox.Password = senhas.Value.confirmacao;

        await ViewModel.CriarNovoArquivoAsync(nome);
    }

    private async void OnRenomearClick(object sender, RoutedEventArgs e)
    {
        var selecionado = ViewModel.CofreSelecionado;
        if (selecionado is null)
        {
            ViewModel.Erro = _localization.GetString("UnlockPage_Erro_NenhumCofreSelecionado");
            return;
        }

        OcultarVaultsFlyout();

        var novoNome = await PedirNovoNomeAsync(selecionado.Nome);
        if (novoNome is null)
            return;

        await ViewModel.RenomearCofreAsync(selecionado.Id, novoNome);
    }

    private async void OnExcluirClick(object sender, RoutedEventArgs e)
    {
        var selecionado = ViewModel.CofreSelecionado;
        if (selecionado is null)
        {
            ViewModel.Erro = _localization.GetString("UnlockPage_Erro_NenhumCofreSelecionado");
            return;
        }

        OcultarVaultsFlyout();

        var confirmar = await ConfirmarExclusaoAsync(selecionado.Nome);
        if (!confirmar)
            return;

        await ViewModel.ExcluirCofreAsync(selecionado.Id);
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
            XamlRoot = XamlRoot,
            RequestedTheme = App.ObterTemaPendente()
        };

        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
            return null;

        return senhaBox.Password;
    }

    private async Task<string?> PedirNomeAsync()
    {
        var nomeBox = new TextBox
        {
            PlaceholderText = _localization.GetString("UnlockPage_NovoNomeBox.PlaceholderText")
        };

        var painel = new StackPanel { Spacing = 8, MinWidth = 360, MinHeight = 120 };
        painel.Children.Add(new TextBlock
        {
            Text = _localization.GetString("UnlockPage_DialogNovoArquivo.Text"),
            TextWrapping = TextWrapping.Wrap
        });
        painel.Children.Add(nomeBox);

        var dialogo = new ContentDialog
        {
            Title = _localization.GetString("UnlockPage_DialogNovoArquivo.Title"),
            Content = painel,
            PrimaryButtonText = _localization.GetString("UnlockPage_DialogNovoArquivo.PrimaryButtonText"),
            CloseButtonText = _localization.GetString("UnlockPage_DialogNovoArquivo.CloseButtonText"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
            RequestedTheme = App.ObterTemaPendente()
        };
        dialogo.MinWidth = 420;
        dialogo.MinHeight = 300;

        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
            return null;

        // Retorna string vazia quando em branco para gerar vault-1/vault-2 automaticamente
        // (null é apenas para cancelamento)
        return nomeBox.Text?.Trim() ?? string.Empty;
    }

    private async Task<(string senha, string confirmacao)?> PedirSenhaCriacaoAsync()
    {
        var senhaBox = new PasswordBox
        {
            PlaceholderText = _localization.GetString("UnlockPage_SenhaMestraBox.PlaceholderText"),
            PasswordRevealMode = PasswordRevealMode.Peek
        };
        var confirmBox = new PasswordBox
        {
            PlaceholderText = _localization.GetString("UnlockPage_ConfirmacaoBox.PlaceholderText"),
            PasswordRevealMode = PasswordRevealMode.Peek
        };

        var erro = new TextBlock
        {
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red),
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed,
            FontSize = 12
        };

        var painel = new StackPanel { Spacing = 8, MinWidth = 360, MinHeight = 120 };
        painel.Children.Add(new TextBlock
        {
            Text = _localization.GetString("UnlockPage_DialogNovoArquivoSenha.Text"),
            TextWrapping = TextWrapping.Wrap
        });
        painel.Children.Add(senhaBox);
        painel.Children.Add(confirmBox);
        painel.Children.Add(erro);

        var dialogo = new ContentDialog
        {
            Title = _localization.GetString("UnlockPage_DialogNovoArquivo.Title"),
            Content = painel,
            PrimaryButtonText = _localization.GetString("UnlockPage_DialogNovoArquivo.PrimaryButtonText"),
            CloseButtonText = _localization.GetString("UnlockPage_DialogNovoArquivo.CloseButtonText"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
            RequestedTheme = App.ObterTemaPendente(),
            IsPrimaryButtonEnabled = false
        };
        dialogo.MinWidth = 420;
        dialogo.MinHeight = 300;

        void AtualizarBotao()
        {
            dialogo.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(senhaBox.Password) && !string.IsNullOrWhiteSpace(confirmBox.Password);
            erro.Visibility = Visibility.Collapsed;
            erro.Text = string.Empty;
        }

        senhaBox.PasswordChanged += (_, _) => AtualizarBotao();
        confirmBox.PasswordChanged += (_, _) => AtualizarBotao();

        dialogo.PrimaryButtonClick += (_, args) =>
        {
            if (string.IsNullOrWhiteSpace(senhaBox.Password) || string.IsNullOrWhiteSpace(confirmBox.Password))
            {
                erro.Text = _localization.GetString("UnlockViewModel_Erro_SenhasNaoConferem");
                erro.Visibility = Visibility.Visible;
                args.Cancel = true;
                return;
            }

            if (senhaBox.Password != confirmBox.Password)
            {
                erro.Text = _localization.GetString("UnlockViewModel_Erro_SenhasNaoConferem");
                erro.Visibility = Visibility.Visible;
                args.Cancel = true;
            }
        };

        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
            return null;

        return (senhaBox.Password, confirmBox.Password);
    }

    private async Task<string?> PedirNovoNomeAsync(string atual)
    {
        var nomeBox = new TextBox
        {
            Text = atual,
            PlaceholderText = _localization.GetString("UnlockPage_NovoNomeBox.PlaceholderText")
        };

        var painel = new StackPanel { Spacing = 8 };
        painel.Children.Add(new TextBlock
        {
            Text = _localization.GetString("UnlockPage_DialogRenomear.Text"),
            TextWrapping = TextWrapping.Wrap
        });
        painel.Children.Add(nomeBox);

        var dialogo = new ContentDialog
        {
            Title = _localization.GetString("UnlockPage_DialogRenomear.Title"),
            Content = painel,
            PrimaryButtonText = _localization.GetString("UnlockPage_DialogRenomear.PrimaryButtonText"),
            CloseButtonText = _localization.GetString("UnlockPage_DialogRenomear.CloseButtonText"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
            RequestedTheme = App.ObterTemaPendente()
        };

        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
            return null;

        return nomeBox.Text?.Trim();
    }

    private async Task<bool> ConfirmarExclusaoAsync(string nome)
    {
        var dialogo = new ContentDialog
        {
            Title = _localization.GetString("UnlockPage_DialogExcluir.Title"),
            Content = string.Format(_localization.GetString("UnlockPage_DialogExcluir.Mensagem"), nome),
            PrimaryButtonText = _localization.GetString("UnlockPage_DialogExcluir.PrimaryButtonText"),
            CloseButtonText = _localization.GetString("UnlockPage_DialogExcluir.CloseButtonText"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
            RequestedTheme = App.ObterTemaPendente()
        };

        return await dialogo.ShowAsync() == ContentDialogResult.Primary;
    }
}
