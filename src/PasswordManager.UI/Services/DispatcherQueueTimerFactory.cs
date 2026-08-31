using Microsoft.UI.Dispatching;

namespace PasswordManager.UI.Services;

/// <summary>
/// Fábrica real de <see cref="ITimer"/> que cria timers na
/// <c>DispatcherQueue</c> da thread atual (UI thread).
/// </summary>
public sealed class DispatcherQueueTimerFactory : ITimerFactory
{
    /// <inheritdoc/>
    public ITimer Create()
    {
        var inner = DispatcherQueue.GetForCurrentThread().CreateTimer();
        return new DispatcherQueueTimerAdapter(inner);
    }
}
