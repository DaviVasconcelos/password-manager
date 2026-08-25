using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using PasswordManager.Application.VaultSession;
using PasswordManager.Domain.Entities;
using PasswordManager.UI.Localization;
using System;
using System.Linq;

namespace PasswordManager.UI.Views;

/// <summary>
/// Conteúdo do diálogo de gerenciamento de pastas: adicionar, renomear
/// (flyout inline) e excluir pastas (a exclusão não apaga os itens — eles
/// ficam sem pasta). Usa Flyout em vez de ContentDialog para as interações
/// por linha: só existe um ContentDialog por XamlRoot, e este diálogo já
/// está aberto.
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

    private void OnRenameFolderClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement botao || botao.DataContext is not VaultFolder pasta)
            return;

        var nomeBox = new TextBox
        {
            Text = pasta.Name,
            PlaceholderText = _localization.GetString("GerenciarPastas_Renomear.PlaceholderText"),
            MinWidth = 220
        };

        var salvar = new Button
        {
            Content = _localization.GetString("GerenciarPastas_DialogRenomear.PrimaryButtonText"),
            Style = (Style)App.Current.Resources["PMButtonPrimary"],
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var conteudo = new StackPanel { Spacing = 8, MinWidth = 240 };
        conteudo.Children.Add(nomeBox);
        conteudo.Children.Add(salvar);

        var flyout = new Flyout { Content = conteudo, Placement = FlyoutPlacementMode.Bottom };

        async void Confirmar()
        {
            var newName = nomeBox.Text.Trim();
            flyout.Hide();
            if (string.IsNullOrEmpty(newName))
                return;

            await _sessionService.RenameFolderAsync(pasta.Id, newName);
            ReloadFolders();
        }

        salvar.Click += (_, _) => Confirmar();
        nomeBox.KeyDown += (_, args) =>
        {
            if (args.Key == Windows.System.VirtualKey.Enter)
            {
                args.Handled = true;
                Confirmar();
            }
        };

        flyout.ShowAt(botao);
        nomeBox.Focus(FocusState.Keyboard);
        nomeBox.SelectAll();
    }

    private void OnDeleteFolderClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement botao || botao.DataContext is not VaultFolder folder)
            return;

        var texto = new TextBlock
        {
            Text = string.Format(_localization.GetString("GerenciarPastas_DialogExcluir.Mensagem"), folder.Name),
            TextWrapping = TextWrapping.Wrap
        };

        var cancelar = new Button
        {
            Content = _localization.GetString("GerenciarPastas_DialogExcluir.CloseButtonText"),
            Style = (Style)App.Current.Resources["PMButtonSecondary"]
        };

        var excluir = new Button
        {
            Content = _localization.GetString("GerenciarPastas_DialogExcluir.PrimaryButtonText"),
            Style = (Style)App.Current.Resources["PMButtonPrimary"]
        };

        var botoes = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        botoes.Children.Add(cancelar);
        botoes.Children.Add(excluir);

        var conteudo = new StackPanel { Spacing = 8, MinWidth = 240 };
        conteudo.Children.Add(texto);
        conteudo.Children.Add(botoes);

        var flyout = new Flyout { Content = conteudo, Placement = FlyoutPlacementMode.Bottom };

        excluir.Click += async (_, _) =>
        {
            flyout.Hide();
            await _sessionService.RemoveFolderAsync(folder.Id);
            ReloadFolders();
        };
        cancelar.Click += (_, _) => flyout.Hide();

        flyout.ShowAt(botao);
    }
}
