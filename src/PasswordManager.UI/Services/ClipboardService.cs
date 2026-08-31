using Windows.ApplicationModel.DataTransfer;

namespace PasswordManager.UI.Services;

/// <summary>
/// Implementação real de <see cref="IClipboardService"/> usando
/// <c>DataPackage</c> e <c>Clipboard</c> do WinRT. Deve ser registrada
/// como singleton na DI.
/// </summary>
public sealed class ClipboardService : IClipboardService
{
    /// <inheritdoc/>
    public void SetText(string texto)
    {
        var pacote = new DataPackage();
        pacote.SetText(texto ?? string.Empty);
        Clipboard.SetContent(pacote);
    }

    /// <inheritdoc/>
    public void Clear()
    {
        var pacote = new DataPackage();
        pacote.SetText(string.Empty);
        Clipboard.SetContent(pacote);
    }

    /// <inheritdoc/>
    public string? GetText()
    {
        // Leitura do clipboard é assíncrona (IAsyncOperation) e raramente
        // necessária em produção — o FakeClipboardService em testes retorna
        // o último valor escrito. Evita dependência de AsTask/WinRT async.
        return null;
    }
}
