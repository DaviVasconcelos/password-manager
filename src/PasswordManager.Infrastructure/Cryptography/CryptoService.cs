using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using PasswordManager.Application.Abstractions;
using PasswordManager.Application.Exceptions;

namespace PasswordManager.Infrastructure.Cryptography;

/// <summary>
/// Implementação do <see cref="ICryptoService"/> usando Argon2id para
/// derivação de chave e AES-256-GCM para criptografia autenticada.
/// </summary>
public sealed class CryptoService : ICryptoService
{
    private const int KeySizeInBytes = 32;
    private const int SaltSizeInBytes = 16;
    private const int NonceSizeInBytes = 12;
    private const int TagSizeInBytes = 16;
    private const int MinimumSaltSizeInBytes = 8;

    private readonly int _argon2MemorySizeInKb;
    private readonly int _argon2Iterations;
    private readonly int _argon2DegreeOfParallelism;

    public CryptoService(
        int argon2MemorySizeInKb = 64 * 1024,
        int argon2Iterations = 3,
        int argon2DegreeOfParallelism = 4)
    {
        _argon2MemorySizeInKb = argon2MemorySizeInKb;
        _argon2Iterations = argon2Iterations;
        _argon2DegreeOfParallelism = argon2DegreeOfParallelism;
    }

    public byte[] DeriveKey(string masterPassword, byte[] salt)
    {
        if (masterPassword is null)
            throw new ArgumentNullException(nameof(masterPassword));

        if (masterPassword.Length == 0)
            throw new ArgumentException("A senha mestra não pode ser vazia.", nameof(masterPassword));

        ArgumentNullException.ThrowIfNull(salt);

        if (salt.Length < MinimumSaltSizeInBytes)
            throw new ArgumentException(
                $"O salt deve ter pelo menos {MinimumSaltSizeInBytes} bytes.", nameof(salt));

        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(masterPassword))
        {
            Salt = salt,
            DegreeOfParallelism = _argon2DegreeOfParallelism,
            MemorySize = _argon2MemorySizeInKb,
            Iterations = _argon2Iterations
        };

        return argon2.GetBytes(KeySizeInBytes);
    }

    public byte[] GenerateSalt() => RandomNumberGenerator.GetBytes(SaltSizeInBytes);

    public byte[] Encrypt(byte[] plainData, byte[] key)
    {
        ArgumentNullException.ThrowIfNull(plainData);
        ValidateKey(key);

        var nonce = RandomNumberGenerator.GetBytes(NonceSizeInBytes);
        var ciphertext = new byte[plainData.Length];
        var tag = new byte[TagSizeInBytes];

        using (var aes = new AesGcm(key, TagSizeInBytes))
        {
            aes.Encrypt(nonce, plainData, ciphertext, tag);
        }

        var package = new byte[NonceSizeInBytes + TagSizeInBytes + ciphertext.Length];
        nonce.CopyTo(package, 0);
        tag.CopyTo(package, NonceSizeInBytes);
        ciphertext.CopyTo(package, NonceSizeInBytes + TagSizeInBytes);

        return package;
    }

    public byte[] Decrypt(byte[] encryptedPackage, byte[] key)
    {
        ArgumentNullException.ThrowIfNull(encryptedPackage);
        ValidateKey(key);

        if (encryptedPackage.Length < NonceSizeInBytes + TagSizeInBytes)
            throw new CryptographicIntegrityException(
                "Pacote criptografado inválido: tamanho insuficiente para conter nonce e tag.");

        var nonce = encryptedPackage.AsSpan(0, NonceSizeInBytes);
        var tag = encryptedPackage.AsSpan(NonceSizeInBytes, TagSizeInBytes);
        var ciphertext = encryptedPackage.AsSpan(NonceSizeInBytes + TagSizeInBytes);
        var plainData = new byte[ciphertext.Length];

        try
        {
            using var aes = new AesGcm(key, TagSizeInBytes);
            aes.Decrypt(nonce, ciphertext, tag, plainData);
        }
        catch (AuthenticationTagMismatchException ex)
        {
            throw new CryptographicIntegrityException(ex.Message, ex);
        }

        return plainData;
    }

    private static void ValidateKey(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (key.Length != KeySizeInBytes)
            throw new ArgumentException(
                $"A chave deve ter {KeySizeInBytes} bytes (AES-256).", nameof(key));
    }
}
