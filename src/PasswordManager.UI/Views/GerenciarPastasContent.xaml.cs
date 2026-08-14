using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PasswordManager.Application.VaultSession;
using PasswordManager.Domain.Entities;
using System.Linq;

namespace PasswordManager.UI.Views;

/// <summary>
/// Conteúdo do diálogo de gerenciamento de pastas: adicionar, renomear e
/// excluir pastas (a exclusão não apaga os itens — eles ficam sem pasta).
/// </summary>
public sealed partial class GerenciarPastasContent : UserControl
{
    private readonly IVaultSessionService _sessionService;

    public GerenciarPastasContent()
    {
        InitializeComponent();
        _sessionService = App.Services.GetRequiredService<IVaultSessionService>();
        RecarregarPastas();
    }

    private void RecarregarPastas()
    {
        PastasList.ItemsSource = _sessionService.VaultAtual.Folders.ToList();
        PastasList.SelectedItem = null;
    }

    private async void OnAdicionarClick(object sender, RoutedEventArgs e)
    {
        var nome = NomePastaBox.Text.Trim();
        if (string.IsNullOrEmpty(nome))
            return;

        await _sessionService.AdicionarPastaAsync(nome);
        NomePastaBox.Text = string.Empty;
        RecarregarPastas();
    }

    private async void OnRenomearClick(object sender, RoutedEventArgs e)
    {
        if (PastasList.SelectedItem is not VaultFolder pasta)
            return;

        var novoNome = NomePastaBox.Text.Trim();
        if (string.IsNullOrEmpty(novoNome))
            return;

        await _sessionService.RenomearPastaAsync(pasta.Id, novoNome);
        RecarregarPastas();
    }

    private async void OnExcluirClick(object sender, RoutedEventArgs e)
    {
        if (PastasList.SelectedItem is not VaultFolder pasta)
            return;

        await _sessionService.RemoverPastaAsync(pasta.Id);
        RecarregarPastas();
    }
}