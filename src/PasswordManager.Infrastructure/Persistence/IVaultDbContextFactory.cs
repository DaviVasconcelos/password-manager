namespace PasswordManager.Infrastructure.Persistence;

/// <summary>
/// Fábrica de <see cref="VaultDbContext"/> por arquivo.
/// Cada cofre (Opção B, ADR 0008) tem seu próprio arquivo SQLite em Vaults/*.db,
/// portanto o contexto não pode mais ser singleton com caminho fixo.
/// A fábrica cria um contexto novo por caminho, aplicando migrations/baseline
/// quando necessário.
/// </summary>
public interface IVaultDbContextFactory
{
    /// <summary>
    /// Cria um novo <see cref="VaultDbContext"/> para o arquivo informado.
    /// O chamador é responsável por descartar o contexto (using/Dispose).
    /// </summary>
    VaultDbContext Create(string caminhoDb);

    /// <summary>
    /// Cria um novo <see cref="VaultDbContext"/> para o arquivo informado
    /// e aplica as migrations pendentes (incluindo baseline de bancos legados).
    /// </summary>
    VaultDbContext CreateAndMigrate(string caminhoDb);
}
