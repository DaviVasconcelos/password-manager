namespace PasswordManager.Application.PasswordGeneration;

/// <summary>
/// Implementa o <see cref="IPasswordStrengthEvaluator"/> com uma heurística
/// simples: combina comprimento com o número de classes de caracteres
/// distintas presentes (minúsculas, maiúsculas, dígitos e símbolos).
/// </summary>
public sealed class PasswordStrengthEvaluator : IPasswordStrengthEvaluator
{
    public ForcaSenha Avaliar(string senha)
    {
        if (string.IsNullOrEmpty(senha))
            return ForcaSenha.Fraca;

        var classes = 0;

        if (senha.Any(char.IsLower)) classes++;
        if (senha.Any(char.IsUpper)) classes++;
        if (senha.Any(char.IsDigit)) classes++;
        if (senha.Any(c => !char.IsLetterOrDigit(c))) classes++;

        if (senha.Length >= 12 && classes >= 3)
            return ForcaSenha.Forte;

        if (senha.Length >= 8 && classes >= 2)
            return ForcaSenha.Media;

        return ForcaSenha.Fraca;
    }
}