using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using PasswordManager.Application.Exceptions;
using PasswordManager.Domain.Entities;
using PasswordManager.UI.Localization;
using PasswordManager.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace PasswordManager.UI.Views;

/// <summary>
/// Página principal do cofre: lista, busca, filtro por pasta, CRUD de
/// itens/pastas e cópia de senha. Navega de volta para
/// <see cref="UnlockPage"/> ao trancar.
/// </summary>
public sealed partial class VaultPage : Page
{
    public VaultViewModel ViewModel { get; }

    private readonly ILocalizationService _localization;

    public VaultPage()
    {
        ViewModel = App.Services.GetRequiredService<VaultViewModel>();
        _localization = App.Services.GetRequiredService<ILocalizationService>();
        InitializeComponent();
        ViewModel.Trancado += OnTrancado;
        NavCofre.ItemInvoked += OnNavItemInvocado;

        PointerMoved += OnPointerMoved;
        PointerPressed += OnPointerPressed;
        KeyDown += OnKeyDown;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.Inicializar();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.PararTimers();
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e) => ViewModel.NotificarAtividade();

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e) => ViewModel.NotificarAtividade();

    private void OnKeyDown(object sender, KeyRoutedEventArgs e) => ViewModel.NotificarAtividade();

    private void OnTrancado()
    {
        Frame.Navigate(typeof(UnlockPage));
    }

    /// <summary>
    /// O brand não seleciona (SelectsOnInvoked=False), então o toggle do
    /// painel é tratado no ItemInvoked.
    /// </summary>
    private void OnNavItemInvocado(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer is NavigationViewItem { Tag: "brand" })
            NavCofre.IsPaneOpen = !NavCofre.IsPaneOpen;
    }

    /// <summary>
    /// Itens do pane que abrem diálogos/comandos: a seleção fica no item
    /// escolhido enquanto o diálogo está aberto e volta para "Itens" ao
    /// fechar (o conteúdo exibido continua sendo a lista de itens).
    /// </summary>
    private async void OnNavSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag is not string tag)
            return;

        switch (tag)
        {
            case "pastas":
                await AbrirGerenciarPastasAsync();
                sender.SelectedItem = NavItens;
                break;
            case "configuracoes":
                await AbrirConfiguracoesAsync();
                sender.SelectedItem = NavItens;
                break;
            case "trancar":
                ViewModel.LockCommand.Execute(null);
                break;
        }
    }

    private async void OnNovoItemClick(object sender, RoutedEventArgs e)
    {
        var editor = new ItemEditorContent();
        editor.ViewModel.CarregarParaCriacao(ViewModel.FolderOptions);

        var dialogo = CriarDialogo(_localization.GetString("VaultPage_DialogNovoItem.Title"), editor);
        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
            return;

        await ViewModel.AddItemAsync(
            editor.ViewModel.Titulo,
            editor.ViewModel.Senha,
            editor.ViewModel.Categoria,
            editor.ViewModel.Usuario,
            editor.ViewModel.Url,
            editor.ViewModel.Notas,
            editor.ViewModel.PastaSelecionada?.Pasta?.Id);
    }

    private void OnEditarItemClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is VaultItem item)
            _ = EditarItemAsync(item);
    }

    private void OnCopiarSenhaClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is VaultItem item)
            ViewModel.CopiarSenhaCommand.Execute(item);
    }

    private void OnFecharToastClick(object sender, RoutedEventArgs e)
        => ViewModel.SenhaCopiada = false;

    private async void OnExcluirItemClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not VaultItem item)
            return;

        var dialogo = new ContentDialog
        {
            Title = _localization.GetString("VaultPage_DialogExcluirItem.Title"),
            Content = string.Format(_localization.GetString("VaultPage_DialogExcluirItem.Mensagem"), item.Title),
            PrimaryButtonText = _localization.GetString("VaultPage_DialogExcluirItem.PrimaryButtonText"),
            CloseButtonText = _localization.GetString("VaultPage_DialogExcluirItem.CloseButtonText"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await dialogo.ShowAsync() == ContentDialogResult.Primary)
            ViewModel.RemoverItemCommand.Execute(item);
    }

    private async void OnItemDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ViewModel.ItemSelecionado is not null)
            await EditarItemAsync(ViewModel.ItemSelecionado);
    }

    private async Task EditarItemAsync(VaultItem item)
    {
        var editor = new ItemEditorContent();
        editor.ViewModel.CarregarParaEdicao(item, ViewModel.FolderOptions);

        var dialogo = CriarDialogo(_localization.GetString("VaultPage_DialogEditarItem.Title"), editor);
        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
            return;

        await ViewModel.ReloadItemAsync(
            item.Id,
            editor.ViewModel.Titulo,
            editor.ViewModel.Senha,
            editor.ViewModel.Categoria,
            editor.ViewModel.Usuario,
            editor.ViewModel.Url,
            editor.ViewModel.Notas,
            editor.ViewModel.PastaSelecionada?.Pasta?.Id);
    }

    private async Task AbrirGerenciarPastasAsync()
    {
        var dialogo = new ContentDialog
        {
            Title = _localization.GetString("VaultPage_DialogGerenciarPastas.Title"),
            Content = new GerenciarPastasContent(),
            CloseButtonText = _localization.GetString("VaultPage_DialogGerenciarPastas.CloseButtonText"),
            XamlRoot = XamlRoot
        };

        await dialogo.ShowAsync();
        ViewModel.ReloadFolders();
    }

    private async Task AbrirConfiguracoesAsync()
    {
        var content = new SettingsContent();
        content.ViewModel.Carregar();

        var dialogo = new ContentDialog
        {
            Title = _localization.GetString("VaultPage_DialogConfiguracoes.Title"),
            Content = content,
            PrimaryButtonText = _localization.GetString("VaultPage_DialogConfiguracoes.PrimaryButtonText"),
            CloseButtonText = _localization.GetString("VaultPage_DialogConfiguracoes.CloseButtonText"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        dialogo.PrimaryButtonClick += async (_, args) =>
        {
            if (!await content.ViewModel.SalvarAsync())
                args.Cancel = true;
        };

        if (await dialogo.ShowAsync() == ContentDialogResult.Primary)
            ViewModel.AplicarConfiguracoes();
    }

    private async void OnTrocarSenhaMestraClick(object sender, RoutedEventArgs e)
    {
        var senhaAtualBox = new PasswordBox
        {
            PlaceholderText = _localization.GetString("VaultPage_SenhaAtualBox.PlaceholderText"),
            PasswordRevealMode = PasswordRevealMode.Peek
        };
        var novaSenhaBox = new PasswordBox
        {
            PlaceholderText = _localization.GetString("VaultPage_NovaSenhaBox.PlaceholderText"),
            PasswordRevealMode = PasswordRevealMode.Peek
        };
        var confirmacaoBox = new PasswordBox
        {
            PlaceholderText = _localization.GetString("VaultPage_ConfirmacaoNovaSenhaBox.PlaceholderText"),
            PasswordRevealMode = PasswordRevealMode.Peek
        };
        var erro = new TextBlock
        {
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red),
            TextWrapping = TextWrapping.Wrap
        };

        var painel = new StackPanel { Spacing = 8 };
        painel.Children.Add(new TextBlock
        {
            Text = _localization.GetString("VaultPage_TextoTrocarSenha.Text"),
            TextWrapping = TextWrapping.Wrap
        });
        painel.Children.Add(senhaAtualBox);
        painel.Children.Add(novaSenhaBox);
        painel.Children.Add(confirmacaoBox);
        painel.Children.Add(erro);

        var dialogo = new ContentDialog
        {
            Title = _localization.GetString("VaultPage_DialogTrocarSenhaMestra.Title"),
            Content = painel,
            PrimaryButtonText = _localization.GetString("VaultPage_DialogTrocarSenhaMestra.PrimaryButtonText"),
            CloseButtonText = _localization.GetString("VaultPage_DialogTrocarSenhaMestra.CloseButtonText"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        dialogo.PrimaryButtonClick += async (_, args) =>
        {
            if (novaSenhaBox.Password != confirmacaoBox.Password)
            {
                erro.Text = _localization.GetString("VaultPage_Erro_SenhasNaoConferem");
                args.Cancel = true;
                return;
            }

            try
            {
                await Task.Run(async () => await ViewModel.TrocarSenhaMestraAsync(
                    senhaAtualBox.Password, novaSenhaBox.Password));
            }
            catch (CryptographicIntegrityException)
            {
                erro.Text = _localization.GetString("VaultPage_Erro_SenhaAtualIncorreta");
                args.Cancel = true;
            }
            catch (Exception ex)
            {
                erro.Text = ex.Message;
                args.Cancel = true;
            }
        };

        await dialogo.ShowAsync();
    }

    private ContentDialog CriarDialogo(string titulo, object conteudo)
    {
        return new ContentDialog
        {
            Title = titulo,
            Content = conteudo,
            PrimaryButtonText = _localization.GetString("VaultPage_DialogGenerico.PrimaryButtonText"),
            CloseButtonText = _localization.GetString("VaultPage_DialogGenerico.CloseButtonText"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };
    }

    private async void OnExportarClick(object sender, RoutedEventArgs e)
    {
        var arquivo = await EscolherDestinoExportacaoAsync();
        if (arquivo is null)
            return;

        var senha = await PedirSenhaAsync(
            _localization.GetString("VaultPage_DialogExportar.Title"),
            _localization.GetString("VaultPage_DialogExportar.Mensagem"));
        if (senha is null)
            return;

        try
        {
            var dados = await ViewModel.ExportarAsync(senha);
            await FileIO.WriteBytesAsync(arquivo, dados);
        }
        catch (Exception ex)
        {
            await MostrarErroAsync(_localization.GetString("VaultPage_Erro_FalhaExportar", ex.Message));
        }
    }

    private async void OnImportarClick(object sender, RoutedEventArgs e)
    {
        var arquivo = await EscolherOrigemImportacaoAsync();
        if (arquivo is null)
            return;

        var dados = await PedirDadosImportacaoAsync();
        if (dados.Senha is null)
            return;

        try
        {
            var buffer = await FileIO.ReadBufferAsync(arquivo);
            var conteudo = new byte[buffer.Length];
            DataReader.FromBuffer(buffer).ReadBytes(conteudo);
            await ViewModel.ImportarAsync(conteudo, dados.Senha, dados.Substituir);
            await MostrarInfoAsync(_localization.GetString(dados.Substituir
                ? "VaultPage_Info_CofreSubstituido"
                : "VaultPage_Info_CofreMesclado"));
        }
        catch (CryptographicIntegrityException)
        {
            await MostrarErroAsync(_localization.GetString("VaultPage_Erro_SenhaArquivoCorrompido"));
        }
        catch (InvalidOperationException ex)
        {
            await MostrarErroAsync(ex.Message);
        }
    }

    private async Task<StorageFile?> EscolherDestinoExportacaoAsync()
    {
        var picker = new FileSavePicker();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, App.MainWindowHandle);
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeChoices.Add(_localization.GetString("VaultPage_FilePicker_VaultFilter"), new List<string> { ".vault" });
        picker.SuggestedFileName = $"cofre-{DateTime.Now:yyyyMMdd}.vault";

        return await picker.PickSaveFileAsync();
    }

    private async Task<StorageFile?> EscolherOrigemImportacaoAsync()
    {
        var picker = new FileOpenPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, App.MainWindowHandle);
        picker.FileTypeFilter.Add(".vault");

        return await picker.PickSingleFileAsync();
    }

    private async Task<string?> PedirSenhaAsync(string titulo, string mensagem)
    {
        var senhaBox = new PasswordBox
        {
            PlaceholderText = _localization.GetString("VaultPage_PedirSenha.PlaceholderText"),
            PasswordRevealMode = PasswordRevealMode.Peek
        };

        var painel = new StackPanel { Spacing = 8 };
        painel.Children.Add(new TextBlock { Text = mensagem, TextWrapping = TextWrapping.Wrap });
        painel.Children.Add(senhaBox);

        var dialogo = new ContentDialog
        {
            Title = titulo,
            Content = painel,
            PrimaryButtonText = _localization.GetString("VaultPage_DialogContinuar.PrimaryButtonText"),
            CloseButtonText = _localization.GetString("VaultPage_DialogContinuar.CloseButtonText"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
            return null;

        return senhaBox.Password;
    }

    private async Task<(string? Senha, bool Substituir)> PedirDadosImportacaoAsync()
    {
        var senhaBox = new PasswordBox
        {
            PlaceholderText = _localization.GetString("VaultPage_PedirSenha.PlaceholderText"),
            PasswordRevealMode = PasswordRevealMode.Peek
        };
        var radioMesclar = new RadioButton { Content = _localization.GetString("VaultPage_RadioMesclar.Content"), IsChecked = true };
        var radioSubstituir = new RadioButton { Content = _localization.GetString("VaultPage_RadioSubstituir.Content"), IsChecked = false };

        var painel = new StackPanel { Spacing = 8 };
        painel.Children.Add(new TextBlock
        {
            Text = _localization.GetString("VaultPage_TextoImportar.Text"),
            TextWrapping = TextWrapping.Wrap
        });
        painel.Children.Add(senhaBox);
        painel.Children.Add(radioMesclar);
        painel.Children.Add(radioSubstituir);

        var dialogo = new ContentDialog
        {
            Title = _localization.GetString("VaultPage_DialogPedirSenha.Title"),
            Content = painel,
            PrimaryButtonText = _localization.GetString("VaultPage_DialogPedirSenha.PrimaryButtonText"),
            CloseButtonText = _localization.GetString("VaultPage_DialogPedirSenha.CloseButtonText"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
            return (null, false);

        return (senhaBox.Password, radioSubstituir.IsChecked == true);
    }

    private async Task MostrarErroAsync(string mensagem)
    {
        var dialogo = new ContentDialog
        {
            Title = _localization.GetString("VaultPage_DialogErro.Title"),
            Content = mensagem,
            CloseButtonText = _localization.GetString("VaultPage_DialogErro.CloseButtonText"),
            XamlRoot = XamlRoot
        };

        await dialogo.ShowAsync();
    }

    private async Task MostrarInfoAsync(string mensagem)
    {
        var dialogo = new ContentDialog
        {
            Title = _localization.GetString("VaultPage_DialogImportacaoConcluida.Title"),
            Content = mensagem,
            CloseButtonText = _localization.GetString("VaultPage_DialogImportacaoConcluida.CloseButtonText"),
            XamlRoot = XamlRoot
        };

        await dialogo.ShowAsync();
    }
}