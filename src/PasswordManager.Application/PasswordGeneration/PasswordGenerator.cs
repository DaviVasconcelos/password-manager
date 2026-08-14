using System.Security.Cryptography;

namespace PasswordManager.Application.PasswordGeneration;

/// <summary>
/// Implementa o <see cref="IPasswordGenerator"/> usando
/// <see cref="RandomNumberGenerator"/> (criptograficamente seguro), sem viés
/// de módulo. Garante ao menos um caractere de cada classe habilitada e
/// embaralha o resultado.
/// </summary>
public sealed class PasswordGenerator : IPasswordGenerator
{
    private const string Lowercase = "abcdefghijklmnopqrstuvwxyz";
    private const string Uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Digits = "0123456789";
    private const string Symbols = "!@#$%^&*()-_=+[]{};:,.<>?";

    public string Generate(int length = 20, bool includeLowercase = true, bool includeUppercase = true,
        bool includeDigits = true, bool includeSymbols = true)
    {
        var classes = new List<string>();

        if (includeLowercase) classes.Add(Lowercase);
        if (includeUppercase) classes.Add(Uppercase);
        if (includeDigits) classes.Add(Digits);
        if (includeSymbols) classes.Add(Symbols);

        if (classes.Count == 0)
            throw new ArgumentException("Pelo menos uma classe de caracteres deve estar habilitada.", nameof(length));

        if (length < classes.Count)
            throw new ArgumentOutOfRangeException(nameof(length),
                $"O comprimento deve ser de pelo menos {classes.Count} para incluir uma classe de cada.");

        if (length < 1)
            throw new ArgumentOutOfRangeException(nameof(length), "O comprimento deve ser maior que zero.");

        var resultado = new char[length];
        var indice = 0;

        foreach (var classe in classes)
            resultado[indice++] = classe[RandomNumberGenerator.GetInt32(classe.Length)];

        var todas = string.Concat(classes);

        for (; indice < length; indice++)
            resultado[indice] = todas[RandomNumberGenerator.GetInt32(todas.Length)];

        for (int i = length - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (resultado[i], resultado[j]) = (resultado[j], resultado[i]);
        }

        return new string(resultado);
    }
}