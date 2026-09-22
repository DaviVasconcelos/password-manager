namespace PasswordManager.Application.Totp;

/// <summary>
/// Gera e valida códigos TOTP (RFC 6238, SHA-1, passo de 30 s, 6 dígitos)
/// a partir do secret Base32 guardado no item do cofre (ADR 0009).
/// </summary>
public interface ITotpService
{
    /// <summary>
    /// Gera o código de 6 dígitos válido no instante informado.
    /// </summary>
    string Gerar(string secretBase32, DateTimeOffset instante);

    /// <summary>
    /// Gera o código de 6 dígitos válido agora (relógio injetado).
    /// </summary>
    string GerarAgora(string secretBase32);

    /// <summary>
    /// Segundos restantes até a virada do passo (1 a 30).
    /// </summary>
    int SegundosRestantes(DateTimeOffset instante);

    /// <summary>
    /// Segundos restantes até a virada do passo, agora (relógio injetado).
    /// </summary>
    int SegundosRestantesAgora();

    /// <summary>
    /// Valida um código com tolerância de ±1 passo (divergência de relógio).
    /// Retorna falso para código malformado; lança exceção para secret inválido.
    /// </summary>
    bool Validar(string secretBase32, string? codigo, DateTimeOffset instante);
}
