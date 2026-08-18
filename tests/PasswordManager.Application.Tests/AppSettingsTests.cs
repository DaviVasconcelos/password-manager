using FluentAssertions;
using PasswordManager.Application.Settings;

namespace PasswordManager.Application.Tests;

public class AppSettingsTests
{
    [Fact]
    public void Default_DeveRetornarValoresPadrao()
    {
        var settings = AppSettings.Default;

        settings.AutoLockTimeoutMinutes.Should().Be(AppSettings.DefaultAutoLockTimeoutMinutes);
        settings.ClipboardCleanTimeSeconds.Should().Be(AppSettings.DefaultClipboardCleanTimeSeconds);
        settings.PasswordGeneratorLength.Should().Be(AppSettings.DefaultPasswordGeneratorLength);
        settings.PasswordGeneratorIncludeLowercase.Should().BeTrue();
        settings.PasswordGeneratorIncludeUppercase.Should().BeTrue();
        settings.PasswordGeneratorIncludeDigits.Should().BeTrue();
        settings.PasswordGeneratorIncludeSymbols.Should().BeTrue();
    }

    [Fact]
    public void Validar_ComConfiguracoesPadrao_DevePassarSemLancar()
    {
        var act = () => AppSettings.Default.Validar();

        act.Should().NotThrow();
    }

    [Fact]
    public void Validar_ComTimeoutDeAutoLockAbaixoDoMinimo_DeveLancarArgumentException()
    {
        var settings = AppSettings.Default with { AutoLockTimeoutMinutes = 0 };

        var act = () => settings.Validar();

        act.Should().Throw<ArgumentException>().WithParameterName(nameof(AppSettings.AutoLockTimeoutMinutes));
    }

    [Fact]
    public void Validar_ComLimpezaDeClipboardAbaixoDoMinimo_DeveLancarArgumentException()
    {
        var settings = AppSettings.Default with { ClipboardCleanTimeSeconds = 3 };

        var act = () => settings.Validar();

        act.Should().Throw<ArgumentException>().WithParameterName(nameof(AppSettings.ClipboardCleanTimeSeconds));
    }

    [Theory]
    [InlineData(7)]
    [InlineData(65)]
    public void Validar_ComTamanhoDeSenhaForaDosLimites_DeveLancarArgumentOutOfRangeException(int tamanho)
    {
        var settings = AppSettings.Default with { PasswordGeneratorLength = tamanho };

        var act = () => settings.Validar();

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(nameof(AppSettings.PasswordGeneratorLength));
    }

    [Fact]
    public void Validar_ComTodasAsClassesDeCaracteresDesabilitadas_DeveLancarArgumentException()
    {
        var settings = AppSettings.Default with
        {
            PasswordGeneratorIncludeLowercase = false,
            PasswordGeneratorIncludeUppercase = false,
            PasswordGeneratorIncludeDigits = false,
            PasswordGeneratorIncludeSymbols = false
        };

        var act = () => settings.Validar();

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Validar_ComConfiguracoesValidasCustomizadas_DevePassarSemLancar()
    {
        var settings = new AppSettings
        {
            AutoLockTimeoutMinutes = 5,
            ClipboardCleanTimeSeconds = 60,
            PasswordGeneratorLength = 16,
            PasswordGeneratorIncludeLowercase = false
        };

        var act = () => settings.Validar();

        act.Should().NotThrow();
    }
}