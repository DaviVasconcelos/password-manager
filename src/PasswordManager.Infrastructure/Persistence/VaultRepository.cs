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

    private readonly VaultDbContext? _dbContext;
    private readonly IVaultDbContextFactory? _factory;
    private readonly string? _caminhoDb;
    private readonly ICryptoService _cryptoService;

    /// <summary>
    /// Construtor legado (singleton, usado em testes com conexão em memória).
    /// </summary>
    public VaultRepository(VaultDbContext dbContext, ICryptoService cryptoService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _cryptoService = cryptoService ?? throw new ArgumentNullException(nameof(cryptoService));
    }

    /// <summary>
    /// Construtor multi-arquivo (ADR 0008, Opção B): o repositório é vinculado
    /// a um arquivo específico e cria um contexto por operação via factory
    /// (evita file-lock do SQLite).
    /// </summary>
    public VaultRepository(IVaultDbContextFactory factory, string caminhoDb, ICryptoService cryptoService)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _caminhoDb = caminhoDb ?? throw new ArgumentNullException(nameof(caminhoDb));
        if (string.IsNullOrWhiteSpace(caminhoDb))
            throw new ArgumentException("O caminho do banco não pode ser vazio.", nameof(caminhoDb));
        _cryptoService = cryptoService ?? throw new ArgumentNullException(nameof(cryptoService));
    }

    private VaultDbContext ObterContexto()
    {
        if (_factory is not null && _caminhoDb is not null)
            return _factory.CreateAndMigrate(_caminhoDb);

        return _dbContext!;
    }

    private bool EhModoFactory => _factory is not null;

    public async Task<byte[]?> GetSaltAsync(CancellationToken ct = default)
    {
        if (EhModoFactory)
        {
            using var ctx = ObterContexto();
            var record = await ctx.Vaults
                .AsNoTracking()
                .SingleOrDefaultAsync(r => r.Id == SingletonRecordId, ct)
                .ConfigureAwait(false);
            return record?.Salt;
        }

        var rec = await _dbContext!.Vaults
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == SingletonRecordId, ct)
            .ConfigureAwait(false);
        return rec?.Salt;
    }

    public async Task<Vault?> LoadAsync(byte[] key, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (EhModoFactory)
        {
            using var ctx = ObterContexto();
            var record = await ctx.Vaults
                .AsNoTracking()
                .SingleOrDefaultAsync(r => r.Id == SingletonRecordId, ct)
                .ConfigureAwait(false);
            if (record is null) return null;
            if (record.SchemaVersion > CurrentSchemaVersion)
                throw new InvalidOperationException(
                    $"O cofre usa uma versão de schema ({record.SchemaVersion}) mais recente do que a suportada ({CurrentSchemaVersion}).");
            var json = _cryptoService.Decrypt(record.EncryptedBlob, key);
            var data = JsonSerializer.Deserialize<VaultData>(json, JsonOptions)
                ?? throw new InvalidOperationException("Falha ao desserializar o cofre.");
            return VaultDataMapper.ToVault(data);
        }

        var rec2 = await _dbContext!.Vaults
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == SingletonRecordId, ct)
            .ConfigureAwait(false);
        if (rec2 is null) return null;
        if (rec2.SchemaVersion > CurrentSchemaVersion)
            throw new InvalidOperationException(
                $"O cofre usa uma versão de schema ({rec2.SchemaVersion}) mais recente do que a suportada ({CurrentSchemaVersion}).");
        var j = _cryptoService.Decrypt(rec2.EncryptedBlob, key);
        var d = JsonSerializer.Deserialize<VaultData>(j, JsonOptions)
            ?? throw new InvalidOperationException("Falha ao desserializar o cofre.");
        return VaultDataMapper.ToVault(d);
    }

    public async Task SaveAsync(Vault vault, byte[] key, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(vault);
        ArgumentNullException.ThrowIfNull(key);

        if (EhModoFactory)
        {
            using var ctx = ObterContexto();
            var existing = await ctx.Vaults
                .SingleOrDefaultAsync(r => r.Id == SingletonRecordId, ct)
                .ConfigureAwait(false);
            if (existing is null)
                throw new InvalidOperationException(
                    "Não existe cofre persistido para atualizar; use CreateAsync para criar o cofre.");
            existing.SchemaVersion = CurrentSchemaVersion;
            existing.EncryptedBlob = Criptografar(vault, key);
            existing.UpdatedAt = DateTime.UtcNow;
            await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
            return;
        }

        var ex = await _dbContext!.Vaults
            .SingleOrDefaultAsync(r => r.Id == SingletonRecordId, ct)
            .ConfigureAwait(false);
        if (ex is null)
            throw new InvalidOperationException(
                "Não existe cofre persistido para atualizar; use CreateAsync para criar o cofre.");
        ex.SchemaVersion = CurrentSchemaVersion;
        ex.EncryptedBlob = Criptografar(vault, key);
        ex.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task CreateAsync(Vault vault, byte[] key, byte[] salt, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(vault);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(salt);

        if (EhModoFactory)
        {
            using var ctx = ObterContexto();
            var existing = await ctx.Vaults
                .SingleOrDefaultAsync(r => r.Id == SingletonRecordId, ct)
                .ConfigureAwait(false);
            if (existing is not null)
                throw new InvalidOperationException(
                    "Já existe um cofre persistido nesta instalação; não é possível criar outro.");
            ctx.Vaults.Add(new VaultRecord
            {
                Id = SingletonRecordId,
                SchemaVersion = CurrentSchemaVersion,
                Salt = salt,
                EncryptedBlob = Criptografar(vault, key),
                UpdatedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
            return;
        }

        var e2 = await _dbContext!.Vaults
            .SingleOrDefaultAsync(r => r.Id == SingletonRecordId, ct)
            .ConfigureAwait(false);
        if (e2 is not null)
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

        if (EhModoFactory)
        {
            using var ctx = ObterContexto();
            var existing = await ctx.Vaults
                .SingleOrDefaultAsync(r => r.Id == SingletonRecordId, ct)
                .ConfigureAwait(false);
            if (existing is null)
                throw new InvalidOperationException(
                    "Não existe cofre persistido para alterar a senha mestra; use CreateAsync para criar o cofre.");
            existing.SchemaVersion = CurrentSchemaVersion;
            existing.Salt = newSalt;
            existing.EncryptedBlob = Criptografar(vault, newKey);
            existing.UpdatedAt = DateTime.UtcNow;
            await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
            return;
        }

        var e3 = await _dbContext!.Vaults
            .SingleOrDefaultAsync(r => r.Id == SingletonRecordId, ct)
            .ConfigureAwait(false);
        if (e3 is null)
            throw new InvalidOperationException(
                "Não existe cofre persistido para alterar a senha mestra; use CreateAsync para criar o cofre.");
        e3.SchemaVersion = CurrentSchemaVersion;
        e3.Salt = newSalt;
        e3.EncryptedBlob = Criptografar(vault, newKey);
        e3.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public Task<bool> ExistsAsync(CancellationToken ct = default)
    {
        if (EhModoFactory)
        {
            using var ctx = ObterContexto();
            return ctx.Vaults.AsNoTracking().AnyAsync(ct);
        }
        return _dbContext!.Vaults
            .AsNoTracking()
            .AnyAsync(ct);
    }

    private byte[] Criptografar(Vault vault, byte[] key)
    {
        var json = JsonSerializer.Serialize(VaultDataMapper.FromVault(vault), JsonOptions);
        return _cryptoService.Encrypt(Encoding.UTF8.GetBytes(json), key);
    }
}