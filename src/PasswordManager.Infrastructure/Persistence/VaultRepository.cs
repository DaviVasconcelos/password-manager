using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PasswordManager.Application.Abstractions;
using PasswordManager.Domain.Entities;
using PasswordManager.Infrastructure.Persistence.Serialization;

namespace PasswordManager.Infrastructure.Persistence;

/// <summary>
/// Implementa o <see cref="IVaultRepository"/> conforme o ADR 0003: o cofre
/// inteiro é serializado em JSON, criptografado com AES-256-GCM e persistido
/// como um único registro no SQLite (upsert). Busca e filtro ocorrem em
/// memória após a descriptografia.
/// </summary>
public sealed class VaultRepository : IVaultRepository
{
    private const int CurrentSchemaVersion = 1;
    private static readonly Guid SingletonRecordId = new("26d49760-67c6-4303-80cb-a1561fcbc775");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly VaultDbContext _dbContext;
    private readonly ICryptoService _cryptoService;

    public VaultRepository(VaultDbContext dbContext, ICryptoService cryptoService)
    {
        _dbContext = dbContext;
        _cryptoService = cryptoService;
    }

    public async Task<Vault?> LoadAsync(string masterPassword, CancellationToken ct = default)
    {
        var record = await _dbContext.Vaults
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == SingletonRecordId, ct)
            .ConfigureAwait(false);

        if (record is null)
            return null;

        if (record.SchemaVersion > CurrentSchemaVersion)
            throw new InvalidOperationException(
                $"O cofre usa uma versão de schema ({record.SchemaVersion}) mais recente do que a suportada ({CurrentSchemaVersion}).");

        var key = _cryptoService.DeriveKey(masterPassword, record.Salt);
        var json = _cryptoService.Decrypt(record.EncryptedBlob, key);
        var data = JsonSerializer.Deserialize<VaultData>(json, JsonOptions)
            ?? throw new InvalidOperationException("Falha ao desserializar o cofre.");

        return VaultDataMapper.ToVault(data);
    }

    public async Task SaveAsync(Vault vault, string masterPassword, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(vault);

        var existing = await _dbContext.Vaults
            .SingleOrDefaultAsync(r => r.Id == SingletonRecordId, ct)
            .ConfigureAwait(false);

        var salt = existing?.Salt ?? _cryptoService.GenerateSalt();
        var key = _cryptoService.DeriveKey(masterPassword, salt);
        var json = JsonSerializer.Serialize(VaultDataMapper.FromVault(vault), JsonOptions);
        var encryptedBlob = _cryptoService.Encrypt(Encoding.UTF8.GetBytes(json), key);

        if (existing is null)
        {
            _dbContext.Vaults.Add(new VaultRecord
            {
                Id = SingletonRecordId,
                SchemaVersion = CurrentSchemaVersion,
                Salt = salt,
                EncryptedBlob = encryptedBlob,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.SchemaVersion = CurrentSchemaVersion;
            existing.EncryptedBlob = encryptedBlob;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public Task<bool> ExistsAsync(CancellationToken ct = default)
    {
        return _dbContext.Vaults
            .AsNoTracking()
            .AnyAsync(ct);
    }
}
