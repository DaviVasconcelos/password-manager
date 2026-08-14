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
public sealed record OpcaoPasta(string Nome, VaultFolder? Pasta);

/// <summary>
/// ViewModel da tela principal: lista de itens com busca e filtro por pasta,
/// operações de CRUD (com auto-save) e cópia de senha com limpeza automática.
/// </summary>
public partial class VaultViewModel : ObservableObject
{
    private const int TempoLimpezaClipboardSegundos = 30;

    private readonly IVaultSessionService _sessionService;
    private readonly DispatcherQueueTimer _timerLimparClipboard;

    public ObservableCollection<VaultItem> ItensExibidos { get; } = new();
    public ObservableCollection<OpcaoPasta> OpcoesPasta { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeEditarItem))]
    [NotifyCanExecuteChangedFor(nameof(RemoverItemCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopiarSenhaCommand))]
    private VaultItem? itemSelecionado;

    [ObservableProperty]
    private string? termoBusca;

    [ObservableProperty]
    private OpcaoPasta? opcaoPastaSelecionada;

    [ObservableProperty]
    private bool senhaCopiada;

    /// <summary>
    /// Disparado na thread da UI quando o cofre é trancado.
    /// </summary>
    public event Action? Trancado;

    public bool PodeEditarItem => ItemSelecionado is not null;

    public VaultViewModel(IVaultSessionService sessionService)
    {
        _sessionService = sessionService;
        _timerLimparClipboard = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _timerLimparClipboard.Interval = TimeSpan.FromSeconds(TempoLimpezaClipboardSegundos);
        _timerLimparClipboard.Tick += OnTimerLimparClipboardTick;
    }

    partial void OnTermoBuscaChanged(string? value) => AplicarFiltro();

    partial void OnOpcaoPastaSelecionadaChanged(OpcaoPasta? value) => AplicarFiltro();

    public void Inicializar() => AtualizarPastas();

    /// <summary>
    /// Reconstrói as opções de pasta (mantendo a seleção quando possível)
    /// e reaplica o filtro.
    /// </summary>
    public void AtualizarPastas()
    {
        var selecionada = OpcaoPastaSelecionada?.Pasta?.Id;

        OpcoesPasta.Clear();
        OpcoesPasta.Add(new OpcaoPasta("Todas as pastas", null));

        foreach (var pasta in _sessionService.VaultAtual.Folders)
            OpcoesPasta.Add(new OpcaoPasta(pasta.Name, pasta));

        OpcaoPastaSelecionada = selecionada is null
            ? OpcoesPasta.First()
            : OpcoesPasta.FirstOrDefault(o => o.Pasta?.Id == selecionada) ?? OpcoesPasta.First();

        AplicarFiltro();
    }

    private void AplicarFiltro()
    {
        var pastaId = OpcaoPastaSelecionada?.Pasta?.Id;
        var itens = _sessionService.BuscarItens(TermoBusca, pastaId);

        ItensExibidos.Clear();
        foreach (var item in itens)
            ItensExibidos.Add(item);
    }

    public async Task AdicionarItemAsync(string title, string password, string category,
        string? username = null, string? url = null, string? notes = null, Guid? pastaId = null)
    {
        var item = await _sessionService.AdicionarItemAsync(title, password, category, username, url, notes);

        if (pastaId is not null)
            await _sessionService.AtribuirItemAPastaAsync(item.Id, pastaId);

        AplicarFiltro();
    }

    public async Task AtualizarItemAsync(Guid itemId, string title, string password, string category,
        string? username = null, string? url = null, string? notes = null, Guid? pastaId = null)
    {
        await _sessionService.AtualizarItemAsync(itemId, title, password, category, username, url, notes);
        await _sessionService.AtribuirItemAPastaAsync(itemId, pastaId);
        AplicarFiltro();
    }

    public async Task AdicionarPastaAsync(string name)
    {
        await _sessionService.AdicionarPastaAsync(name);
        AtualizarPastas();
    }

    public async Task RenomearPastaAsync(Guid folderId, string name)
    {
        await _sessionService.RenomearPastaAsync(folderId, name);
        AtualizarPastas();
    }

    public async Task RemoverPastaAsync(Guid folderId)
    {
        await _sessionService.RemoverPastaAsync(folderId);
        AtualizarPastas();
    }

    [RelayCommand]
    private async Task RemoverItemAsync()
    {
        if (ItemSelecionado is null)
            return;

        await _sessionService.RemoverItemAsync(ItemSelecionado.Id);
        ItemSelecionado = null;
        AplicarFiltro();
    }

    private bool CanRemoverItem() => ItemSelecionado is not null;

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

    private bool CanCopiarSenha() => ItemSelecionado is not null;

    [RelayCommand]
    private void Trancar()
    {
        _timerLimparClipboard.Stop();
        _sessionService.Trancar();
        SenhaCopiada = false;
        Trancado?.Invoke();
    }

    private void OnTimerLimparClipboardTick(DispatcherQueueTimer sender, object args)
    {
        var pacote = new DataPackage();
        pacote.SetText(string.Empty);
        Clipboard.SetContent(pacote);
        SenhaCopiada = false;
        _timerLimparClipboard.Stop();
    }
}