using System.Security.Cryptography;
using PasswordManager.Application.Abstractions;
using PasswordManager.Application.Exceptions;
using PasswordManager.Application.VaultRegistry;
using PasswordManager.Domain.Entities;

namespace PasswordManager.Application.VaultSession;

/// <summary>
/// Implementa o <see cref="IVaultSessionService"/>. O serviço é o único ponto
/// que conhece a senha mestra na Application, usando-a apenas para derivar a
/// chave no desbloqueio/criação e descartando-a em seguida: a sessão retém
/// somente a chave derivada (e o <see cref="Vault"/> carregado), zerando-a
/// ao trancar. Suporta multi-arquivo (ADR 0008) via <see cref="IVaultRegistry"/>
/// e <see cref="IVaultRepositoryFactory"/>; mantém fallback legado para testes.
/// </summary>
public sealed class VaultSessionService : IVaultSessionService
{
    private readonly IVaultRepository _vaultRepository;
    private readonly IVaultRegistry? _vaultRegistry;
    private readonly IVaultRepositoryFactory? _repositoryFactory;
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

    /// <summary>
    /// Construtor multi-arquivo (produção, ADR 0008).
    /// </summary>
    public VaultSessionService(
        IVaultRepository vaultRepository,
        IVaultRegistry vaultRegistry,
        IVaultRepositoryFactory repositoryFactory,
        ICryptoService cryptoService,
        IExportImportService exportImportService)
    {
        _vaultRepository = vaultRepository;
        _vaultRegistry = vaultRegistry;
        _repositoryFactory = repositoryFactory;
        _cryptoService = cryptoService;
        _exportImportService = exportImportService;
    }

    public bool Unlocked => _vault is not null;

    public Vault CurrentVault =>
        _vault ?? throw new InvalidOperationException("A sessão está trancada; desbloqueie o cofre antes de acessá-lo.");

    public VaultDescriptor? CofreAtivo => _vaultRegistry?.Ativo;

    private bool EhMultiArquivo => _vaultRegistry is not null && _repositoryFactory is not null;

    private IVaultRepository RepositorioAtivo
    {
        get
        {
            if (EhMultiArquivo)
                return _repositoryFactory!.CreateForActive();
            return _vaultRepository;
        }
    }

    public async Task<bool> VaultExistsAsync(CancellationToken ct = default)
    {
        if (EhMultiArquivo)
        {
            var lista = await _vaultRegistry!.ListarAsync(ct).ConfigureAwait(false);
            return lista.Count > 0;
        }
        return await _vaultRepository.ExistsAsync(ct).ConfigureAwait(false);
    }

    public Task<Vault> CreateAsync(string senhaMestra, CancellationToken ct = default)
        => CreateAsync(null, senhaMestra, ct);

    public async Task<Vault> CreateAsync(string? nome, string senhaMestra, CancellationToken ct = default)
    {
        ValidarSenhaMestra(senhaMestra);

        if (Unlocked)
            throw new InvalidOperationException("A sessão já está desbloqueada; tranque o cofre antes de criar outro.");

        if (EhMultiArquivo)
        {
            // Garante que o registry está inicializado.
            await _vaultRegistry!.InicializarAsync(ct).ConfigureAwait(false);

            var descriptor = await _vaultRegistry.CriarAsync(nome, ct).ConfigureAwait(false);
            var salt = _cryptoService.GenerateSalt();
            var chave = _cryptoService.DeriveKey(senhaMestra, salt);
            var vault = Vault.CreateNew();

            var repo = _repositoryFactory!.Create(descriptor.Id);
            await repo.CreateAsync(vault, chave, salt, ct).ConfigureAwait(false);

            DefinirSessao(chave, vault);
            return vault;
        }

        if (await _vaultRepository.ExistsAsync(ct).ConfigureAwait(false))
            throw new InvalidOperationException("Já existe um cofre persistido nesta instalação; use DesbloquearAsync.");

        var saltLegado = _cryptoService.GenerateSalt();
        var chaveLegada = _cryptoService.DeriveKey(senhaMestra, saltLegado);
        var vaultLegado = Vault.CreateNew();

        await _vaultRepository.CreateAsync(vaultLegado, chaveLegada, saltLegado, ct).ConfigureAwait(false);

        DefinirSessao(chaveLegada, vaultLegado);
        return vaultLegado;
    }

    public async Task<Vault> UnlockAsync(string senhaMestra, CancellationToken ct = default)
    {
        ValidarSenhaMestra(senhaMestra);

        if (Unlocked)
            throw new InvalidOperationException("A sessão já está desbloqueada; tranque o cofre antes de desbloquear novamente.");

        if (EhMultiArquivo)
        {
            await _vaultRegistry!.InicializarAsync(ct).ConfigureAwait(false);

            var ativo = _vaultRegistry.Ativo
                        ?? throw new InvalidOperationException("Não há cofre selecionado; selecione um cofre antes de desbloquear.");

            var repo = _repositoryFactory!.Create(ativo.Id);
            var salt = await repo.GetSaltAsync(ct).ConfigureAwait(false)
                       ?? throw new InvalidOperationException("Não há cofre persistido; use CriarAsync.");

            var chave = _cryptoService.DeriveKey(senhaMestra, salt);
            var vault = await repo.LoadAsync(chave, ct).ConfigureAwait(false)
                        ?? throw new InvalidOperationException("Não há cofre persistido; use CriarAsync.");

            DefinirSessao(chave, vault);
            return vault;
        }

        var saltLegado = await _vaultRepository.GetSaltAsync(ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Não há cofre persistido; use CriarAsync.");

        var chaveLegada = _cryptoService.DeriveKey(senhaMestra, saltLegado);
        var vaultLegado = await _vaultRepository.LoadAsync(chaveLegada, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Não há cofre persistido; use CriarAsync.");

        DefinirSessao(chaveLegada, vaultLegado);
        return vaultLegado;
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

    public async Task ChangeMasterPasswordAsync(string senhaAtual, string novaSenhaMestra, CancellationToken ct = default)
    {
        ValidarSenhaMestra(senhaAtual);
        ValidarSenhaMestra(novaSenhaMestra);

        var vault = CurrentVault;
        var chaveRetida = _chave ?? throw new InvalidOperationException("A sessão está trancada; desbloqueie o cofre antes.");

        var repo = RepositorioAtivo;
        var saltAtual = await repo.GetSaltAsync(ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Não há cofre persistido; use CriarAsync.");

        var chaveDerivadaDaSenhaAtual = _cryptoService.DeriveKey(senhaAtual, saltAtual);
        if (chaveDerivadaDaSenhaAtual.Length != chaveRetida.Length
            || !CryptographicOperations.FixedTimeEquals(chaveDerivadaDaSenhaAtual, chaveRetida))
        {
            CryptographicOperations.ZeroMemory(chaveDerivadaDaSenhaAtual);
            throw new CryptographicIntegrityException("A senha mestra atual está incorreta.");
        }

        CryptographicOperations.ZeroMemory(chaveDerivadaDaSenhaAtual);

        var novoSalt = _cryptoService.GenerateSalt();
        var novaChave = _cryptoService.DeriveKey(novaSenhaMestra, novoSalt);

        await repo.ChangeMasterPasswordAsync(vault, novaChave, novoSalt, ct).ConfigureAwait(false);

        SubstituirChave(novaChave);
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        var vault = CurrentVault;
        var chave = _chave ?? throw new InvalidOperationException("A sessão está trancada; desbloqueie o cofre antes.");

        await RepositorioAtivo.SaveAsync(vault, chave, ct).ConfigureAwait(false);
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

        if (EhMultiArquivo)
        {
            await _vaultRegistry!.InicializarAsync(ct).ConfigureAwait(false);
            var existentes = await _vaultRegistry.ListarAsync(ct).ConfigureAwait(false);
            if (existentes.Count > 0)
            {
                throw new InvalidOperationException(
                    "Desbloqueie o cofre antes de importar, ou importe na primeira execução para criar o cofre.");
            }

            var descriptor = await _vaultRegistry.CriarAsync(null, ct).ConfigureAwait(false);
            var salt = _cryptoService.GenerateSalt();
            var chave = _cryptoService.DeriveKey(masterPassword, salt);
            var repo = _repositoryFactory!.Create(descriptor.Id);
            await repo.CreateAsync(importado, chave, salt, ct).ConfigureAwait(false);
            DefinirSessao(chave, importado);
            return;
        }

        if (await _vaultRepository.ExistsAsync(ct).ConfigureAwait(false))
        {
            throw new InvalidOperationException(
                "Desbloqueie o cofre antes de importar, ou importe na primeira execução para criar o cofre.");
        }

        var saltLegado = _cryptoService.GenerateSalt();
        var chaveLegada = _cryptoService.DeriveKey(masterPassword, saltLegado);

        await _vaultRepository.CreateAsync(importado, chaveLegada, saltLegado, ct).ConfigureAwait(false);

        DefinirSessao(chaveLegada, importado);
    }

    public async Task<IReadOnlyList<VaultDescriptor>> ListarCofresAsync(CancellationToken ct = default)
    {
        if (!EhMultiArquivo)
            return Array.Empty<VaultDescriptor>();

        await _vaultRegistry!.InicializarAsync(ct).ConfigureAwait(false);
        return await _vaultRegistry.ListarAsync(ct).ConfigureAwait(false);
    }

    public async Task RenomearCofreAsync(Guid id, string novoNome, CancellationToken ct = default)
    {
        if (!EhMultiArquivo)
            throw new InvalidOperationException("Operação de múltiplos cofres não está disponível nesta configuração.");

        await _vaultRegistry!.RenomearAsync(id, novoNome, ct).ConfigureAwait(false);
    }

    public async Task ExcluirCofreAsync(Guid id, CancellationToken ct = default)
    {
        if (!EhMultiArquivo)
            throw new InvalidOperationException("Operação de múltiplos cofres não está disponível nesta configuração.");

        var eraAtivo = _vaultRegistry!.AtivoId == id;
        if (eraAtivo && Unlocked)
            Lock();

        await _vaultRegistry.ExcluirAsync(id, ct).ConfigureAwait(false);
    }

    public async Task SelecionarCofreAsync(Guid id, CancellationToken ct = default)
    {
        if (!EhMultiArquivo)
            throw new InvalidOperationException("Operação de múltiplos cofres não está disponível nesta configuração.");

        if (Unlocked)
            Lock();

        await _vaultRegistry!.DefinirAtivoAsync(id, ct).ConfigureAwait(false);
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
