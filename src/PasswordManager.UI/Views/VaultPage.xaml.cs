using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using PasswordManager.Application.Exceptions;
using PasswordManager.Domain.Entities;
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

    public VaultPage()
    {
        ViewModel = App.Services.GetRequiredService<VaultViewModel>();
        InitializeComponent();
        ViewModel.Trancado += OnTrancado;

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

    private async void OnNovoItemClick(object sender, RoutedEventArgs e)
    {
        var editor = new ItemEditorContent();
        editor.ViewModel.CarregarParaCriacao(ViewModel.FolderOptions);

        var dialogo = CriarDialogo("Novo item", editor);
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

    private async void OnEditarItemClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.ItemSelecionado is not null)
            await EditarItemAsync(ViewModel.ItemSelecionado);
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

        var dialogo = CriarDialogo("Editar item", editor);
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

    private async void OnGerenciarPastasClick(object sender, RoutedEventArgs e)
    {
        var dialogo = new ContentDialog
        {
            Title = "Gerenciar pastas",
            Content = new GerenciarPastasContent(),
            PrimaryButtonText = "Concluir",
            XamlRoot = XamlRoot
        };

        await dialogo.ShowAsync();
        ViewModel.ReloadFolders();
    }

    private async void OnConfiguracoesClick(object sender, RoutedEventArgs e)
    {
        var content = new SettingsContent();
        content.ViewModel.Carregar();

        var dialogo = new ContentDialog
        {
            Title = "Configurações",
            Content = content,
            PrimaryButtonText = "Salvar",
            CloseButtonText = "Cancelar",
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
            PlaceholderText = "Senha mestra atual",
            PasswordRevealMode = PasswordRevealMode.Peek
        };
        var novaSenhaBox = new PasswordBox
        {
            PlaceholderText = "Nova senha mestra",
            PasswordRevealMode = PasswordRevealMode.Peek
        };
        var confirmacaoBox = new PasswordBox
        {
            PlaceholderText = "Confirme a nova senha",
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
            Text = "Digite a senha mestra atual e confirme a nova senha. O cofre será re-criptografado com um novo salt.",
            TextWrapping = TextWrapping.Wrap
        });
        painel.Children.Add(senhaAtualBox);
        painel.Children.Add(novaSenhaBox);
        painel.Children.Add(confirmacaoBox);
        painel.Children.Add(erro);

        var dialogo = new ContentDialog
        {
            Title = "Trocar senha mestra",
            Content = painel,
            PrimaryButtonText = "Alterar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        dialogo.PrimaryButtonClick += async (_, args) =>
        {
            if (novaSenhaBox.Password != confirmacaoBox.Password)
            {
                erro.Text = "As novas senhas não conferem.";
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
                erro.Text = "A senha mestra atual está incorreta.";
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
            PrimaryButtonText = "Salvar",
            CloseButtonText = "Cancelar",
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
            "Exportar cofre",
            "Digite a senha mestra para criptografar o arquivo de backup.");
        if (senha is null)
            return;

        try
        {
            var dados = await ViewModel.ExportarAsync(senha);
            await FileIO.WriteBytesAsync(arquivo, dados);
        }
        catch (Exception ex)
        {
            await MostrarErroAsync($"Falha ao exportar: {ex.Message}");
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
            await MostrarInfoAsync(dados.Substituir
                ? "Cofre substituído pelo conteúdo do arquivo."
                : "Cofre mesclado com o conteúdo do arquivo.");
        }
        catch (CryptographicIntegrityException)
        {
            await MostrarErroAsync("Senha mestra incorreta ou arquivo corrompido.");
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
        picker.FileTypeChoices.Add("Cofre PasswordManager (.vault)", new List<string> { ".vault" });
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
            PlaceholderText = "Senha mestra",
            PasswordRevealMode = PasswordRevealMode.Peek
        };

        var painel = new StackPanel { Spacing = 8 };
        painel.Children.Add(new TextBlock { Text = mensagem, TextWrapping = TextWrapping.Wrap });
        painel.Children.Add(senhaBox);

        var dialogo = new ContentDialog
        {
            Title = titulo,
            Content = painel,
            PrimaryButtonText = "Continuar",
            CloseButtonText = "Cancelar",
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
            PlaceholderText = "Senha mestra",
            PasswordRevealMode = PasswordRevealMode.Peek
        };
        var radioMesclar = new RadioButton { Content = "Mesclar com o cofre atual", IsChecked = true };
        var radioSubstituir = new RadioButton { Content = "Substituir o cofre atual", IsChecked = false };

        var painel = new StackPanel { Spacing = 8 };
        painel.Children.Add(new TextBlock
        {
            Text = "Digite a senha mestra usada para criptografar o arquivo e escolha como aplicar.",
            TextWrapping = TextWrapping.Wrap
        });
        painel.Children.Add(senhaBox);
        painel.Children.Add(radioMesclar);
        painel.Children.Add(radioSubstituir);

        var dialogo = new ContentDialog
        {
            Title = "Importar cofre",
            Content = painel,
            PrimaryButtonText = "Importar",
            CloseButtonText = "Cancelar",
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
            Title = "Erro",
            Content = mensagem,
            CloseButtonText = "OK",
            XamlRoot = XamlRoot
        };

        await dialogo.ShowAsync();
    }

    private async Task MostrarInfoAsync(string mensagem)
    {
        var dialogo = new ContentDialog
        {
            Title = "Importação concluída",
            Content = mensagem,
            CloseButtonText = "OK",
            XamlRoot = XamlRoot
        };

        await dialogo.ShowAsync();
    }
}