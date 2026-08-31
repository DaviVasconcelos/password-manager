using System;
using Microsoft.UI.Dispatching;

namespace PasswordManager.UI.Services;

/// <summary>
/// Adapta <c>DispatcherQueueTimer</c> para <see cref="ITimer"/>.
/// Usa o <c>DispatcherQueue</c> da thread atual (thread da UI).
/// </summary>
public sealed class DispatcherQueueTimerAdapter : ITimer
{
    private readonly DispatcherQueueTimer _inner;

    public DispatcherQueueTimerAdapter(DispatcherQueueTimer inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _inner.Tick += (s, e) => Tick?.Invoke(s, e);
    }

    /// <inheritdoc/>
    public TimeSpan Interval
    {
        get => _inner.Interval;
        set => _inner.Interval = value;
    }

    /// <inheritdoc/>
    public bool IsRunning => _inner.IsRunning;

    /// <inheritdoc/>
    public event EventHandler<object>? Tick;

    /// <inheritdoc/>
    public void Start() => _inner.Start();

    /// <inheritdoc/>
    public void Stop() => _inner.Stop();
}
