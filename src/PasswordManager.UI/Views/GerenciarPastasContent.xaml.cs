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
        ReloadFolders();
    }

    private void ReloadFolders()
    {
        PastasList.ItemsSource = _sessionService.CurrentVault.Folders.ToList();
        PastasList.SelectedItem = null;
    }

    private async void OnAddClick(object sender, RoutedEventArgs e)
    {
        var name = FolderBoxName.Text.Trim();
        if (string.IsNullOrEmpty(name))
            return;

        await _sessionService.AddFolderAsync(name);
        FolderBoxName.Text = string.Empty;
        ReloadFolders();
    }

    private async void OnRenameClick(object sender, RoutedEventArgs e)
    {
        if (PastasList.SelectedItem is not VaultFolder pasta)
            return;

        var newName = FolderBoxName.Text.Trim();
        if (string.IsNullOrEmpty(newName))
            return;

        await _sessionService.RenameFolderAsync(pasta.Id, newName);
        ReloadFolders();
    }

    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (PastasList.SelectedItem is not VaultFolder folder)
            return;

        await _sessionService.RemoveFolderAsync(folder.Id);
        ReloadFolders();
    }
}