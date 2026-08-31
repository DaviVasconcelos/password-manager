using PasswordManager.UI.Localization;

namespace PasswordManager.UI.Tests.Fakes;

/// <summary>
/// Fake de <see cref="ILocalizationService"/> que usa um dicionário em memória.
/// Quando a chave não existe, retorna a própria chave (comportamento similar ao real).
/// </summary>
internal sealed class FakeLocalizationService : ILocalizationService
{
    private readonly Dictionary<string, string> _mapa;

    public FakeLocalizationService(Dictionary<string, string>? mapa = null)
    {
        _mapa = mapa ?? new Dictionary<string, string>();
    }

    public void Definir(string chave, string valor) => _mapa[chave] = valor;

    public string GetString(string key)
    {
        if (_mapa.TryGetValue(key, out var v))
            return v;

        // Simula chaves de idioma (Settings_Idioma_Opcao_*) ou genéricas — retorna a chave para fallback.
        return key;
    }

    public string GetString(string key, params object[] args)
    {
        var format = GetString(key);
        try { return string.Format(format, args); } catch { return format; }
    }

    public void AplicarIdioma(string idioma) { /* sem efeito em testes */ }
}
