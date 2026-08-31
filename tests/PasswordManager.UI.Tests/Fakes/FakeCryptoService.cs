using System.Security.Cryptography;
using System.Text;
using PasswordManager.Application.Abstractions;

namespace PasswordManager.UI.Tests.Fakes;

/// <summary>
/// Fake determinístico de <see cref="ICryptoService"/> sem custo de Argon2id.
/// </summary>
internal sealed class FakeCryptoService : ICryptoService
{
    private int _contador;

    public byte[] DeriveKey(string masterPassword, byte[] salt)
    {
        using var sha = SHA256.Create();
        return sha.ComputeHash(Encoding.UTF8.GetBytes(masterPassword).Concat(salt).ToArray());
    }

    public byte[] GenerateSalt()
    {
        _contador++;
        var salt = new byte[16];
        BitConverter.TryWriteBytes(salt, _contador);
        return salt;
    }

    public byte[] Encrypt(byte[] plainData, byte[] key) => plainData.ToArray();
    public byte[] Decrypt(byte[] encryptedPackage, byte[] key) => encryptedPackage.ToArray();
}
