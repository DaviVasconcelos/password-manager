using System;
using System.Linq;
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
    private ResourceLoader? _loader;
    private bool _loaderOk;

    public LocalizationService()
    {
        TentarCriarLoader();
    }

    private void TentarCriarLoader()
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

    /// <inheritdoc/>
    public void AplicarIdioma(string idioma)
    {
        string alvo;
        if (string.Equals(idioma, "auto", StringComparison.OrdinalIgnoreCase))
        {
            var sistema = Windows.Globalization.ApplicationLanguages.Languages.FirstOrDefault();
            alvo = sistema?.Equals("pt-BR", StringComparison.OrdinalIgnoreCase) == true
                ? "pt-BR"
                : "en-US";
        }
        else
        {
            alvo = idioma;
        }

        try
        {
            Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = alvo;
        }
        catch
        {
        }

        try
        {
            Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = alvo;
        }
        catch
        {
        }

        try
        {
            var culture = new System.Globalization.CultureInfo(alvo);
            System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culture;
            System.Globalization.CultureInfo.DefaultThreadCurrentCulture = culture;
        }
        catch
        {
        }

        TentarCriarLoader();
    }
}
