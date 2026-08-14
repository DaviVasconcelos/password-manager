namespace PasswordManager.Application.PasswordGeneration;

/// <summary>
/// Avalia heuristicamente a força de uma senha (comprimento e diversidade
/// de classes de caracteres) para feedback de usuário.
/// </summary>
public interface IPasswordStrengthEvaluator
{
    /// <summary>
    /// Retorna <see cref="ForcaSenha"/> para a senha informada.
    /// </summary>
    ForcaSenha Avaliar(string senha);
}