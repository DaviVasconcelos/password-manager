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

    // --- Overloads multi-arquivo (ADR 0008, Opção B) ---
    // Mantidos como default interface methods para não quebrar implementações
    // legadas (VaultRepository singleton) na Etapa 7.1. A implementação
    // multi-arquivo (Etapa 7.2) irá sobrescrever estes métodos.

    /// <summary>
    /// Obtém o salt do cofre identificado por <paramref name="vaultId"/>.
    /// </summary>
    Task<byte[]?> GetSaltAsync(Guid vaultId, CancellationToken ct = default) => GetSaltAsync(ct);

    /// <summary>
    /// Carrega o cofre identificado por <paramref name="vaultId"/>.
    /// </summary>
    Task<Vault?> LoadAsync(Guid vaultId, byte[] key, CancellationToken ct = default) => LoadAsync(key, ct);

    /// <summary>
    /// Salva o cofre identificado por <paramref name="vaultId"/>.
    /// </summary>
    Task SaveAsync(Guid vaultId, Vault vault, byte[] key, CancellationToken ct = default) => SaveAsync(vault, key, ct);

    /// <summary>
    /// Cria o cofre identificado por <paramref name="vaultId"/>.
    /// </summary>
    Task CreateAsync(Guid vaultId, Vault vault, byte[] key, byte[] salt, CancellationToken ct = default) => CreateAsync(vault, key, salt, ct);

    /// <summary>
    /// Rotaciona o salt do cofre identificado por <paramref name="vaultId"/>.
    /// </summary>
    Task ChangeMasterPasswordAsync(Guid vaultId, Vault vault, byte[] newKey, byte[] newSalt, CancellationToken ct = default) => ChangeMasterPasswordAsync(vault, newKey, newSalt, ct);

    /// <summary>
    /// Indica se existe o cofre identificado por <paramref name="vaultId"/>.
    /// </summary>
    Task<bool> ExistsAsync(Guid vaultId, CancellationToken ct = default) => ExistsAsync(ct);
}