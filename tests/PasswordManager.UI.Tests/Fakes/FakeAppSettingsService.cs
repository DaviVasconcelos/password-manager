using PasswordManager.Application.Settings;

namespace PasswordManager.UI.Tests.Fakes;

/// <summary>
/// Fake em memória de <see cref="IAppSettingsService"/>.
/// </summary>
internal sealed class FakeAppSettingsService : IAppSettingsService
{
    private AppSettings _settings;

    public FakeAppSettingsService(AppSettings? inicial = null)
    {
        _settings = inicial ?? AppSettings.Default;
    }

    public int ChamadasSaveAsync { get; private set; }
    public AppSettings? UltimoSalvo { get; private set; }

    public AppSettings Get() => _settings;

    public Task SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validar();
        _settings = settings;
        UltimoSalvo = settings;
        ChamadasSaveAsync++;
        return Task.CompletedTask;
    }
}
