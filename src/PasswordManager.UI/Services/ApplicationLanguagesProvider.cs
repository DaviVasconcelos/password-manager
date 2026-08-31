using System;
using System.Collections.Generic;
using System.Linq;

namespace PasswordManager.UI.Services;

/// <summary>
/// Implementação real de <see cref="IIdiomaProvider"/> que delega para
/// <c>Windows.Globalization.ApplicationLanguages</c> e
/// <c>Microsoft.Windows.Globalization.ApplicationLanguages</c>, com
/// fallback idêntico ao código original de <c>SettingsViewModel</c>
/// e <c>App</c>.
/// </summary>
public sealed class ApplicationLanguagesProvider : IIdiomaProvider
{
    /// <inheritdoc/>
    public IReadOnlyList<string> ManifestLanguages
    {
        get
        {
            // Tenta Windows.Globalization primeiro, cai para Microsoft.Windows.Globalization.
            try
            {
                var manifest = Windows.Globalization.ApplicationLanguages.ManifestLanguages;
                if (manifest != null && manifest.Count > 0)
                    return manifest.ToList();
            }
            catch
            {
                // Ignora e tenta Microsoft.*
            }

            try
            {
                var manifest2 = Microsoft.Windows.Globalization.ApplicationLanguages.ManifestLanguages;
                if (manifest2 != null && manifest2.Count > 0)
                    return manifest2.ToList();
            }
            catch
            {
            }

            return Array.Empty<string>();
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> Languages
    {
        get
        {
            try
            {
                var langs = Windows.Globalization.ApplicationLanguages.Languages;
                if (langs != null && langs.Count > 0)
                    return langs.ToList();
            }
            catch
            {
            }

            try
            {
                var langs2 = Microsoft.Windows.Globalization.ApplicationLanguages.Languages;
                if (langs2 != null && langs2.Count > 0)
                    return langs2.ToList();
            }
            catch
            {
            }

            return Array.Empty<string>();
        }
    }

    /// <inheritdoc/>
    public string PrimaryLanguageOverride
    {
        get
        {
            try
            {
                var v = Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride;
                if (!string.IsNullOrEmpty(v))
                    return v;
            }
            catch
            {
            }

            try
            {
                return Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride ?? string.Empty;
            }
            catch
            {
            }

            return string.Empty;
        }
        set
        {
            var alvo = value ?? string.Empty;

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
        }
    }
}
