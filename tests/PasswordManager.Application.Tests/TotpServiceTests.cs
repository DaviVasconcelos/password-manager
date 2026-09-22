using FluentAssertions;
using PasswordManager.Application.Tests.Fakes;
using PasswordManager.Application.Totp;

namespace PasswordManager.Application.Tests;

/// <summary>
/// Testes do <see cref="TotpService"/> (RFC 6238, SHA-1, 30 s, 6 dígitos).
/// Os vetores vêm do Apêndice B da RFC 6238 (secret ASCII
/// "12345678901234567890", SHA-1): o código de 6 dígitos equivale aos
/// 6 últimos dígitos do vetor de 8 dígitos.
/// </summary>
public class TotpServiceTests
{
    // Base32 de "12345678901234567890" (20 bytes).
    private const string SecretRfc = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

    private static DateTimeOffset InstanteDe(long unixSegundos) =>
        DateTimeOffset.FromUnixTimeSeconds(unixSegundos);

    [Theory]
    [InlineData(59L, "287082")] // vetor 94287082
    [InlineData(1111111109L, "081804")] // vetor 07081804
    [InlineData(1111111111L, "050471")] // vetor 14050471
    [InlineData(1234567890L, "005924")] // vetor 89005924
    [InlineData(2000000000L, "279037")] // vetor 69279037
    [InlineData(20000000000L, "353130")] // vetor 65353130
    public void Gerar_ComVetoresRfc6238_DeveGerarCodigosEsperados(long unixSegundos, string esperado)
    {
        var servico = new TotpService();

        var codigo = servico.Gerar(SecretRfc, InstanteDe(unixSegundos));

        codigo.Should().Be(esperado);
    }

    [Fact]
    public void Gerar_ComSecretMinusculoComEspacosEHifens_DeveNormalizar()
    {
        var servico = new TotpService();

        var codigo = servico.Gerar("gezd gnbv-gy3tqojq gezdgnbvgy3tqojq", InstanteDe(59));

        codigo.Should().Be("287082");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ABC!123")]
    [InlineData("=====")]
    public void Gerar_ComSecretInvalido_DeveLancarArgumentException(string? secret)
    {
        var servico = new TotpService();

        var act = () => servico.Gerar(secret!, InstanteDe(59));

        act.Should().Throw<ArgumentException>().WithParameterName("secretBase32");
    }

    [Theory]
    [InlineData(0L, 30)]
    [InlineData(29L, 1)]
    [InlineData(30L, 30)]
    [InlineData(59L, 1)]
    public void SegundosRestantes_ComInstantesFixos_DeveCalcularVirada(long unixSegundos, int esperado)
    {
        var servico = new TotpService();

        servico.SegundosRestantes(InstanteDe(unixSegundos)).Should().Be(esperado);
    }

    [Fact]
    public void Validar_ComCodigoAtual_DeveRetornarTrue()
    {
        var servico = new TotpService();
        var instante = InstanteDe(59);

        servico.Validar(SecretRfc, "287082", instante).Should().BeTrue();
    }

    [Theory]
    [InlineData(-30L)] // passo anterior
    [InlineData(30L)] // passo seguinte
    public void Validar_ComPassoAdjacente_DeveRetornarTrue(long deslocamento)
    {
        var servico = new TotpService();
        var instante = InstanteDe(59 + deslocamento);

        servico.Validar(SecretRfc, "287082", instante).Should().BeTrue();
    }

    [Fact]
    public void Validar_ComDoisPassosDeDistancia_DeveRetornarFalse()
    {
        var servico = new TotpService();

        servico.Validar(SecretRfc, "287082", InstanteDe(59 + 60)).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("abcdef")]
    [InlineData("287083")]
    public void Validar_ComCodigoMalformadoOuErrado_DeveRetornarFalse(string? codigo)
    {
        var servico = new TotpService();

        servico.Validar(SecretRfc, codigo, InstanteDe(59)).Should().BeFalse();
    }

    [Fact]
    public void GerarAgora_ComRelogioFake_DeveUsarInstanteDoRelogio()
    {
        var relogio = new FakeTimeProvider { Instante = InstanteDe(1111111109) };
        var servico = new TotpService(relogio);

        servico.GerarAgora(SecretRfc).Should().Be("081804");
        servico.SegundosRestantesAgora().Should().Be(30 - 1111111109 % 30);
    }
}
