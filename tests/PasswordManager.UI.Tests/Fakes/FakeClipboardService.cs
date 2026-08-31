using PasswordManager.UI.Services;

namespace PasswordManager.UI.Tests.Fakes;

/// <summary>
/// Fake de <see cref="IClipboardService"/> que armazena o último texto
/// copiado em memória, sem depender de <c>Clipboard</c> WinRT.
/// </summary>
internal sealed class FakeClipboardService : IClipboardService
{
    public string? UltimoTexto { get; private set; }
    public int ChamadasSetText { get; private set; }
    public int ChamadasClear { get; private set; }

    public void SetText(string texto)
    {
        UltimoTexto = texto ?? string.Empty;
        ChamadasSetText++;
    }

    public void Clear()
    {
        UltimoTexto = string.Empty;
        ChamadasClear++;
    }

    public string? GetText() => UltimoTexto;
}
