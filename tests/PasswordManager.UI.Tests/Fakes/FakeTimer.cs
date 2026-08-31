using PasswordManager.UI.Services;
using ITimer = PasswordManager.UI.Services.ITimer;

namespace PasswordManager.UI.Tests.Fakes;

/// <summary>
/// Fake de <see cref="ITimer"/> com disparo manual. Permite que testes de
/// <c>VaultViewModel</c> avancem o tempo sem depender de <c>DispatcherQueue</c>.
/// </summary>
internal sealed class FakeTimer : ITimer
{
    private TimeSpan _interval;
    private bool _isRunning;

    public TimeSpan Interval
    {
        get => _interval;
        set => _interval = value;
    }

    public bool IsRunning => _isRunning;

    public event EventHandler<object>? Tick;

    public int ChamadasStart { get; private set; }
    public int ChamadasStop { get; private set; }

    public void Start()
    {
        ChamadasStart++;
        _isRunning = true;
    }

    public void Stop()
    {
        ChamadasStop++;
        _isRunning = false;
    }

    /// <summary>
    /// Dispara o evento <see cref="Tick"/> manualmente, simulando a expiração do intervalo.
    /// </summary>
    public void DispararTick()
    {
        Tick?.Invoke(this, new object());
    }
}
