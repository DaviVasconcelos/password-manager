using PasswordManager.UI.Services;

namespace PasswordManager.UI.Tests.Fakes;

/// <summary>
/// Fake de <see cref="IIdiomaProvider"/> configurável por teste.
/// </summary>
internal sealed class FakeIdiomaProvider : IIdiomaProvider
{
    public FakeIdiomaProvider(
        IReadOnlyList<string>? manifestLanguages = null,
        IReadOnlyList<string>? languages = null,
        string primaryLanguageOverride = "")
    {
        ManifestLanguages = manifestLanguages ?? Array.Empty<string>();
        Languages = languages ?? Array.Empty<string>();
        PrimaryLanguageOverride = primaryLanguageOverride;
    }

    public IReadOnlyList<string> ManifestLanguages { get; set; }

    public IReadOnlyList<string> Languages { get; set; }

    public string PrimaryLanguageOverride { get; set; }
}
