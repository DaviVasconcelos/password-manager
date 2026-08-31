using System;

namespace PasswordManager.UI.Services;

/// <summary>
/// Abstração sobre <c>DispatcherQueueTimer</c> para que ViewModels
/// (ex.: <c>VaultViewModel</c>) possam ser testados sem depender da
/// thread da UI. Implementação real delega para <c>DispatcherQueueTimer</c>;
/// em testes usa <c>FakeTimer</c> com disparo manual.
/// </summary>
public interface ITimer
{
    /// <summary>
    /// Intervalo até o próximo disparo. Equivale a <c>DispatcherQueueTimer.Interval</c>.
    /// </summary>
    TimeSpan Interval { get; set; }

    /// <summary>
    /// Indica se o timer está em contagem (após <see cref="Start"/> e antes de <see cref="Stop"/>/Tick).
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Disparado quando o intervalo expira. Equivale a <c>DispatcherQueueTimer.Tick</c>.
    /// </summary>
    event EventHandler<object> Tick;

    /// <summary>
    /// Inicia a contagem.
    /// </summary>
    void Start();

    /// <summary>
    /// Para a contagem.
    /// </summary>
    void Stop();
}
