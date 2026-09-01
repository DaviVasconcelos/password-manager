using PasswordManager.Domain.Entities;

namespace PasswordManager.Application.Abstractions;

/// <summary>
/// Fábrica de repositórios por arquivo (ADR 0008, Opção B).
/// Cada cofre tem seu próprio arquivo SQLite; o repositório é
/// criado sob demanda para o vaultId ativo.
/// </summary>
public interface IVaultRepositoryFactory
{
    /// <summary>
    /// Cria um repositório vinculado ao cofre identificado por <paramref name="vaultId"/>.
    /// O caminho do arquivo é resolvido via <see cref="VaultRegistry.IVaultRegistry"/>.
    /// </summary>
    IVaultRepository Create(Guid vaultId);

    /// <summary>
    /// Cria um repositório para o cofre ativo.
    /// Lança se não houver cofre ativo definido.
    /// </summary>
    IVaultRepository CreateForActive();
}
