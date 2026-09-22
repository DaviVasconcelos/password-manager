namespace PasswordManager.Application.Totp;

/// <summary>
/// Implementação de <see cref="ITimeProvider"/> que usa o relógio do sistema.
/// </summary>
public sealed class SystemTimeProvider : ITimeProvider
{
    public DateTimeOffset AgoraUtc => DateTimeOffset.UtcNow;
}
