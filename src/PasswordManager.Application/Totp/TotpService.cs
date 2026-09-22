using System.Globalization;
using System.Security.Cryptography;

namespace PasswordManager.Application.Totp;

/// <summary>
/// Implementa o <see cref="ITotpService"/> com BCL pura
/// (<see cref="HMACSHA1"/>), sem pacote NuGet novo: TOTP conforme
/// RFC 6238 com HMAC-SHA1, passo de 30 s e 6 dígitos (truncamento
/// dinâmico da RFC 4226). O relógio é injetado via
/// <see cref="ITimeProvider"/> para testabilidade.
/// </summary>
public sealed class TotpService : ITotpService
{
    /// <summary>
    /// Duração de cada passo em segundos (padrão dos authenticators).
    /// </summary>
    public const int PassoEmSegundos = 30;

    private const int Digitos = 6;
    private const int Modulo = 1_000_000;

    private readonly ITimeProvider _timeProvider;

    public TotpService(ITimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? new SystemTimeProvider();
    }

    public string Gerar(string secretBase32, DateTimeOffset instante)
    {
        var chave = DecodificarSecret(secretBase32, nameof(secretBase32));
        var contador = instante.ToUnixTimeSeconds() / PassoEmSegundos;

        Span<byte> mensagem = stackalloc byte[8];
        for (var i = 7; i >= 0; i--)
        {
            mensagem[i] = (byte)(contador & 0xFF);
            contador >>= 8;
        }

        using var hmac = new HMACSHA1(chave);
        CryptographicOperations.ZeroMemory(chave);
        var hash = hmac.ComputeHash(mensagem.ToArray());

        var deslocamento = hash[^1] & 0x0F;
        var binario = ((hash[deslocamento] & 0x7F) << 24)
            | (hash[deslocamento + 1] << 16)
            | (hash[deslocamento + 2] << 8)
            | hash[deslocamento + 3];

        return (binario % Modulo).ToString($"D{Digitos}", CultureInfo.InvariantCulture);
    }

    public string GerarAgora(string secretBase32) =>
        Gerar(secretBase32, _timeProvider.AgoraUtc);

    public int SegundosRestantes(DateTimeOffset instante)
    {
        var decorridos = instante.ToUnixTimeSeconds() % PassoEmSegundos;
        return PassoEmSegundos - (int)decorridos;
    }

    public int SegundosRestantesAgora() =>
        SegundosRestantes(_timeProvider.AgoraUtc);

    public bool Validar(string secretBase32, string? codigo, DateTimeOffset instante)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            return false;

        var normalizado = codigo.Trim();
        if (normalizado.Length != Digitos || !normalizado.All(char.IsDigit))
            return false;

        // Tolerância de ±1 passo para divergência de relógio (ADR 0009).
        for (var passo = -1; passo <= 1; passo++)
        {
            var candidato = Gerar(secretBase32, instante.AddSeconds(passo * PassoEmSegundos));
            if (CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.ASCII.GetBytes(candidato),
                System.Text.Encoding.ASCII.GetBytes(normalizado)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Normaliza (maiúsculas, ignora espaços/hífens/padding) e decodifica o
    /// secret Base32 (RFC 4648). Aqui valida só o formato; a política de
    /// tamanho mínimo/máximo vive no Domain (momento do salvamento).
    /// </summary>
    private static byte[] DecodificarSecret(string? secret, string nomeParametro)
    {
        if (string.IsNullOrWhiteSpace(secret))
            throw new ArgumentException("Secret TOTP não pode ser vazio.", nomeParametro);

        var normalizado = secret.Trim().ToUpperInvariant()
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .TrimEnd('=');

        if (normalizado.Length == 0)
            throw new ArgumentException("Secret TOTP não pode ser vazio.", nomeParametro);

        foreach (var c in normalizado)
        {
            if (!((c >= 'A' && c <= 'Z') || (c >= '2' && c <= '7')))
                throw new ArgumentException(
                    "Secret TOTP inválido: use apenas caracteres Base32 (A-Z, 2-7).",
                    nomeParametro);
        }

        var bytes = new List<byte>(normalizado.Length * 5 / 8);
        var buffer = 0;
        var bitsNoBuffer = 0;

        foreach (var c in normalizado)
        {
            var valor = c <= '9' ? c - '2' + 26 : c - 'A';
            buffer = (buffer << 5) | valor;
            bitsNoBuffer += 5;

            if (bitsNoBuffer >= 8)
            {
                bitsNoBuffer -= 8;
                bytes.Add((byte)(buffer >> bitsNoBuffer));
                buffer &= (1 << bitsNoBuffer) - 1;
            }
        }

        if (bytes.Count == 0)
            throw new ArgumentException("Secret TOTP inválido: não contém dados.", nomeParametro);

        return bytes.ToArray();
    }
}
