using System.Collections.Generic;

namespace PasswordManager.UI.Services;

/// <summary>
/// Abstração sobre <c>Windows.Globalization.ApplicationLanguages</c> e
/// <c>Microsoft.Windows.Globalization.ApplicationLanguages</c>. Permite que
/// <c>SettingsViewModel</c> e <c>App</c> sejam testados sem depender de
/// APIs WinRT estáticas.
/// </summary>
public interface IIdiomaProvider
{
    /// <summary>
    /// Idiomas declarados no manifesto do app (PRI). Equivale a
    /// <c>ApplicationLanguages.ManifestLanguages</c> (WinRT + Microsoft).
    /// </summary>
    IReadOnlyList<string> ManifestLanguages { get; }

    /// <summary>
    /// Idiomas preferidos do sistema/usuário. Equivale a
    /// <c>ApplicationLanguages.Languages</c>.
    /// </summary>
    IReadOnlyList<string> Languages { get; }

    /// <summary>
    /// Override do idioma primário do app. Equivale a
    /// <c>ApplicationLanguages.PrimaryLanguageOverride</c>.
    /// Em "auto" deve ser <c>string.Empty</c> para limpar o override.
    /// </summary>
    string PrimaryLanguageOverride { get; set; }
}
