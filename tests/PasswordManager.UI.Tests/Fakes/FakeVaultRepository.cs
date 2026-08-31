using PasswordManager.Application.Abstractions;
using PasswordManager.Application.Exceptions;
using PasswordManager.Domain.Entities;

namespace PasswordManager.UI.Tests.Fakes;

/// <summary>
/// Fake em memória de <see cref="IVaultRepository"/>.
/// </summary>
internal sealed class FakeVaultRepository : IVaultRepository
{
    private byte[]? _salt;
    private byte[]? _chave;
    private Vault? _vault;

    public Task<byte[]?> GetSaltAsync(CancellationToken ct = default) => Task.FromResult(_salt?.ToArray());
    public Task<bool> ExistsAsync(CancellationToken ct = default) => Task.FromResult(_vault is not null);

    public Task<Vault?> LoadAsync(byte[] key, CancellationToken ct = default)
    {
        if (_vault is null) return Task.FromResult<Vault?>(null);
        GarantirChave(key);
        return Task.FromResult<Vault?>(_vault);
    }

    public Task SaveAsync(Vault vault, byte[] key, CancellationToken ct = default)
    {
        if (_vault is null) throw new InvalidOperationException("Não existe cofre persistido para atualizar.");
        GarantirChave(key);
        _vault = vault;
        return Task.CompletedTask;
    }

    public Task CreateAsync(Vault vault, byte[] key, byte[] salt, CancellationToken ct = default)
    {
        if (_vault is not null) throw new InvalidOperationException("Já existe cofre persistido nesta instalação.");
        _salt = salt.ToArray();
        _chave = key.ToArray();
        _vault = vault;
        return Task.CompletedTask;
    }

    public Task ChangeMasterPasswordAsync(Vault vault, byte[] newKey, byte[] newSalt, CancellationToken ct = default)
    {
        if (_vault is null) throw new InvalidOperationException("Não existe cofre persistido para alterar a senha mestra.");
        _salt = newSalt.ToArray();
        _chave = newKey.ToArray();
        _vault = vault;
        return Task.CompletedTask;
    }

    private void GarantirChave(byte[] key)
    {
        if (_chave is null || key.Length != _chave.Length || !_chave.AsSpan().SequenceEqual(key))
            throw new CryptographicIntegrityException("Chave incorreta (simulação de falha de autenticação).");
    }
}
