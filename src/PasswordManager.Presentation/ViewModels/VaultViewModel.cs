using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordManager.Application.Settings;
using PasswordManager.Application.VaultSession;
using PasswordManager.Domain.Entities;
using PasswordManager.UI.Localization;
using PasswordManager.UI.Services;
using ITimer = PasswordManager.UI.Services.ITimer;
using ITimerFactory = PasswordManager.UI.Services.ITimerFactory;

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
    private readonly IVaultSessionService _sessionService;
    private readonly IAppSettingsService _settingsService;
    private readonly ILocalizationService _localization;
    private readonly IClipboardService _clipboardService;
    private readonly ITimer _timerLimparClipboard;
    private readonly ITimer _timerInatividade;
    private readonly ITimer _timerInfoBanner;

    private static readonly TimeSpan DuracaoInfoBanner = TimeSpan.FromSeconds(4);

    private TimeSpan _timeoutInatividade = TimeSpan.FromMinutes(2);
    private TimeSpan _tempoLimparClipboard = TimeSpan.FromSeconds(30);

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

    [ObservableProperty]
    private bool infoBannerVisivel;

    [ObservableProperty]
    private string? textoInfoBanner;

    /// <summary>
    /// Disparado na thread da UI quando o cofre é trancado.
    /// </summary>
    public event Action? Trancado;

    public bool CanEditItem => ItemSelecionado is not null;

    public VaultViewModel(
        IVaultSessionService sessionService,
        IAppSettingsService settingsService,
        ILocalizationService localization,
        ITimerFactory timerFactory,
        IClipboardService clipboardService)
    {
        _sessionService = sessionService;
        _settingsService = settingsService;
        _localization = localization;
        _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
        if (timerFactory is null) throw new ArgumentNullException(nameof(timerFactory));

        _timerLimparClipboard = timerFactory.Create();
        _timerLimparClipboard.Tick += OnTimerCleanClipboardTick;
        _timerInatividade = timerFactory.Create();
        _timerInatividade.Tick += OnTimerInatividadeTick;
        _timerInfoBanner = timerFactory.Create();
        _timerInfoBanner.Tick += OnTimerInfoBannerTick;
    }

    partial void OnTermoBuscaChanged(string? value) => AddFilter();

    partial void OnOpcaoPastaSelecionadaChanged(OpcoesPasta? value) => AddFilter();

    /// <summary>
    /// Nome do cofre ativo (arquivo selecionado na UnlockPage).
    /// </summary>
    public string NomeCofreAtivo => _sessionService.CofreAtivo?.Nome ?? string.Empty;

    public void Inicializar()
    {
        AplicarConfiguracoes();
        ReloadFolders();
        OnPropertyChanged(nameof(NomeCofreAtivo));
    }

    /// <summary>
    /// Aplica as configurações persistidas (timeouts de auto-lock e de
    /// limpeza do clipboard) e reinicia o timer de inatividade.
    /// </summary>
    public void AplicarConfiguracoes()
    {
        var settings = _settingsService.Get();
        _timeoutInatividade = TimeSpan.FromMinutes(settings.AutoLockTimeoutMinutes);
        _tempoLimparClipboard = TimeSpan.FromSeconds(settings.ClipboardCleanTimeSeconds);
        OnPropertyChanged(nameof(TextoToastSenhaCopiada));
        ReiniciarTimerInatividade();
    }

    /// <summary>
    /// Texto do banner "senha copiada" com o tempo configurado para limpeza
    /// da área de transferência.
    /// </summary>
    public string TextoToastSenhaCopiada =>
        string.Format(_localization.GetString("VaultPage_ToastSenhaCopiada.Text"), (int)_tempoLimparClipboard.TotalSeconds);

    /// <summary>
    /// Registra atividade do usuário, reiniciando o timer de inatividade.
    /// </summary>
    public void NotificarAtividade() => ReiniciarTimerInatividade();

    /// <summary>
    /// Para os timers da página (usado ao navegar para fora do cofre).
    /// </summary>
    public void PararTimers()
    {
        _timerLimparClipboard.Stop();
        _timerInatividade.Stop();
        _timerInfoBanner.Stop();
    }

    /// <summary>
    /// Reconstrói as opções de pasta (mantendo a seleção quando possível)
    /// e reaplica o filtro.
    /// </summary>
    public void ReloadFolders()
    {
        if (!_sessionService.Unlocked)
            return;

        var selected = OpcaoPastaSelecionada?.Pasta?.Id;

        FolderOptions.Clear();
        FolderOptions.Add(new OpcoesPasta(_localization.GetString("VaultViewModel_TodasPastas"), null));

        foreach (var pasta in _sessionService.CurrentVault.Folders)
            FolderOptions.Add(new OpcoesPasta(pasta.Name, pasta));

        OpcaoPastaSelecionada = selected is null
            ? FolderOptions.First()
            : FolderOptions.FirstOrDefault(o => o.Pasta?.Id == selected) ?? FolderOptions.First();

        AddFilter();
    }

    private void AddFilter(bool forcarAtualizacao = false)
    {
        if (!_sessionService.Unlocked)
        {
            DisplayedItems.Clear();
            return;
        }

        var folderId = OpcaoPastaSelecionada?.Pasta?.Id;
        var items = _sessionService.SearchItems(TermoBusca, folderId);

        // Evita o recarregamento visual (limpar + re-adicionar com animação)
        // quando o resultado da busca não mudou — ex.: ao fechar o diálogo
        // de pastas sem alterações.
        // Para edição, forçar atualização: SequenceEqual por referência não detecta mutação do item.
        if (!forcarAtualizacao && DisplayedItems.SequenceEqual(items))
            return;

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
        AddFilter(forcarAtualizacao: true);
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
    private async Task RemoverItemAsync(VaultItem? item)
    {
        if (item is null)
            return;

        await _sessionService.RemoveItemAsync(item.Id);
        if (ItemSelecionado?.Id == item.Id)
            ItemSelecionado = null;
        AddFilter();
    }

    [RelayCommand]
    private void CopiarSenha(VaultItem? item)
    {
        if (item is null)
            return;

        _clipboardService.SetText(item.Password);

        SenhaCopiada = true;
        _timerLimparClipboard.Stop();
        _timerLimparClipboard.Interval = _tempoLimparClipboard;
        _timerLimparClipboard.Start();
    }

    /// <summary>
    /// Copia apenas o usuário para a área de transferência: sem toast e sem
    /// limpeza automática (dado menos sensível que a senha).
    /// </summary>
    [RelayCommand]
    private void CopiarUsuario(VaultItem? item)
    {
        if (item is null || string.IsNullOrEmpty(item.Username))
            return;

        _clipboardService.SetText(item.Username);
    }

    [RelayCommand]
    private void Lock() => Trancar();

    /// <summary>
    /// Tranca a sessão, zera a flag de senha copiada e notifica a UI.
    /// </summary>
    private void Trancar()
    {
        _timerLimparClipboard.Stop();
        _timerInatividade.Stop();
        _timerInfoBanner.Stop();
        _sessionService.Lock();
        SenhaCopiada = false;
        InfoBannerVisivel = false;
        Trancado?.Invoke();
    }

    /// <summary>
    /// Exibe um banner informativo temporário (export/import) no mesmo estilo
    /// do banner "senha copiada": auto-oculta em 4 s e pode ser fechado manualmente.
    /// </summary>
    public void MostrarInfoBanner(string texto)
    {
        TextoInfoBanner = texto;
        InfoBannerVisivel = true;
        _timerInfoBanner.Stop();
        _timerInfoBanner.Interval = DuracaoInfoBanner;
        _timerInfoBanner.Start();
    }

    public void NotificarExportacaoSucesso() =>
        MostrarInfoBanner(_localization.GetString("VaultPage_ToastCofreExportado.Text"));

    public void NotificarImportacaoSucesso() =>
        MostrarInfoBanner(_localization.GetString("VaultPage_ToastCofreImportado.Text"));

    [RelayCommand]
    private void FecharInfoBanner()
    {
        _timerInfoBanner.Stop();
        InfoBannerVisivel = false;
    }

    private void ReiniciarTimerInatividade()
    {
        _timerInatividade.Stop();
        _timerInatividade.Interval = _timeoutInatividade;
        _timerInatividade.Start();
    }

    private void OnTimerInatividadeTick(object? sender, object args) => Trancar();

    /// <summary>
    /// Troca a senha mestra exigindo a senha atual (verificada pelo serviço
    /// de sessão por derivação de chave).
    /// </summary>
    public Task TrocarSenhaMestraAsync(string senhaAtual, string novaSenhaMestra)
        => _sessionService.ChangeMasterPasswordAsync(senhaAtual, novaSenhaMestra);

    /// <summary>
    /// Renomeia o cofre ativo (sem exigir desbloqueio adicional além da sessão atual).
    /// </summary>
    public async Task RenomearCofreAsync(string novoNome)
    {
        var ativo = _sessionService.CofreAtivo ?? throw new InvalidOperationException("Nenhum cofre ativo.");
        await _sessionService.RenomearCofreAsync(ativo.Id, novoNome);
        OnPropertyChanged(nameof(NomeCofreAtivo));
    }

    /// <summary>
    /// Exclui o cofre ativo e tranca a sessão (navega para UnlockPage).
    /// </summary>
    public async Task ExcluirCofreAtivoAsync()
    {
        var ativo = _sessionService.CofreAtivo ?? throw new InvalidOperationException("Nenhum cofre ativo.");
        await _sessionService.ExcluirCofreAsync(ativo.Id);
        Trancar();
    }

    private void OnTimerCleanClipboardTick(object? sender, object args)
    {
        _clipboardService.Clear();
        SenhaCopiada = false;
        _timerLimparClipboard.Stop();
    }

    private void OnTimerInfoBannerTick(object? sender, object args)
    {
        _timerInfoBanner.Stop();
        InfoBannerVisivel = false;
    }
}