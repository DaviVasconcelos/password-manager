namespace PasswordManager.Application.Totp;

/// <summary>
/// Abstração sobre o relógio para que o TOTP seja testável sem depender
/// da hora real. Produção usa <see cref="SystemTimeProvider"/>; testes
/// usam um fake com instante fixo.
/// </summary>
public interface ITimeProvider
{
    /// <summary>
    /// Instante atual em UTC.
    /// </summary>
    DateTimeOffset AgoraUtc { get; }
}
