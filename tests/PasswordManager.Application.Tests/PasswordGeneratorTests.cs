using PasswordManager.Application.PasswordGeneration;
using FluentAssertions;

namespace PasswordManager.Application.Tests;

public class PasswordGeneratorTests
{
    private readonly PasswordGenerator _generator = new();

    [Fact]
    public void Generate_ComComprimentoPadrao_DeveRetornarSenhaComTamanhoExato()
    {
        var senha = _generator.Generate();

        senha.Should().HaveLength(20);
    }

    [Fact]
    public void Generate_ComTodasAsClasses_DeveIncluirAoMenosUmaDeCada()
    {
        var senha = _generator.Generate(length: 24);

        senha.Any(char.IsLower).Should().BeTrue();
        senha.Any(char.IsUpper).Should().BeTrue();
        senha.Any(char.IsDigit).Should().BeTrue();
        senha.Any(c => !char.IsLetterOrDigit(c)).Should().BeTrue();
    }

    [Fact]
    public void Generate_ComApenasDigitos_DeveRetornarApenasDigitos()
    {
        var senha = _generator.Generate(
            length: 16, includeLowercase: false, includeUppercase: false,
            includeDigits: true, includeSymbols: false);

        senha.Should().HaveLength(16);
        senha.All(char.IsDigit).Should().BeTrue();
    }

    [Fact]
    public void Generate_ComComprimentoMenorQueNumeroDeClasses_DeveLancarArgumentOutOfRangeException()
    {
        var act = () => _generator.Generate(length: 2);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Generate_ComComprimentoZero_DeveLancarArgumentOutOfRangeException()
    {
        var act = () => _generator.Generate(length: 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Generate_SemClassesHabilitadas_DeveLancarArgumentException()
    {
        var act = () => _generator.Generate(
            includeLowercase: false, includeUppercase: false,
            includeDigits: false, includeSymbols: false);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Generate_ChamadoDuasVezes_NaoDeveRetornarSenhasIguais()
    {
        var primeira = _generator.Generate();
        var segunda = _generator.Generate();

        primeira.Should().NotBe(segunda);
    }
}