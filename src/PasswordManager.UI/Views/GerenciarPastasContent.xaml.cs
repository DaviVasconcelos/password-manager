using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PasswordManager.Application.VaultSession;
using PasswordManager.Domain.Entities;
using PasswordManager.UI.Localization;
using System;
using System.Linq;

namespace PasswordManager.UI.Views;

/// <summary>
/// Conteúdo do diálogo de gerenciamento de pastas: adicionar, renomear
/// (mini-diálogo) e excluir pastas (a exclusão não apaga os itens — eles
/// ficam sem pasta).
/// </summary>
public sealed partial class GerenciarPastasContent : UserControl
{
    private readonly IVaultSessionService _sessionService;
    private readonly ILocalizationService _localization;

    public GerenciarPastasContent()
    {
        InitializeComponent();
        _sessionService = App.Services.GetRequiredService<IVaultSessionService>();
        _localization = App.Services.GetRequiredService<ILocalizationService>();
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

    private async void OnRenameFolderClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not VaultFolder pasta)
            return;

        var nomeBox = new TextBox
        {
            Text = pasta.Name,
            PlaceholderText = _localization.GetString("GerenciarPastas_Renomear.PlaceholderText")
        };

        var dialogo = new ContentDialog
        {
            Title = _localization.GetString("GerenciarPastas_DialogRenomear.Title"),
            Content = nomeBox,
            PrimaryButtonText = _localization.GetString("GerenciarPastas_DialogRenomear.PrimaryButtonText"),
            CloseButtonText = _localization.GetString("GerenciarPastas_DialogRenomear.CloseButtonText"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
            return;

        var newName = nomeBox.Text.Trim();
        if (string.IsNullOrEmpty(newName))
            return;

        await _sessionService.RenameFolderAsync(pasta.Id, newName);
        ReloadFolders();
    }

    private async void OnDeleteFolderClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not VaultFolder folder)
            return;

        var dialogo = new ContentDialog
        {
            Title = _localization.GetString("GerenciarPastas_DialogExcluir.Title"),
            Content = string.Format(_localization.GetString("GerenciarPastas_DialogExcluir.Mensagem"), folder.Name),
            PrimaryButtonText = _localization.GetString("GerenciarPastas_DialogExcluir.PrimaryButtonText"),
            CloseButtonText = _localization.GetString("GerenciarPastas_DialogExcluir.CloseButtonText"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
            return;

        await _sessionService.RemoveFolderAsync(folder.Id);
        ReloadFolders();
    }
}
