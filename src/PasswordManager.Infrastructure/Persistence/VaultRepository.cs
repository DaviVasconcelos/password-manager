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
/// como um único registro no SQLite. Busca e filtro ocorrem em memória após
/// a descriptografia.
/// </summary>
/// <remarks>
/// O repositório recebe a chave já derivada pela Application (nunca a senha
/// mestra em si) e é responsável apenas por criptografar/descriptografar
/// o blob e serializar/desserializar o agregado.
/// </remarks>
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

    public async Task<byte[]?> GetSaltAsync(CancellationToken ct = default)
    {
        var record = await _dbContext.Vaults
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == SingletonRecordId, ct)
            .ConfigureAwait(false);

        return record?.Salt;
    }

    public async Task<Vault?> LoadAsync(byte[] key, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        var record = await _dbContext.Vaults
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == SingletonRecordId, ct)
            .ConfigureAwait(false);

        if (record is null)
            return null;

        if (record.SchemaVersion > CurrentSchemaVersion)
            throw new InvalidOperationException(
                $"O cofre usa uma versão de schema ({record.SchemaVersion}) mais recente do que a suportada ({CurrentSchemaVersion}).");

        var json = _cryptoService.Decrypt(record.EncryptedBlob, key);
        var data = JsonSerializer.Deserialize<VaultData>(json, JsonOptions)
            ?? throw new InvalidOperationException("Falha ao desserializar o cofre.");

        return VaultDataMapper.ToVault(data);
    }

    public async Task SaveAsync(Vault vault, byte[] key, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(vault);
        ArgumentNullException.ThrowIfNull(key);

        var existing = await _dbContext.Vaults
            .SingleOrDefaultAsync(r => r.Id == SingletonRecordId, ct)
            .ConfigureAwait(false);

        if (existing is null)
            throw new InvalidOperationException(
                "Não existe cofre persistido para atualizar; use CreateAsync para criar o cofre.");

        existing.SchemaVersion = CurrentSchemaVersion;
        existing.EncryptedBlob = Criptografar(vault, key);
        existing.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task CreateAsync(Vault vault, byte[] key, byte[] salt, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(vault);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(salt);

        var existing = await _dbContext.Vaults
            .SingleOrDefaultAsync(r => r.Id == SingletonRecordId, ct)
            .ConfigureAwait(false);

        if (existing is not null)
            throw new InvalidOperationException(
                "Já existe um cofre persistido nesta instalação; não é possível criar outro.");

        _dbContext.Vaults.Add(new VaultRecord
        {
            Id = SingletonRecordId,
            SchemaVersion = CurrentSchemaVersion,
            Salt = salt,
            EncryptedBlob = Criptografar(vault, key),
            UpdatedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task ChangeMasterPasswordAsync(Vault vault, byte[] newKey, byte[] newSalt, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(vault);
        ArgumentNullException.ThrowIfNull(newKey);
        ArgumentNullException.ThrowIfNull(newSalt);

        var existing = await _dbContext.Vaults
            .SingleOrDefaultAsync(r => r.Id == SingletonRecordId, ct)
            .ConfigureAwait(false);

        if (existing is null)
            throw new InvalidOperationException(
                "Não existe cofre persistido para alterar a senha mestra; use CreateAsync para criar o cofre.");

        existing.SchemaVersion = CurrentSchemaVersion;
        existing.Salt = newSalt;
        existing.EncryptedBlob = Criptografar(vault, newKey);
        existing.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public Task<bool> ExistsAsync(CancellationToken ct = default)
    {
        return _dbContext.Vaults
            .AsNoTracking()
            .AnyAsync(ct);
    }

    private byte[] Criptografar(Vault vault, byte[] key)
    {
        var json = JsonSerializer.Serialize(VaultDataMapper.FromVault(vault), JsonOptions);
        return _cryptoService.Encrypt(Encoding.UTF8.GetBytes(json), key);
    }
}