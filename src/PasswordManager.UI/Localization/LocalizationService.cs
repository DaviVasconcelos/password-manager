using System;
using Windows.ApplicationModel.Resources;

namespace PasswordManager.UI.Localization;

/// <summary>
/// Implementação baseada em <see cref="ResourceLoader"/> (PRI gerado a
/// partir de <c>Strings/pt-BR/Resources.resw</c> e
/// <c>Strings/en-US/Resources.resw</c>). Usa
/// <c>GetForViewIndependentUse</c> para funcionar fora da thread da UI
/// (ViewModels).
/// </summary>
public sealed class LocalizationService : ILocalizationService
{
    private readonly ResourceLoader? _loader;
    private readonly bool _loaderOk;

    public LocalizationService()
    {
        try
        {
            _loader = ResourceLoader.GetForViewIndependentUse("Resources");
            _loaderOk = _loader is not null;
        }
        catch
        {
            _loaderOk = false;
        }
    }

    /// <inheritdoc/>
    public string GetString(string key)
    {
        if (_loaderOk && _loader is not null)
        {
            try
            {
                var value = _loader.GetString(key);
                if (!string.IsNullOrEmpty(value))
                    return value;

                // Compatibilidade: chaves x:Uid usam separador "." (ex. "Foo.Content"),
                // mas o mapa de recursos interno usa "/" (ex. "Foo/Content").
                var alternativa = key.Replace('.', '/');
                if (!string.Equals(alternativa, key, StringComparison.Ordinal))
                {
                    var altValue = _loader.GetString(alternativa);
                    if (!string.IsNullOrEmpty(altValue))
                        return altValue;
                }
            }
            catch
            {
                // Fallback para a chave se o PRI não estiver disponível.
            }
        }

        return key;
    }

    /// <inheritdoc/>
    public string GetString(string key, params object[] args)
    {
        var format = GetString(key);
        try
        {
            return string.Format(format, args);
        }
        catch (FormatException)
        {
            return format;
        }
    }
}
