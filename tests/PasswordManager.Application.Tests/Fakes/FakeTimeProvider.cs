using PasswordManager.Application.Totp;

namespace PasswordManager.Application.Tests.Fakes;

/// <summary>
/// Fake de <see cref="ITimeProvider"/> com instante fixo e ajustável.
/// </summary>
internal sealed class FakeTimeProvider : ITimeProvider
{
    public DateTimeOffset Instante { get; set; } = new(2024, 1, 10, 0, 0, 0, TimeSpan.Zero);

    public DateTimeOffset AgoraUtc => Instante;
}
