namespace PasswordManager.UI.Services;

/// <summary>
/// Abstração sobre a área de transferência do sistema. Encapsula
/// <c>Windows.ApplicationModel.DataTransfer.Clipboard</c> + <c>DataPackage</c>
/// para que ViewModels possam ser testados sem depender de APIs WinRT.
/// </summary>
public interface IClipboardService
{
    /// <summary>
    /// Copia o texto informado para a área de transferência.
    /// </summary>
    void SetText(string texto);

    /// <summary>
    /// Limpa a área de transferência (equivale a copiar string vazia).
    /// </summary>
    void Clear();

    /// <summary>
    /// Obtém o texto atual da área de transferência, se disponível.
    /// Implementações reais podem retornar <c>null</c> quando o acesso
    /// ao clipboard falhar ou não houver texto.
    /// </summary>
    string? GetText();
}
