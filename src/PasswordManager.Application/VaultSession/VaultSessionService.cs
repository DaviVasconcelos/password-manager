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

    private byte[]? _chave;
    private Vault? _vault;

    public VaultSessionService(IVaultRepository vaultRepository, ICryptoService cryptoService)
    {
        _vaultRepository = vaultRepository;
        _cryptoService = cryptoService;
    }

    public bool Desbloqueado => _vault is not null;

    public Vault VaultAtual =>
        _vault ?? throw new InvalidOperationException("A sessão está trancada; desbloqueie o cofre antes de acessá-lo.");

    public async Task<bool> ExisteCofreAsync(CancellationToken ct = default)
        => await _vaultRepository.ExistsAsync(ct).ConfigureAwait(false);

    public async Task<Vault> CriarAsync(string senhaMestra, CancellationToken ct = default)
    {
        ValidarSenhaMestra(senhaMestra);

        if (Desbloqueado)
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

    public async Task<Vault> DesbloquearAsync(string senhaMestra, CancellationToken ct = default)
    {
        ValidarSenhaMestra(senhaMestra);

        if (Desbloqueado)
            throw new InvalidOperationException("A sessão já está desbloqueada; tranque o cofre antes de desbloquear novamente.");

        var salt = await _vaultRepository.GetSaltAsync(ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Não há cofre persistido; use CriarAsync.");

        var chave = _cryptoService.DeriveKey(senhaMestra, salt);
        var vault = await _vaultRepository.LoadAsync(chave, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Não há cofre persistido; use CriarAsync.");

        DefinirSessao(chave, vault);
        return vault;
    }

    public void Trancar()
    {
        if (_chave is not null)
        {
            CryptographicOperations.ZeroMemory(_chave);
            _chave = null;
        }

        _vault = null;
    }

    public async Task TrocarSenhaMestraAsync(string novaSenhaMestra, CancellationToken ct = default)
    {
        ValidarSenhaMestra(novaSenhaMestra);

        var vault = VaultAtual;
        var novoSalt = _cryptoService.GenerateSalt();
        var novaChave = _cryptoService.DeriveKey(novaSenhaMestra, novoSalt);

        await _vaultRepository.ChangeMasterPasswordAsync(vault, novaChave, novoSalt, ct).ConfigureAwait(false);

        SubstituirChave(novaChave);
    }

    public async Task SalvarAsync(CancellationToken ct = default)
    {
        var vault = VaultAtual;
        var chave = _chave ?? throw new InvalidOperationException("A sessão está trancada; desbloqueie o cofre antes.");

        await _vaultRepository.SaveAsync(vault, chave, ct).ConfigureAwait(false);
    }

    public async Task<VaultItem> AdicionarItemAsync(string title, string password, string category,
        string? username = null, string? url = null, string? notes = null, CancellationToken ct = default)
    {
        var item = VaultAtual.AddItem(title, password, category, username, url, notes);
        await SalvarAsync(ct).ConfigureAwait(false);
        return item;
    }

    public async Task AtualizarItemAsync(Guid itemId, string title, string password, string category,
        string? username = null, string? url = null, string? notes = null, CancellationToken ct = default)
    {
        VaultAtual.UpdateItem(itemId, title, password, category, username, url, notes);
        await SalvarAsync(ct).ConfigureAwait(false);
    }

    public async Task RemoverItemAsync(Guid itemId, CancellationToken ct = default)
    {
        VaultAtual.RemoveItem(itemId);
        await SalvarAsync(ct).ConfigureAwait(false);
    }

    public async Task<VaultFolder> AdicionarPastaAsync(string name, CancellationToken ct = default)
    {
        var pasta = VaultAtual.AddFolder(name);
        await SalvarAsync(ct).ConfigureAwait(false);
        return pasta;
    }

    public async Task RenomearPastaAsync(Guid folderId, string name, CancellationToken ct = default)
    {
        VaultAtual.RenameFolder(folderId, name);
        await SalvarAsync(ct).ConfigureAwait(false);
    }

    public async Task RemoverPastaAsync(Guid folderId, CancellationToken ct = default)
    {
        VaultAtual.RemoveFolder(folderId);
        await SalvarAsync(ct).ConfigureAwait(false);
    }

    public async Task AtribuirItemAPastaAsync(Guid itemId, Guid? folderId, CancellationToken ct = default)
    {
        VaultAtual.AssignItemToFolder(itemId, folderId);
        await SalvarAsync(ct).ConfigureAwait(false);
    }

    public IReadOnlyList<VaultItem> BuscarItens(string? termo = null, Guid? pastaId = null)
    {
        IEnumerable<VaultItem> itens = VaultAtual.Items;

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