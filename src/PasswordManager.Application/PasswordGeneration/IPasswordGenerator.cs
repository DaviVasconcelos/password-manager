namespace PasswordManager.Application.PasswordGeneration;

/// <summary>
/// Gera senhas aleatórias criptograficamente seguras, garantindo ao menos
/// um caractere de cada classe habilitada.
/// </summary>
public interface IPasswordGenerator
{
    /// <summary>
    /// Gera uma senha com o comprimento e as classes de caracteres
    /// informadas. Valores padrão: 16 caracteres com todas as classes.
    /// </summary>
    string Generate(int length = 16, bool includeLowercase = true, bool includeUppercase = true,
        bool includeDigits = true, bool includeSymbols = true);
}