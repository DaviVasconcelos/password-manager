using System.Text;
using FluentAssertions;
using PasswordManager.Application.Exceptions;
using PasswordManager.Infrastructure.Cryptography;

namespace PasswordManager.Infrastructure.Tests;

public class CryptoServiceTests
{
    private const int KeySizeInBytes = 32;
    private const int SaltSizeInBytes = 16;
    private const int NonceSizeInBytes = 12;
    private const int TagSizeInBytes = 16;

    private readonly CryptoService _cryptoService = new(
        argon2MemorySizeInKb: 32,
        argon2Iterations: 1,
        argon2DegreeOfParallelism: 1);

    [Fact]
    public void DeriveKey_ComMesmaSenhaEMesmoSalt_DeveRetornarMesmaChave()
    {
        var salt = _cryptoService.GenerateSalt();

        var primeiraChave = _cryptoService.DeriveKey("senha-mestra", salt);
        var segundaChave = _cryptoService.DeriveKey("senha-mestra", salt);

        primeiraChave.Should().Equal(segundaChave);
    }

    [Fact]
    public void DeriveKey_ComSaltDiferente_DeveRetornarChaveDiferente()
    {
        var chaveComSaltA = _cryptoService.DeriveKey("senha-mestra", _cryptoService.GenerateSalt());
        var chaveComSaltB = _cryptoService.DeriveKey("senha-mestra", _cryptoService.GenerateSalt());

        chaveComSaltA.Should().NotEqual(chaveComSaltB);
    }

    [Fact]
    public void DeriveKey_ComSenhaDiferente_DeveRetornarChaveDiferente()
    {
        var salt = _cryptoService.GenerateSalt();

        var chaveComSenhaA = _cryptoService.DeriveKey("senha-a", salt);
        var chaveComSenhaB = _cryptoService.DeriveKey("senha-b", salt);

        chaveComSenhaA.Should().NotEqual(chaveComSenhaB);
    }

    [Fact]
    public void DeriveKey_DeveRetornarChaveCom32Bytes()
    {
        var chave = _cryptoService.DeriveKey("senha-mestra", _cryptoService.GenerateSalt());

        chave.Should().HaveCount(KeySizeInBytes);
    }

    [Fact]
    public void DeriveKey_ComSenhaNula_DeveLancarArgumentNullException()
    {
        var act = () => _cryptoService.DeriveKey(null!, _cryptoService.GenerateSalt());

        act.Should().ThrowExactly<ArgumentNullException>().WithParameterName("masterPassword");
    }

    [Fact]
    public void DeriveKey_ComSenhaVazia_DeveLancarArgumentException()
    {
        var act = () => _cryptoService.DeriveKey(string.Empty, _cryptoService.GenerateSalt());

        act.Should().ThrowExactly<ArgumentException>()
            .WithParameterName("masterPassword");
    }

    [Fact]
    public void DeriveKey_ComSaltNulo_DeveLancarArgumentNullException()
    {
        var act = () => _cryptoService.DeriveKey("senha-mestra", null!);

        act.Should().ThrowExactly<ArgumentNullException>().WithParameterName("salt");
    }

    [Fact]
    public void DeriveKey_ComSaltCurtoDemais_DeveLancarArgumentException()
    {
        var saltCurto = new byte[4];

        var act = () => _cryptoService.DeriveKey("senha-mestra", saltCurto);

        act.Should().ThrowExactly<ArgumentException>()
            .WithParameterName("salt");
    }

    [Fact]
    public void GenerateSalt_DeveRetornarSaltCom16Bytes()
    {
        var salt = _cryptoService.GenerateSalt();

        salt.Should().HaveCount(SaltSizeInBytes);
    }

    [Fact]
    public void GenerateSalt_ChamadoDuasVezes_DeveGerarSaisDistintos()
    {
        var primeiroSalt = _cryptoService.GenerateSalt();
        var segundoSalt = _cryptoService.GenerateSalt();

        primeiroSalt.Should().NotEqual(segundoSalt);
    }

    [Fact]
    public void Encrypt_ComDadosValidos_DeveRetornarPacoteComNonceTagECiphertext()
    {
        var dados = Encoding.UTF8.GetBytes("dados sensíveis");
        var chave = _cryptoService.DeriveKey("senha-mestra", _cryptoService.GenerateSalt());

        var pacote = _cryptoService.Encrypt(dados, chave);

        pacote.Should().HaveCount(NonceSizeInBytes + TagSizeInBytes + dados.Length);
    }

    [Fact]
    public void Encrypt_ChamadoDuasVezes_DeveGerarPacotesDistintos()
    {
        var dados = Encoding.UTF8.GetBytes("dados sensíveis");
        var chave = _cryptoService.DeriveKey("senha-mestra", _cryptoService.GenerateSalt());

        var primeiroPacote = _cryptoService.Encrypt(dados, chave);
        var segundoPacote = _cryptoService.Encrypt(dados, chave);

        primeiroPacote.Should().NotEqual(segundoPacote);
    }

    [Fact]
    public void Encrypt_ComDadosNulos_DeveLancarArgumentNullException()
    {
        var chave = _cryptoService.DeriveKey("senha-mestra", _cryptoService.GenerateSalt());

        var act = () => _cryptoService.Encrypt(null!, chave);

        act.Should().ThrowExactly<ArgumentNullException>().WithParameterName("plainData");
    }

    [Fact]
    public void Encrypt_ComChaveNula_DeveLancarArgumentNullException()
    {
        var act = () => _cryptoService.Encrypt(new byte[] { 1, 2, 3 }, null!);

        act.Should().ThrowExactly<ArgumentNullException>().WithParameterName("key");
    }

    [Fact]
    public void Encrypt_ComChaveDeTamanhoInvalido_DeveLancarArgumentException()
    {
        var chaveInvalida = new byte[16];

        var act = () => _cryptoService.Encrypt(new byte[] { 1, 2, 3 }, chaveInvalida);

        act.Should().ThrowExactly<ArgumentException>()
            .WithParameterName("key")
            .WithMessage($"*{KeySizeInBytes}*");
    }

    [Fact]
    public void Decrypt_ComChaveCorreta_DeveRetornarDadosOriginais()
    {
        var dados = Encoding.UTF8.GetBytes("dados sensíveis");
        var chave = _cryptoService.DeriveKey("senha-mestra", _cryptoService.GenerateSalt());
        var pacote = _cryptoService.Encrypt(dados, chave);

        var dadosDescriptografados = _cryptoService.Decrypt(pacote, chave);

        dadosDescriptografados.Should().Equal(dados);
    }

    [Fact]
    public void Decrypt_ComChaveErrada_DeveLancarCryptographicIntegrityException()
    {
        var dados = Encoding.UTF8.GetBytes("dados sensíveis");
        var salt = _cryptoService.GenerateSalt();
        var chaveCorreta = _cryptoService.DeriveKey("senha-mestra", salt);
        var chaveErrada = _cryptoService.DeriveKey("senha-errada", salt);
        var pacote = _cryptoService.Encrypt(dados, chaveCorreta);

        var act = () => _cryptoService.Decrypt(pacote, chaveErrada);

        act.Should().ThrowExactly<CryptographicIntegrityException>();
    }

    [Fact]
    public void Decrypt_ComPacoteAdulteradoNoCiphertext_DeveLancarCryptographicIntegrityException()
    {
        var dados = Encoding.UTF8.GetBytes("dados sensíveis");
        var chave = _cryptoService.DeriveKey("senha-mestra", _cryptoService.GenerateSalt());
        var pacote = _cryptoService.Encrypt(dados, chave);
        pacote[^1] ^= 0xFF;

        var act = () => _cryptoService.Decrypt(pacote, chave);

        act.Should().ThrowExactly<CryptographicIntegrityException>();
    }

    [Fact]
    public void Decrypt_ComPacoteAdulteradoNaTag_DeveLancarCryptographicIntegrityException()
    {
        var dados = Encoding.UTF8.GetBytes("dados sensíveis");
        var chave = _cryptoService.DeriveKey("senha-mestra", _cryptoService.GenerateSalt());
        var pacote = _cryptoService.Encrypt(dados, chave);
        pacote[NonceSizeInBytes] ^= 0xFF;

        var act = () => _cryptoService.Decrypt(pacote, chave);

        act.Should().ThrowExactly<CryptographicIntegrityException>();
    }

    [Fact]
    public void Decrypt_ComPacoteTruncado_DeveLancarCryptographicIntegrityException()
    {
        var dados = Encoding.UTF8.GetBytes("dados sensíveis");
        var chave = _cryptoService.DeriveKey("senha-mestra", _cryptoService.GenerateSalt());
        var pacote = _cryptoService.Encrypt(dados, chave);
        var pacoteTruncado = pacote.Take(NonceSizeInBytes).ToArray();

        var act = () => _cryptoService.Decrypt(pacoteTruncado, chave);

        act.Should().ThrowExactly<CryptographicIntegrityException>();
    }

    [Fact]
    public void Decrypt_ComPacoteNulo_DeveLancarArgumentNullException()
    {
        var chave = _cryptoService.DeriveKey("senha-mestra", _cryptoService.GenerateSalt());

        var act = () => _cryptoService.Decrypt(null!, chave);

        act.Should().ThrowExactly<ArgumentNullException>().WithParameterName("encryptedPackage");
    }

    [Fact]
    public void Decrypt_ComChaveDeTamanhoInvalido_DeveLancarArgumentException()
    {
        var chaveInvalida = new byte[16];

        var act = () => _cryptoService.Decrypt(new byte[32], chaveInvalida);

        act.Should().ThrowExactly<ArgumentException>()
            .WithParameterName("key");
    }

    [Fact]
    public void RoundTrip_ComChaveDerivadaDoArgon2_DevePreservarDadosJson()
    {
        var dadosJson = """
            {"titulo":"GitHub","usuario":"meu_user","url":"https://github.com"}
            """;
        var salt = _cryptoService.GenerateSalt();
        var chave = _cryptoService.DeriveKey("senha-mestra", salt);

        var pacote = _cryptoService.Encrypt(Encoding.UTF8.GetBytes(dadosJson), chave);
        var dadosDescriptografados = _cryptoService.Decrypt(pacote, chave);

        Encoding.UTF8.GetString(dadosDescriptografados).Should().Be(dadosJson);
    }
}
