using PasswordManager.Application.PasswordGeneration;
using FluentAssertions;

namespace PasswordManager.Application.Tests;

public class PasswordStrengthEvaluatorTests
{
    private readonly PasswordStrengthEvaluator _evaluator = new();

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("abcdefgh")]       // 8 caracteres, apenas 1 classe
    [InlineData("12345678901234")] // longa, porém apenas dígitos
    public void Avaliar_SenhaComPoucaDiversidade_DeveRetornarFraca(string senha)
    {
        _evaluator.Avaliar(senha).Should().Be(ForcaSenha.Fraca);
    }

    [Theory]
    [InlineData("abcdefgh1")] // 9 chars, minúsculas + dígitos
    [InlineData("Abcdefgh")]  // 8 chars, maiúsculas + minúsculas
    public void Avaliar_SenhaDeComprimentoEOuClassesMedios_DeveRetornarMedia(string senha)
    {
        _evaluator.Avaliar(senha).Should().Be(ForcaSenha.Media);
    }

    [Theory]
    [InlineData("Abcdefghij1!")]    // 12 chars, 4 classes
    [InlineData("Senha-Forte-2025")] // 16 chars, 4 classes
    public void Avaliar_SenhaLongaEComClassesVariadas_DeveRetornarForte(string senha)
    {
        _evaluator.Avaliar(senha).Should().Be(ForcaSenha.Forte);
    }
}