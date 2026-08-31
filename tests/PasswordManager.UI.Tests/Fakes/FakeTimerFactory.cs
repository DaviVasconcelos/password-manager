using PasswordManager.UI.Services;
using ITimer = PasswordManager.UI.Services.ITimer;
using ITimerFactory = PasswordManager.UI.Services.ITimerFactory;

namespace PasswordManager.UI.Tests.Fakes;

/// <summary>
/// Fábrica fake que cria <see cref="FakeTimer"/> sob demanda e retém
/// todos os timers criados para inspeção nos testes.
/// </summary>
internal sealed class FakeTimerFactory : ITimerFactory
{
    private readonly List<FakeTimer> _timers = new();

    public IReadOnlyList<FakeTimer> Timers => _timers;

    /// <summary>
    /// Timers específicos usados por <c>VaultViewModel</c> (criados na ordem):
    /// 0 = clipboard, 1 = inatividade, 2 = info banner.
    /// </summary>
    public FakeTimer TimerClipboard => _timers.Count > 0 ? _timers[0] : throw new InvalidOperationException("Nenhum timer criado ainda.");
    public FakeTimer TimerInatividade => _timers.Count > 1 ? _timers[1] : throw new InvalidOperationException("Timer de inatividade ainda não criado.");
    public FakeTimer TimerInfoBanner => _timers.Count > 2 ? _timers[2] : throw new InvalidOperationException("Timer de banner ainda não criado.");

    public ITimer Create()
    {
        var timer = new FakeTimer();
        _timers.Add(timer);
        return timer;
    }
}
