using System.Security.Cryptography;
using PasswordManager.Application.Abstractions;
using PasswordManager.Domain.Entities;

namespace PasswordManager.Application.VaultSession;

/// <summary>
/// Implementa o <see cref="IVaultSessionService"/>. O serviço é o único ponto
/// que conhece a senha mestra na Application, usando-a apenas para derivar a
/// chave no desbloqueio/criação e descartando-a em seguida: a sessão retém
/// somente a chave derivada (e o <see cref="Vault"/> carregado), zerando-a
/// ao trancar.
/// </summary>
public sealed class VaultSessionService : IVaultSessionService
{
    private readonly IVaultRepository _vaultRepository;
    private readonly ICryptoService _cryptoService;
    private readonly IExportImportService _exportImportService;

    private byte[]? _chave;
    private Vault? _vault;

    public VaultSessionService(
        IVaultRepository vaultRepository,
        ICryptoService cryptoService,
        IExportImportService exportImportService)
    {
        _vaultRepository = vaultRepository;
        _cryptoService = cryptoService;
        _exportImportService = exportImportService;
    }

    public bool Unlocked => _vault is not null;

    public Vault CurrentVault =>
        _vault ?? throw new InvalidOperationException("A sessão está trancada; desbloqueie o cofre antes de acessá-lo.");

    public async Task<bool> VaultExistsAsync(CancellationToken ct = default)
        => await _vaultRepository.ExistsAsync(ct).ConfigureAwait(false);

    public async Task<Vault> CreateAsync(string senhaMestra, CancellationToken ct = default)
    {
        ValidarSenhaMestra(senhaMestra);

        if (Unlocked)
            throw new InvalidOperationException("A sessão já está desbloqueada; tranque o cofre antes de criar outro.");

        if (await _vaultRepository.ExistsAsync(ct).ConfigureAwait(false))
            throw new InvalidOperationException("Já existe um cofre persistido nesta instalação; use DesbloquearAsync.");

        var salt = _cryptoService.GenerateSalt();
        var chave = _cryptoService.DeriveKey(senhaMestra, salt);
        var vault = Vault.CreateNew();

        await _vaultRepository.CreateAsync(vault, chave, salt, ct).ConfigureAwait(false);

        DefinirSessao(chave, vault);
        return vault;
    }

    public async Task<Vault> UnlockAsync(string senhaMestra, CancellationToken ct = default)
    {
        ValidarSenhaMestra(senhaMestra);

        if (Unlocked)
            throw new InvalidOperationException("A sessão já está desbloqueada; tranque o cofre antes de desbloquear novamente.");

        var salt = await _vaultRepository.GetSaltAsync(ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Não há cofre persistido; use CriarAsync.");

        var chave = _cryptoService.DeriveKey(senhaMestra, salt);
        var vault = await _vaultRepository.LoadAsync(chave, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Não há cofre persistido; use CriarAsync.");

        DefinirSessao(chave, vault);
        return vault;
    }

    public void Lock()
    {
        if (_chave is not null)
        {
            CryptographicOperations.ZeroMemory(_chave);
            _chave = null;
        }

        _vault = null;
    }

    public async Task ChangeMasterPasswordAsync(string novaSenhaMestra, CancellationToken ct = default)
    {
        ValidarSenhaMestra(novaSenhaMestra);

        var vault = CurrentVault;
        var novoSalt = _cryptoService.GenerateSalt();
        var novaChave = _cryptoService.DeriveKey(novaSenhaMestra, novoSalt);

        await _vaultRepository.ChangeMasterPasswordAsync(vault, novaChave, novoSalt, ct).ConfigureAwait(false);

        SubstituirChave(novaChave);
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        var vault = CurrentVault;
        var chave = _chave ?? throw new InvalidOperationException("A sessão está trancada; desbloqueie o cofre antes.");

        await _vaultRepository.SaveAsync(vault, chave, ct).ConfigureAwait(false);
    }

    public async Task<VaultItem> AddItemAsync(string title, string password, string category,
        string? username = null, string? url = null, string? notes = null, CancellationToken ct = default)
    {
        var item = CurrentVault.AddItem(title, password, category, username, url, notes);
        await SaveAsync(ct).ConfigureAwait(false);
        return item;
    }

    public async Task ReloadItemAsync(Guid itemId, string title, string password, string category,
        string? username = null, string? url = null, string? notes = null, CancellationToken ct = default)
    {
        CurrentVault.UpdateItem(itemId, title, password, category, username, url, notes);
        await SaveAsync(ct).ConfigureAwait(false);
    }

    public async Task RemoveItemAsync(Guid itemId, CancellationToken ct = default)
    {
        CurrentVault.RemoveItem(itemId);
        await SaveAsync(ct).ConfigureAwait(false);
    }

    public async Task<VaultFolder> AddFolderAsync(string name, CancellationToken ct = default)
    {
        var pasta = CurrentVault.AddFolder(name);
        await SaveAsync(ct).ConfigureAwait(false);
        return pasta;
    }

    public async Task RenameFolderAsync(Guid folderId, string name, CancellationToken ct = default)
    {
        CurrentVault.RenameFolder(folderId, name);
        await SaveAsync(ct).ConfigureAwait(false);
    }

    public async Task RemoveFolderAsync(Guid folderId, CancellationToken ct = default)
    {
        CurrentVault.RemoveFolder(folderId);
        await SaveAsync(ct).ConfigureAwait(false);
    }

    public async Task AssignItemToFolderAsync(Guid itemId, Guid? folderId, CancellationToken ct = default)
    {
        CurrentVault.AssignItemToFolder(itemId, folderId);
        await SaveAsync(ct).ConfigureAwait(false);
    }

    public IReadOnlyList<VaultItem> SearchItems(string? termo = null, Guid? pastaId = null)
    {
        IEnumerable<VaultItem> itens = CurrentVault.Items;

        if (pastaId is not null)
            itens = itens.Where(i => i.FolderId == pastaId);

        if (!string.IsNullOrWhiteSpace(termo))
        {
            var t = termo.Trim();
            itens = itens.Where(i =>
                i.Title.Contains(t, StringComparison.OrdinalIgnoreCase)
                || (i.Username?.Contains(t, StringComparison.OrdinalIgnoreCase) ?? false)
                || (i.Url?.Contains(t, StringComparison.OrdinalIgnoreCase) ?? false)
                || (i.Notes?.Contains(t, StringComparison.OrdinalIgnoreCase) ?? false)
                || i.Category.Contains(t, StringComparison.OrdinalIgnoreCase));
        }

        return itens.ToList();
    }

    public Task<byte[]> ExportAsync(string masterPassword, CancellationToken ct = default)
    {
        ValidarSenhaMestra(masterPassword);

        var vault = CurrentVault;
        return Task.Run(() => _exportImportService.Export(vault, masterPassword), ct);
    }

    public async Task ImportAsync(byte[] fileData, string masterPassword, bool replace, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(fileData);
        ValidarSenhaMestra(masterPassword);

        var importado = await Task.Run(() => _exportImportService.Import(fileData, masterPassword), ct)
            .ConfigureAwait(false);

        if (Unlocked)
        {
            if (replace)
            {
                _vault = importado;
            }
            else
            {
                CurrentVault.MergeFrom(importado);
            }

            await SaveAsync(ct).ConfigureAwait(false);
            return;
        }

        if (await _vaultRepository.ExistsAsync(ct).ConfigureAwait(false))
        {
            throw new InvalidOperationException(
                "Desbloqueie o cofre antes de importar, ou importe na primeira execução para criar o cofre.");
        }

        var salt = _cryptoService.GenerateSalt();
        var chave = _cryptoService.DeriveKey(masterPassword, salt);

        await _vaultRepository.CreateAsync(importado, chave, salt, ct).ConfigureAwait(false);

        DefinirSessao(chave, importado);
    }

    private void DefinirSessao(byte[] chave, Vault vault)
    {
        _chave = chave;
        _vault = vault;
    }

    private void SubstituirChave(byte[] novaChave)
    {
        if (_chave is not null)
            CryptographicOperations.ZeroMemory(_chave);

        _chave = novaChave;
    }

    private static void ValidarSenhaMestra(string senhaMestra)
    {
        if (string.IsNullOrWhiteSpace(senhaMestra))
            throw new ArgumentException("A senha mestra não pode ser vazia.", nameof(senhaMestra));
    }
}