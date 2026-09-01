using PasswordManager.Application.Abstractions;
using PasswordManager.Application.VaultRegistry;

namespace PasswordManager.Infrastructure.Persistence;

/// <summary>
/// Fábrica de <see cref="IVaultRepository"/> por arquivo (ADR 0008).
/// </summary>
public sealed class VaultRepositoryFactory : IVaultRepositoryFactory
{
    private readonly IVaultRegistry _registry;
    private readonly IVaultDbContextFactory _dbContextFactory;
    private readonly ICryptoService _cryptoService;

    public VaultRepositoryFactory(
        IVaultRegistry registry,
        IVaultDbContextFactory dbContextFactory,
        ICryptoService cryptoService)
    {
        _registry = registry;
        _dbContextFactory = dbContextFactory;
        _cryptoService = cryptoService;
    }

    public IVaultRepository Create(Guid vaultId)
    {
        var caminho = _registry.ObterCaminho(vaultId);
        return new VaultRepository(_dbContextFactory, caminho, _cryptoService);
    }

    public IVaultRepository CreateForActive()
    {
        var caminho = _registry.ObterCaminhoAtivo();
        return new VaultRepository(_dbContextFactory, caminho, _cryptoService);
    }
}
