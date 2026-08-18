using FluentAssertions;
using PasswordManager.Application.Settings;
using PasswordManager.Infrastructure.Settings;

namespace PasswordManager.Infrastructure.Tests;

public class AppSettingsServiceTests
{
    [Fact]
    public void Get_QuandoArquivoNaoExiste_DeveRetornarConfiguracoesPadrao()
    {
        var servico = new AppSettingsService(CriarCaminhoTemporario());

        var settings = servico.Get();

        settings.Should().Be(AppSettings.Default);
    }

    [Fact]
    public async Task SaveAsync_ComConfiguracoesValidas_DevePersistirArquivoEAtualizarCache()
    {
        var caminho = CriarCaminhoTemporario();
        var servico = new AppSettingsService(caminho);
        var novas = new AppSettings
        {
            AutoLockTimeoutMinutes = 5,
            ClipboardCleanTimeSeconds = 60,
            PasswordGeneratorLength = 16
        };

        await servico.SaveAsync(novas);

        File.Exists(caminho).Should().BeTrue();
        servico.Get().Should().Be(novas);

        var recarregado = new AppSettingsService(caminho).Get();
        recarregado.Should().Be(novas);
    }

    [Fact]
    public async Task SaveAsync_ComConfiguracoesInvalidas_DeveLancarArgumentExceptionESemCriarArquivo()
    {
        var caminho = CriarCaminhoTemporario();
        var servico = new AppSettingsService(caminho);
        var invalidas = AppSettings.Default with { AutoLockTimeoutMinutes = 0 };

        var act = async () => await servico.SaveAsync(invalidas);

        await act.Should().ThrowAsync<ArgumentException>();
        File.Exists(caminho).Should().BeFalse();
    }

    [Fact]
    public void Get_QuandoArquivoCorrompido_DeveRetornarConfiguracoesPadrao()
    {
        var caminho = CriarCaminhoTemporario("{isto não é um json válido");

        var servico = new AppSettingsService(caminho);

        servico.Get().Should().Be(AppSettings.Default);
    }

    [Fact]
    public void Get_QuandoArquivoComValoresInvalidos_DeveRetornarConfiguracoesPadrao()
    {
        var caminho = CriarCaminhoTemporario("""{"autoLockTimeoutMinutes":0}""");

        var servico = new AppSettingsService(caminho);

        servico.Get().Should().Be(AppSettings.Default);
    }

    [Fact]
    public void Get_QuandoArquivoValido_DeveRetornarConfiguracoesPersistidas()
    {
        var caminho = CriarCaminhoTemporario(
            """{"autoLockTimeoutMinutes":10,"clipboardCleanTimeSeconds":60,"passwordGeneratorLength":24}""");

        var servico = new AppSettingsService(caminho);

        var settings = servico.Get();
        settings.AutoLockTimeoutMinutes.Should().Be(10);
        settings.ClipboardCleanTimeSeconds.Should().Be(60);
        settings.PasswordGeneratorLength.Should().Be(24);
    }

    private static string CriarCaminhoTemporario(string? conteudo = null)
    {
        var caminho = Path.Combine(Path.GetTempPath(), $"settings-{Guid.NewGuid():N}.json");

        if (conteudo is not null)
            File.WriteAllText(caminho, conteudo);

        return caminho;
    }
}