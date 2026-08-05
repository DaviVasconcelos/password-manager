using PasswordManager.Domain.Entities;

namespace PasswordManager.Application.Abstractions;

public interface IVaultRepository
{
    /// <summary>
    /// Carrega o cofre, descriptografando o blob com a senha mestra.
    /// Retorna null se ainda não existir cofre persistido.
    /// Lança CryptographicIntegrityException se a senha mestra estiver errada.
    /// </summary>
    Task<Vault?> LoadAsync(string masterPassword, CancellationToken ct = default);

    /// <summary>
    /// Serializa e criptografa o Vault inteiro, sobrescrevendo o
    /// registro único existente (upsert).
    /// </summary>
    Task SaveAsync(Vault vault, string masterPassword, CancellationToken ct = default);

    /// <summary>
    /// Indica se já existe um cofre persistido nesta instalação.
    /// </summary>
    Task<bool> ExistsAsync(CancellationToken ct = default);
}