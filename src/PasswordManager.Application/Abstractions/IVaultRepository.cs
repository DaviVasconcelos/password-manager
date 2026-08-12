using PasswordManager.Domain.Entities;

namespace PasswordManager.Application.Abstractions;

public interface IVaultRepository
{
    /// <summary>
    /// Obtém o salt de derivação armazenado junto ao cofre. Retorna null
    /// se ainda não houver cofre persistido. O salt é necessário para a
    /// Application derivar a chave a partir da senha mestra.
    /// </summary>
    Task<byte[]?> GetSaltAsync(CancellationToken ct = default);

    /// <summary>
    /// Carrega o cofre, descriptografando o blob com a chave derivada.
    /// Retorna null se ainda não existir cofre persistido.
    /// Lança <see cref="PasswordManager.Application.Exceptions.CryptographicIntegrityException"/>
    /// se a chave estiver errada (dado corrompido/adulterado).
    /// </summary>
    Task<Vault?> LoadAsync(byte[] key, CancellationToken ct = default);

    /// <summary>
    /// Serializa e criptografa o Vault inteiro, sobrescrevendo o registro
    /// único existente e mantendo o salt atual. Lança exceção se ainda não
    /// houver cofre persistido — a criação deve passar por
    /// <see cref="CreateAsync"/>.
    /// </summary>
    Task SaveAsync(Vault vault, byte[] key, CancellationToken ct = default);

    /// <summary>
    /// Cria o registro único do cofre com o salt fornecido pela Application.
    /// Lança exceção se já existir cofre persistido nesta instalação.
    /// </summary>
    Task CreateAsync(Vault vault, byte[] key, byte[] salt, CancellationToken ct = default);

    /// <summary>
    /// Rotaciona o salt e re-criptografa o blob com a nova chave, persistindo
    /// a troca de senha mestra. Lança exceção se não existir cofre persistido.
    /// </summary>
    Task ChangeMasterPasswordAsync(Vault vault, byte[] newKey, byte[] newSalt, CancellationToken ct = default);

    /// <summary>
    /// Indica se já existe um cofre persistido nesta instalação.
    /// </summary>
    Task<bool> ExistsAsync(CancellationToken ct = default);
}