using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using PasswordManager.Application.VaultSession;
using PasswordManager.Domain.Entities;
using Windows.ApplicationModel.DataTransfer;

namespace PasswordManager.UI.ViewModels;

/// <summary>
/// Representa uma opção de pasta (ou "todas as pastas") em filtros/combos.
/// </summary>
public sealed record OpcoesPasta(string Nome, VaultFolder? Pasta);

/// <summary>
/// ViewModel da tela principal: lista de itens com busca e filtro por pasta,
/// operações de CRUD (com auto-save) e cópia de senha com limpeza automática.
/// </summary>
public partial class VaultViewModel : ObservableObject
{
    private const int ClipboardCleanTimeInSeconds = 30;

    private readonly IVaultSessionService _sessionService;
    private readonly DispatcherQueueTimer _timerLimparClipboard;

    public ObservableCollection<VaultItem> DisplayedItems { get; } = new();
    public ObservableCollection<OpcoesPasta> FolderOptions { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditItem))]
    [NotifyCanExecuteChangedFor(nameof(RemoverItemCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopiarSenhaCommand))]
    private VaultItem? itemSelecionado;

    [ObservableProperty]
    private string? termoBusca;

    [ObservableProperty]
    private OpcoesPasta? opcaoPastaSelecionada;

    [ObservableProperty]
    private bool senhaCopiada;

    /// <summary>
    /// Disparado na thread da UI quando o cofre é trancado.
    /// </summary>
    public event Action? Trancado;

    public bool CanEditItem => ItemSelecionado is not null;

    public VaultViewModel(IVaultSessionService sessionService)
    {
        _sessionService = sessionService;
        _timerLimparClipboard = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _timerLimparClipboard.Interval = TimeSpan.FromSeconds(ClipboardCleanTimeInSeconds);
        _timerLimparClipboard.Tick += OnTimerCleanClipboardTick;
    }

    partial void OnTermoBuscaChanged(string? value) => AddFilter();

    partial void OnOpcaoPastaSelecionadaChanged(OpcoesPasta? value) => AddFilter();

    public void Inicializar() => ReloadFolders();

    /// <summary>
    /// Reconstrói as opções de pasta (mantendo a seleção quando possível)
    /// e reaplica o filtro.
    /// </summary>
    public void ReloadFolders()
    {
        var selected = OpcaoPastaSelecionada?.Pasta?.Id;

        FolderOptions.Clear();
        FolderOptions.Add(new OpcoesPasta("Todas as pastas", null));

        foreach (var pasta in _sessionService.CurrentVault.Folders)
            FolderOptions.Add(new OpcoesPasta(pasta.Name, pasta));

        OpcaoPastaSelecionada = selected is null
            ? FolderOptions.First()
            : FolderOptions.FirstOrDefault(o => o.Pasta?.Id == selected) ?? FolderOptions.First();

        AddFilter();
    }

    private void AddFilter()
    {
        var folderId = OpcaoPastaSelecionada?.Pasta?.Id;
        var items = _sessionService.SearchItems(TermoBusca, folderId);

        DisplayedItems.Clear();
        foreach (var item in items)
            DisplayedItems.Add(item);
    }

    public async Task AddItemAsync(string title, string password, string category,
        string? username = null, string? url = null, string? notes = null, Guid? pastaId = null)
    {
        var item = await _sessionService.AddItemAsync(title, password, category, username, url, notes);

        if (pastaId is not null)
            await _sessionService.AssignItemToFolderAsync(item.Id, pastaId);

        AddFilter();
    }

    public async Task ReloadItemAsync(Guid itemId, string title, string password, string category,
        string? username = null, string? url = null, string? notes = null, Guid? pastaId = null)
    {
        await _sessionService.ReloadItemAsync(itemId, title, password, category, username, url, notes);
        await _sessionService.AssignItemToFolderAsync(itemId, pastaId);
        AddFilter();
    }

    public async Task AdicionarPastaAsync(string name)
    {
        await _sessionService.AddFolderAsync(name);
        ReloadFolders();
    }

    public async Task RenomearPastaAsync(Guid folderId, string name)
    {
        await _sessionService.RenameFolderAsync(folderId, name);
        ReloadFolders();
    }

    public async Task RemoverPastaAsync(Guid folderId)
    {
        await _sessionService.RemoveFolderAsync(folderId);
        ReloadFolders();
    }

    /// <summary>
    /// Serializa o cofre atual para bytes no formato .vault usando a senha
    /// mestra re-digitada pelo usuário. A UI é responsável por gravar os
    /// bytes no arquivo escolhido.
    /// </summary>
    public Task<byte[]> ExportarAsync(string masterPassword)
        => _sessionService.ExportAsync(masterPassword);

    /// <summary>
    /// Importa um arquivo .vault, substituindo ou mesclando com o cofre
    /// atual, e atualiza a lista e as pastas exibidas.
    /// </summary>
    public async Task ImportarAsync(byte[] fileData, string masterPassword, bool substituir)
    {
        await _sessionService.ImportAsync(fileData, masterPassword, substituir);
        ReloadFolders();
    }

    [RelayCommand]
    private async Task RemoverItemAsync()
    {
        if (ItemSelecionado is null)
            return;

        await _sessionService.RemoveItemAsync(ItemSelecionado.Id);
        ItemSelecionado = null;
        AddFilter();
    }

    private bool CanRemoveItem() => ItemSelecionado is not null;

    [RelayCommand]
    private void CopiarSenha()
    {
        if (ItemSelecionado is null)
            return;

        var pacote = new DataPackage();
        pacote.SetText(ItemSelecionado.Password);
        Clipboard.SetContent(pacote);

        SenhaCopiada = true;
        _timerLimparClipboard.Start();
    }

    private bool CanCopyPassword() => ItemSelecionado is not null;

    [RelayCommand]
    private void Lock()
    {
        _timerLimparClipboard.Stop();
        _sessionService.Lock();
        SenhaCopiada = false;
        Trancado?.Invoke();
    }

    private void OnTimerCleanClipboardTick(DispatcherQueueTimer sender, object args)
    {
        var package = new DataPackage();
        package.SetText(string.Empty);
        Clipboard.SetContent(package);
        SenhaCopiada = false;
        _timerLimparClipboard.Stop();
    }
}