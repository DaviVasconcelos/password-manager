using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using PasswordManager.Domain.Entities;
using PasswordManager.UI.ViewModels;
using System;
using System.Threading.Tasks;

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
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.Inicializar();
    }

    private void OnTrancado()
    {
        Frame.Navigate(typeof(UnlockPage));
    }

    private async void OnNovoItemClick(object sender, RoutedEventArgs e)
    {
        var editor = new ItemEditorContent();
        editor.ViewModel.CarregarParaCriacao(ViewModel.OpcoesPasta);

        var dialogo = CriarDialogo("Novo item", editor);
        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
            return;

        await ViewModel.AdicionarItemAsync(
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
        editor.ViewModel.CarregarParaEdicao(item, ViewModel.OpcoesPasta);

        var dialogo = CriarDialogo("Editar item", editor);
        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
            return;

        await ViewModel.AtualizarItemAsync(
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
        ViewModel.AtualizarPastas();
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
}