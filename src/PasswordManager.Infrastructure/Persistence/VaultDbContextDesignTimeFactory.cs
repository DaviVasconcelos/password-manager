using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PasswordManager.Infrastructure.Persistence;

/// <summary>
/// Factory usada pelas ferramentas de design do EF Core (dotnet-ef) para criar o
/// VaultDbContext em tempo de design, já que o caminho real do banco só é montado
/// pelo composition root da UI. Usa o mesmo caminho da produção
/// (LocalAppData\PasswordManager\vault.db); nenhuma conexão é aberta em tempo de design.
/// </summary>
public sealed class VaultDbContextDesignTimeFactory : IDesignTimeDbContextFactory<VaultDbContext>
{
    public VaultDbContext CreateDbContext(string[] args)
    {
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PasswordManager");
        var bankPath = Path.Combine(appDataDir, "vault.db");

        var options = new DbContextOptionsBuilder<VaultDbContext>()
            .UseSqlite($"Data Source={bankPath}")
            .Options;

        return new VaultDbContext(options);
    }
}
