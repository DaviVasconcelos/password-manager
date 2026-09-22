using PasswordManager.Application.Totp;

namespace PasswordManager.UI.Tests.Fakes;

/// <summary>
/// Fake de <see cref="ITimeProvider"/> com instante fixo e ajustável,
/// para testar o TOTP sem depender da hora real.
/// </summary>
internal sealed class FakeTimeProvider : ITimeProvider
{
    public FakeTimeProvider(DateTimeOffset? instante = null)
    {
        Instante = instante ?? new DateTimeOffset(2024, 1, 10, 0, 0, 0, TimeSpan.Zero);
    }

    public DateTimeOffset Instante { get; set; }

    public DateTimeOffset AgoraUtc => Instante;
}
