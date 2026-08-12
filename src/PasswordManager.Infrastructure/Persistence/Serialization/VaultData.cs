using PasswordManager.Domain.Entities;

namespace PasswordManager.Infrastructure.Persistence.Serialization;

/// <summary>
/// DTOs usados para serializar o agregado Vault em JSON (System.Text.Json).
/// As entidades de domínio têm setters privados, então a serialização é feita
/// através destes DTOs, reconstruindo o agregado pelas factories Rehydrate.
/// </summary>
internal sealed record VaultData(
    Guid Id,
    List<VaultItemData> Items,
    List<VaultFolderData> Folders);

internal sealed record VaultItemData(
    Guid Id,
    Guid? FolderId,
    string Title,
    string? Username,
    string Password,
    string? Url,
    string? Notes,
    string Category,
    DateTime CreatedAt,
    DateTime UpdatedAt);

internal sealed record VaultFolderData(
    Guid Id,
    string Name,
    DateTime CreatedAt);

internal static class VaultDataMapper
{
    public static VaultData FromVault(Vault vault)
    {
        return new VaultData(
            vault.Id,
            vault.Items
                .Select(i => new VaultItemData(
                    i.Id, i.FolderId, i.Title, i.Username, i.Password,
                    i.Url, i.Notes, i.Category, i.CreatedAt, i.UpdatedAt))
                .ToList(),
            vault.Folders
                .Select(f => new VaultFolderData(f.Id, f.Name, f.CreatedAt))
                .ToList());
    }

    public static Vault ToVault(VaultData data)
    {
        return Vault.Rehydrate(
            data.Id,
            (data.Items ?? new List<VaultItemData>())
                .Select(i => VaultItem.Rehydrate(
                    i.Id, i.Title, i.Password, i.Category, i.Username,
                    i.Url, i.Notes, i.FolderId, i.CreatedAt, i.UpdatedAt))
                .ToList(),
            (data.Folders ?? new List<VaultFolderData>())
                .Select(f => VaultFolder.Rehydrate(f.Id, f.Name, f.CreatedAt))
                .ToList());
    }
}