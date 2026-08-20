namespace PasswordManager.UI.Localization;

/// <summary>
/// Abstração para acesso a strings localizadas. Permite que ViewModels
/// e code-behind obtenham textos do PRI sem depender diretamente de
/// <c>ResourceLoader</c>, facilitando testes com mocks.
/// </summary>
public interface ILocalizationService
{
    /// <summary>
    /// Obtém a string localizada para a chave informada.
    /// </summary>
    string GetString(string key);

    /// <summary>
    /// Obtém a string localizada e aplica formatação com os argumentos
    /// (usa <c>string.Format</c> sobre o recurso com placeholders {0}, {1}...).
    /// </summary>
    string GetString(string key, params object[] args);
}
