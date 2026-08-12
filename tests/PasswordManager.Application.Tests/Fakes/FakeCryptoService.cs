using System.Security.Cryptography;
using System.Text;
using PasswordManager.Application.Abstractions;

namespace PasswordManager.Application.Tests.Fakes;

/// <summary>
/// Fake determinístico do <see cref="ICryptoService"/> para os testes de
/// sessão: a derivação de chave é um hash simples (sem custo de Argon2id) e
/// as operações de cifra são identidade. Os testes do repositório/serviço
/// não dependem das primitivas reais.
/// </summary>
internal sealed class FakeCryptoService : ICryptoService
{
    private int _contadorDeSalts;

    public byte[] DeriveKey(string masterPassword, byte[] salt)
    {
        using var sha = SHA256.Create();
        return sha.ComputeHash(Encoding.UTF8.GetBytes(masterPassword).Concat(salt).ToArray());
    }

    public byte[] GenerateSalt()
    {
        _contadorDeSalts++;
        var salt = new byte[16];
        BitConverter.TryWriteBytes(salt, _contadorDeSalts);
        return salt;
    }

    public byte[] Encrypt(byte[] plainData, byte[] key) => plainData.ToArray();

    public byte[] Decrypt(byte[] encryptedPackage, byte[] key) => encryptedPackage.ToArray();
}