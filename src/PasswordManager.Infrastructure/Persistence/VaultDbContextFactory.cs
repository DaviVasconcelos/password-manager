using Microsoft.EntityFrameworkCore;

namespace PasswordManager.Infrastructure.Persistence;

/// <summary>
/// Fábrica padrão de <see cref="VaultDbContext"/> por arquivo (ADR 0008, Opção B).
/// </summary>
public sealed class VaultDbContextFactory : IVaultDbContextFactory
{
    public VaultDbContext Create(string caminhoDb)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caminhoDb);

        var options = new DbContextOptionsBuilder<VaultDbContext>()
            .UseSqlite($"Data Source={caminhoDb};Pooling=False")
            .Options;

        return new VaultDbContext(options);
    }

    public VaultDbContext CreateAndMigrate(string caminhoDb)
    {
        var context = Create(caminhoDb);
        VaultDatabaseMigrator.ApplyMigrations(context);
        return context;
    }
}
