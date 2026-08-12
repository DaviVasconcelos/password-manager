using PasswordManager.Application.Abstractions;
using PasswordManager.Application.Exceptions;
using PasswordManager.Domain.Entities;

namespace PasswordManager.Application.Tests.Fakes;

/// <summary>
/// Fake em memória do <see cref="IVaultRepository"/> que simula o armazenamento
/// criptografado: retém salt, chave e cofre, e lança
/// <see cref="CryptographicIntegrityException"/> quando a chave informada não
/// corresponde à persistida (equivale à falha de tag do AES-GCM no repositório real).
/// </summary>
internal sealed class FakeVaultRepository : IVaultRepository
{
    private byte[]? _salt;
    private byte[]? _chave;
    private Vault? _vault;

    public byte[]? SaltPersistido => _salt?.ToArray();
    public byte[]? ChavePersistida => _chave?.ToArray();
    public Vault? VaultPersistido => _vault;

    public Task<byte[]?> GetSaltAsync(CancellationToken ct = default)
        => Task.FromResult(_salt?.ToArray());

    public Task<Vault?> LoadAsync(byte[] key, CancellationToken ct = default)
    {
        if (_vault is null)
            return Task.FromResult<Vault?>(null);

        GarantirChaveCorreta(key);
        return Task.FromResult<Vault?>(_vault);
    }

    public Task SaveAsync(Vault vault, byte[] key, CancellationToken ct = default)
    {
        if (_vault is null)
            throw new InvalidOperationException("Não existe cofre persistido para atualizar.");

        GarantirChaveCorreta(key);
        _vault = vault;
        return Task.CompletedTask;
    }

    public Task CreateAsync(Vault vault, byte[] key, byte[] salt, CancellationToken ct = default)
    {
        if (_vault is not null)
            throw new InvalidOperationException("Já existe cofre persistido nesta instalação.");

        _salt = salt.ToArray();
        _chave = key.ToArray();
        _vault = vault;
        return Task.CompletedTask;
    }

    public Task ChangeMasterPasswordAsync(Vault vault, byte[] newKey, byte[] newSalt, CancellationToken ct = default)
    {
        if (_vault is null)
            throw new InvalidOperationException("Não existe cofre persistido para alterar a senha mestra.");

        _salt = newSalt.ToArray();
        _chave = newKey.ToArray();
        _vault = vault;
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(CancellationToken ct = default)
        => Task.FromResult(_vault is not null);

    private void GarantirChaveCorreta(byte[] key)
    {
        if (_chave is null || key is null || _chave.Length != key.Length || !_chave.AsSpan().SequenceEqual(key))
            throw new CryptographicIntegrityException("Chave incorreta (simulação de falha de autenticação).");
    }
}