namespace PasswordManager.UI.Services;

/// <summary>
/// Fábrica de <see cref="ITimer"/>. Abstrai <c>DispatcherQueue.GetForCurrentThread().CreateTimer()</c>
/// para permitir injeção de <c>FakeTimer</c> em testes.
/// </summary>
public interface ITimerFactory
{
    /// <summary>
    /// Cria um novo timer parado, com <c>Interval</c> ainda não definido.
    /// </summary>
    ITimer Create();
}
