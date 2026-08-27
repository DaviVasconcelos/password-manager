using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
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
        HookValidacaoObrigatoria(dialogo, editor);

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

    private void OnCopiarClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not VaultItem item)
            return;
        if (sender is not FrameworkElement elemento)
            return;

        var flyout = new MenuFlyout
        {
            Placement = FlyoutPlacementMode.BottomEdgeAlignedRight
        };

        var itemUsuario = new MenuFlyoutItem
        {
            Text = _localization.GetString("VaultPage_MenuCopiar.Usuario"),
            IsEnabled = !string.IsNullOrEmpty(item.Username)
        };
        itemUsuario.Click += (_, _) => ViewModel.CopiarUsuarioCommand.Execute(item);
        flyout.Items.Add(itemUsuario);

        var itemSenha = new MenuFlyoutItem
        {
            Text = _localization.GetString("VaultPage_MenuCopiar.Senha")
        };
        itemSenha.Click += (_, _) => ViewModel.CopiarSenhaCommand.Execute(item);
        flyout.Items.Add(itemSenha);

        flyout.ShowAt(elemento);
    }

    private void OnFecharToastClick(object sender, RoutedEventArgs e)
        => ViewModel.SenhaCopiada = false;

    private async void OnExcluirItemClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not VaultItem item)
            return;

        await ConfirmarExclusaoAsync(item);
    }

    private async Task ConfirmarExclusaoAsync(VaultItem item)
    {
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

    private void OnItemRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        // Ignorar clique direito originado nos botões de ação (editar/copiar/excluir)
        // para não conflitar com seus próprios handlers.
        if (e.OriginalSource is DependencyObject dep)
        {
            var cur = dep;
            while (cur != null)
            {
                if (cur is Button)
                    return;
                cur = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(cur);
            }
        }

        if ((sender as FrameworkElement)?.DataContext is not VaultItem item)
            return;

        ViewModel.ItemSelecionado = item;

        var flyout = new MenuFlyout
        {
            Placement = FlyoutPlacementMode.Right
        };

        var editar = new MenuFlyoutItem
        {
            Text = _localization.GetString("VaultPage_MenuContexto.Editar")
        };
        editar.Click += (_, _) => _ = EditarItemAsync(item);
        flyout.Items.Add(editar);

        flyout.Items.Add(new MenuFlyoutSeparator());

        var copiarUsuario = new MenuFlyoutItem
        {
            Text = _localization.GetString("VaultPage_MenuContexto.CopiarUsuario"),
            IsEnabled = !string.IsNullOrEmpty(item.Username)
        };
        copiarUsuario.Click += (_, _) => ViewModel.CopiarUsuarioCommand.Execute(item);
        flyout.Items.Add(copiarUsuario);

        var copiarSenha = new MenuFlyoutItem
        {
            Text = _localization.GetString("VaultPage_MenuContexto.CopiarSenha")
        };
        copiarSenha.Click += (_, _) => ViewModel.CopiarSenhaCommand.Execute(item);
        flyout.Items.Add(copiarSenha);

        flyout.Items.Add(new MenuFlyoutSeparator());

        var excluir = new MenuFlyoutItem
        {
            Text = _localization.GetString("VaultPage_MenuContexto.Excluir")
        };
        if (App.Current.Resources.TryGetValue("PMDangerBrush", out var brush) && brush is Microsoft.UI.Xaml.Media.SolidColorBrush scb)
            excluir.Foreground = scb;
        excluir.Click += async (_, _) => await ConfirmarExclusaoAsync(item);
        flyout.Items.Add(excluir);

        flyout.ShowAt((FrameworkElement)sender, e.GetPosition((FrameworkElement)sender));
    }

    private async void OnItemDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (EncontrarVaultItem(e.OriginalSource as DependencyObject) is VaultItem item)
            await EditarItemAsync(item);
    }

    private static VaultItem? EncontrarVaultItem(DependencyObject? origem)
    {
        var cur = origem;
        while (cur != null)
        {
            if (cur is FrameworkElement fe && fe.DataContext is VaultItem vi)
                return vi;
            cur = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(cur);
        }

        return null;
    }

    private void OnItemPointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Grid outer && EncontrarElemento<Border>(outer, "HoverBorder") is Border hover
            && App.Current.Resources.TryGetValue("PMSurfaceAltBrush", out var brush) && brush is Microsoft.UI.Xaml.Media.Brush b)
            hover.Background = b;
    }

    private void OnItemPointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Grid outer && EncontrarElemento<Border>(outer, "HoverBorder") is Border hover)
            hover.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }

    private static T? EncontrarElemento<T>(DependencyObject raiz, string nome) where T : FrameworkElement
    {
        if (raiz is T t && t.Name == nome)
            return t;

        int count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(raiz);
        for (int i = 0; i < count; i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(raiz, i);
            var encontrado = EncontrarElemento<T>(child, nome);
            if (encontrado != null)
                return encontrado;
        }

        return null;
    }

    private async Task EditarItemAsync(VaultItem item)
    {
        var editor = new ItemEditorContent();
        editor.ViewModel.CarregarParaEdicao(item, ViewModel.FolderOptions);

        var dialogo = CriarDialogo(_localization.GetString("VaultPage_DialogEditarItem.Title"), editor);
        HookValidacaoObrigatoria(dialogo, editor);

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

    /// <summary>
    /// Segura o diálogo aberto quando os campos obrigatórios estão em
    /// branco (a mensagem de erro é exibida dentro do editor).
    /// </summary>
    private static void HookValidacaoObrigatoria(ContentDialog dialogo, ItemEditorContent editor)
    {
        dialogo.PrimaryButtonClick += (_, args) =>
        {
            if (!editor.ValidarCamposObrigatorios())
                args.Cancel = true;
        };
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

        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
            return;

        ViewModel.AplicarConfiguracoes();

        if (content.ViewModel.RequerReinicio)
            await SolicitarReinicioIdiomaAsync();
    }

    private async Task SolicitarReinicioIdiomaAsync()
    {
        var confirmacao = new ContentDialog
        {
            Title = _localization.GetString("Settings_Idioma_Reiniciar_Title"),
            Content = _localization.GetString("Settings_Idioma_Reiniciar_Mensagem"),
            PrimaryButtonText = _localization.GetString("Settings_Idioma_Reiniciar_Agora"),
            CloseButtonText = _localization.GetString("Settings_Idioma_Reiniciar_Depois"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        if (await confirmacao.ShowAsync() != ContentDialogResult.Primary)
            return;

        try
        {
            Microsoft.Windows.AppLifecycle.AppInstance.Restart(string.Empty);
        }
        catch
        {
            Microsoft.UI.Xaml.Application.Current.Exit();
        }
    }

    /// <summary>
    /// Diálogo de troca de senha mestra em dois passos: coleta das senhas
    /// e, depois, confirmação explícita do usuário (o mesmo diálogo muda de
    /// conteúdo — só existe um ContentDialog por XamlRoot).
    /// </summary>
    private async void OnTrocarSenhaMestraClick(object sender, RoutedEventArgs e)
    {
        var senhaAtualBox = new PasswordBox
        {
            PlaceholderText = _localization.GetString("VaultPage_SenhaAtualBox.PlaceholderText")
        };
        var novaSenhaBox = new PasswordBox
        {
            PlaceholderText = _localization.GetString("VaultPage_NovaSenhaBox.PlaceholderText")
        };
        var confirmacaoBox = new PasswordBox
        {
            PlaceholderText = _localization.GetString("VaultPage_ConfirmacaoNovaSenhaBox.PlaceholderText")
        };
        var erro = new TextBlock
        {
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red),
            TextWrapping = TextWrapping.Wrap
        };

        var painelSenhas = new StackPanel { Spacing = 8 };
        painelSenhas.Children.Add(new TextBlock
        {
            Text = _localization.GetString("VaultPage_TextoTrocarSenha.Text"),
            TextWrapping = TextWrapping.Wrap
        });
        painelSenhas.Children.Add(CaixaSenhaComMostrar(senhaAtualBox));
        painelSenhas.Children.Add(CaixaSenhaComMostrar(novaSenhaBox));
        painelSenhas.Children.Add(CaixaSenhaComMostrar(confirmacaoBox));
        painelSenhas.Children.Add(erro);

        var painelConfirmacao = new StackPanel
        {
            Spacing = 8,
            Visibility = Visibility.Collapsed
        };
        painelConfirmacao.Children.Add(new TextBlock
        {
            Text = _localization.GetString("VaultPage_TextoConfirmarTroca.Text"),
            TextWrapping = TextWrapping.Wrap
        });
        var ocupado = new ProgressRing { IsActive = false };
        painelConfirmacao.Children.Add(ocupado);

        var raiz = new Grid();
        raiz.Children.Add(painelSenhas);
        raiz.Children.Add(painelConfirmacao);

        var dialogo = new ContentDialog
        {
            Title = _localization.GetString("VaultPage_DialogTrocarSenhaMestra.Title"),
            Content = raiz,
            PrimaryButtonText = _localization.GetString("VaultPage_DialogTrocarSenhaMestra.PrimaryButtonText"),
            CloseButtonText = _localization.GetString("VaultPage_DialogTrocarSenhaMestra.CloseButtonText"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        var textoBotaoAlterar = _localization.GetString("VaultPage_DialogTrocarSenhaMestra.PrimaryButtonText");
        var textoBotaoCancelar = _localization.GetString("VaultPage_DialogTrocarSenhaMestra.CloseButtonText");
        var textoBotaoConfirmar = _localization.GetString("VaultPage_DialogTrocarSenhaMestra.ConfirmarButtonText");
        var textoBotaoVoltar = _localization.GetString("VaultPage_DialogTrocarSenhaMestra.VoltarButtonText");
        var emConfirmacao = false;
        var senhaAlterada = false;

        void VoltarParaSenhas()
        {
            emConfirmacao = false;
            painelConfirmacao.Visibility = Visibility.Collapsed;
            painelSenhas.Visibility = Visibility.Visible;
            dialogo.PrimaryButtonText = textoBotaoAlterar;
            dialogo.CloseButtonText = textoBotaoCancelar;
        }

        dialogo.PrimaryButtonClick += async (_, args) =>
        {
            args.Cancel = true;

            if (!emConfirmacao)
            {
                if (string.IsNullOrWhiteSpace(senhaAtualBox.Password) ||
                    string.IsNullOrWhiteSpace(novaSenhaBox.Password) ||
                    string.IsNullOrWhiteSpace(confirmacaoBox.Password))
                {
                    erro.Text = _localization.GetString("VaultPage_Erro_CamposEmBranco");
                    return;
                }

                if (novaSenhaBox.Password != confirmacaoBox.Password)
                {
                    erro.Text = _localization.GetString("VaultPage_Erro_SenhasNaoConferem");
                    return;
                }

                erro.Text = string.Empty;
                emConfirmacao = true;
                painelSenhas.Visibility = Visibility.Collapsed;
                painelConfirmacao.Visibility = Visibility.Visible;
                dialogo.PrimaryButtonText = textoBotaoConfirmar;
                dialogo.CloseButtonText = textoBotaoVoltar;
                return;
            }

            // Captura as senhas na thread da UI: acessar PasswordBox.Password
            // de dentro do Task.Run lança WrongThreadException.
            var senhaAtual = senhaAtualBox.Password;
            var novaSenha = novaSenhaBox.Password;

            // Deferral mantém o diálogo modal e os botões desabilitados durante
            // a operação (derivação Argon2id + re-criptografia levam segundos).
            var deferral = args.GetDeferral();
            ocupado.IsActive = true;
            dialogo.IsPrimaryButtonEnabled = false;
            try
            {
                try
                {
                    await Task.Run(async () => await ViewModel.TrocarSenhaMestraAsync(
                        senhaAtual, novaSenha));
                }
                catch (CryptographicIntegrityException)
                {
                    VoltarParaSenhas();
                    erro.Text = _localization.GetString("VaultPage_Erro_SenhaAtualIncorreta");
                    return;
                }
                catch (Exception ex)
                {
                    VoltarParaSenhas();
                    erro.Text = ex.Message;
                    return;
                }

                erro.Text = string.Empty;
                senhaAlterada = true;
                dialogo.Hide();
            }
            finally
            {
                ocupado.IsActive = false;
                dialogo.IsPrimaryButtonEnabled = true;
                deferral.Complete();
            }
        };

        // "Voltar" no passo de confirmação retorna à coleta de senhas.
        dialogo.CloseButtonClick += (_, args) =>
        {
            if (!emConfirmacao)
                return;

            args.Cancel = true;
            erro.Text = string.Empty;
            VoltarParaSenhas();
        };

        await dialogo.ShowAsync();

        // Exibida só após o fechamento completo do diálogo: abrir outro
        // ContentDialog imediatamente após Hide() viola a regra de um diálogo
        // aberto por XamlRoot.
        if (senhaAlterada)
            await MostrarInfoAsync(_localization.GetString("VaultPage_Info_SenhaAlterada"));
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

    /// <summary>
    /// Cria uma PasswordBox com o botão de olho para alternar a visibilidade
    /// (padrão do projeto, como no editor de itens): o modo Peek é instável
    /// em diálogos construídos em código — o botão nativo some após o primeiro
    /// uso. Aqui a caixa fica em Hidden e a alternância é explícita.
    /// </summary>
    private FrameworkElement CaixaSenhaComMostrar(PasswordBox caixa)
    {
        caixa.PasswordRevealMode = PasswordRevealMode.Hidden;

        var botaoMostrar = new Button
        {
            Style = (Style)App.Current.Resources["PMIconButton"],
            Content = new FontIcon { Glyph = "\uE7B3", FontSize = 14 },
            VerticalAlignment = VerticalAlignment.Center
        };
        AutomationProperties.SetName(botaoMostrar, _localization.GetString("ItemEditor_BtnMostrarOcultar.AutomationProperties.Name"));
        botaoMostrar.Click += (_, _) =>
            caixa.PasswordRevealMode = caixa.PasswordRevealMode == PasswordRevealMode.Hidden
                ? PasswordRevealMode.Visible
                : PasswordRevealMode.Hidden;

        var grade = new Grid { ColumnSpacing = 8 };
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(caixa, 0);
        Grid.SetColumn(botaoMostrar, 1);
        grade.Children.Add(caixa);
        grade.Children.Add(botaoMostrar);
        return grade;
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
            ViewModel.NotificarExportacaoSucesso();
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
            ViewModel.NotificarImportacaoSucesso();
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
            PlaceholderText = _localization.GetString("VaultPage_PedirSenha.PlaceholderText")
        };

        var painel = new StackPanel { Spacing = 8 };
        painel.Children.Add(new TextBlock { Text = mensagem, TextWrapping = TextWrapping.Wrap });
        painel.Children.Add(CaixaSenhaComMostrar(senhaBox));

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
            PlaceholderText = _localization.GetString("VaultPage_PedirSenha.PlaceholderText")
        };
        var radioMesclar = new RadioButton { Content = _localization.GetString("VaultPage_RadioMesclar.Content"), IsChecked = true };
        var radioSubstituir = new RadioButton { Content = _localization.GetString("VaultPage_RadioSubstituir.Content"), IsChecked = false };

        var painel = new StackPanel { Spacing = 8 };
        painel.Children.Add(new TextBlock
        {
            Text = _localization.GetString("VaultPage_TextoImportar.Text"),
            TextWrapping = TextWrapping.Wrap
        });
        painel.Children.Add(CaixaSenhaComMostrar(senhaBox));
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