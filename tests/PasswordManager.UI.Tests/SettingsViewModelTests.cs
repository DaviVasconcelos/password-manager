using FluentAssertions;
using PasswordManager.Application.Settings;
using PasswordManager.UI.Tests.Fakes;
using PasswordManager.UI.ViewModels;

namespace PasswordManager.UI.Tests;

/// <summary>
/// Testes de <see cref="SettingsViewModel"/> com <see cref="FakeIdiomaProvider"/>.
/// </summary>
public class SettingsViewModelTests
{
    private static FakeLocalizationService CriarLoc()
    {
        return new FakeLocalizationService(new Dictionary<string, string>
        {
            ["Settings_Idioma_Opcao_Auto"] = "Automático",
            ["Settings_Tema_Opcao_Sistema"] = "Sistema",
            ["Settings_Tema_Opcao_Claro"] = "Claro",
            ["Settings_Tema_Opcao_Escuro"] = "Escuro",
            ["ItemEditor_Tamanho_Sufixo"] = "caracteres"
        });
    }

    private static AppSettings SettingsPadrao() => new()
    {
        AutoLockTimeoutMinutes = 2,
        ClipboardCleanTimeSeconds = 30,
        PasswordGeneratorLength = 16,
        PasswordGeneratorIncludeLowercase = true,
        PasswordGeneratorIncludeUppercase = true,
        PasswordGeneratorIncludeDigits = true,
        PasswordGeneratorIncludeSymbols = false,
        Idioma = AppSettings.IdiomaAuto,
        Tema = AppSettings.TemaSistema
    };

    [Fact]
    public void Construtor_ComManifestVazio_DeveGarantirPtBrEEnUs()
    {
        var svc = new FakeAppSettingsService(SettingsPadrao());
        var loc = CriarLoc();
        var idioma = new FakeIdiomaProvider(manifestLanguages: Array.Empty<string>());

        var vm = new SettingsViewModel(svc, loc, idioma);

        vm.OpcoesIdioma.Should().Contain(o => o.Codigo == "pt-BR");
        vm.OpcoesIdioma.Should().Contain(o => o.Codigo == "en-US");
        vm.OpcoesIdioma.Should().Contain(o => o.Codigo == "auto");
        // auto + pt-BR + en-US = 3
        vm.OpcoesIdioma.Should().HaveCount(3);
    }

    [Fact]
    public void Construtor_ComManifestComIds_DeveConstruirOpcoesOrdenadas()
    {
        var svc = new FakeAppSettingsService(SettingsPadrao());
        var loc = CriarLoc();
        var idioma = new FakeIdiomaProvider(manifestLanguages: new[] { "pt-BR", "en-US", "es-ES" });

        var vm = new SettingsViewModel(svc, loc, idioma);

        vm.OpcoesIdioma.Should().HaveCount(4); // auto + 3
        vm.OpcoesIdioma[0].Codigo.Should().Be("auto");
        // Ordenado ordinalIgnoreCase: en-US, es-ES, pt-BR
        vm.OpcoesIdioma[1].Codigo.Should().Be("en-US");
        vm.OpcoesIdioma[2].Codigo.Should().Be("es-ES");
        vm.OpcoesIdioma[3].Codigo.Should().Be("pt-BR");
        vm.OpcoesTema.Should().HaveCount(3);
        vm.OpcoesTema.Select(o => o.Codigo).Should().Contain(AppSettings.TemaSistema);
        vm.OpcoesTema.Select(o => o.Codigo).Should().Contain(AppSettings.TemaClaro);
        vm.OpcoesTema.Select(o => o.Codigo).Should().Contain(AppSettings.TemaEscuro);
    }

    [Fact]
    public void Carregar_DevePreencherCamposComSettingsPersistidos()
    {
        var settings = new AppSettings
        {
            AutoLockTimeoutMinutes = 5,
            ClipboardCleanTimeSeconds = 60,
            PasswordGeneratorLength = 20,
            PasswordGeneratorIncludeLowercase = false,
            PasswordGeneratorIncludeUppercase = true,
            PasswordGeneratorIncludeDigits = false,
            PasswordGeneratorIncludeSymbols = true,
            Idioma = "en-US",
            Tema = AppSettings.TemaEscuro
        };
        var svc = new FakeAppSettingsService(settings);
        var loc = CriarLoc();
        var idioma = new FakeIdiomaProvider(manifestLanguages: new[] { "pt-BR", "en-US" }, languages: new[] { "en-US" });

        var vm = new SettingsViewModel(svc, loc, idioma);
        vm.Carregar();

        vm.TimeoutAutoLockMinutes.Should().Be(5);
        vm.ClipboardCleanTimeSeconds.Should().Be(60);
        vm.PasswordGeneratorLength.Should().Be(20);
        vm.IncludeLowercase.Should().BeFalse();
        vm.IncludeUppercase.Should().BeTrue();
        vm.IncludeDigits.Should().BeFalse();
        vm.IncludeSymbols.Should().BeTrue();
        vm.IdiomaSelecionado!.Codigo.Should().Be("en-US");
        vm.TemaSelecionado!.Codigo.Should().Be(AppSettings.TemaEscuro);
        vm.RequerReinicio.Should().BeFalse();
        vm.Erro.Should().BeNull();
    }

    [Fact]
    public void TamanhoSenhaTexto_DeveFormatarComSufixoLocalizado()
    {
        var svc = new FakeAppSettingsService(SettingsPadrao());
        var loc = CriarLoc();
        var idioma = new FakeIdiomaProvider();
        var vm = new SettingsViewModel(svc, loc, idioma);
        vm.Carregar();
        vm.PasswordGeneratorLength = 16;

        vm.TamanhoSenhaTexto.Should().Be("16 caracteres");
    }

    [Fact]
    public async Task SalvarAsync_ComValoresValidos_DevePersistirERetornarTrue()
    {
        var svc = new FakeAppSettingsService(SettingsPadrao());
        var loc = CriarLoc();
        var idioma = new FakeIdiomaProvider(manifestLanguages: new[] { "pt-BR", "en-US" }, languages: new[] { "pt-BR" });
        var vm = new SettingsViewModel(svc, loc, idioma);
        vm.Carregar();
        vm.TimeoutAutoLockMinutes = 10;
        vm.ClipboardCleanTimeSeconds = 15;
        vm.PasswordGeneratorLength = 20;
        vm.IncludeLowercase = true;
        vm.IncludeUppercase = true;
        vm.IncludeDigits = true;
        vm.IncludeSymbols = true;
        vm.IdiomaSelecionado = vm.OpcoesIdioma.First(o => o.Codigo == "en-US");
        vm.TemaSelecionado = vm.OpcoesTema.First(o => o.Codigo == AppSettings.TemaClaro);

        var ok = await vm.SalvarAsync();

        ok.Should().BeTrue();
        svc.ChamadasSaveAsync.Should().Be(1);
        svc.UltimoSalvo!.AutoLockTimeoutMinutes.Should().Be(10);
        svc.UltimoSalvo!.ClipboardCleanTimeSeconds.Should().Be(15);
        svc.UltimoSalvo!.Idioma.Should().Be("en-US");
        svc.UltimoSalvo!.Tema.Should().Be(AppSettings.TemaClaro);
        vm.Erro.Should().BeNull();
    }

    [Fact]
    public async Task SalvarAsync_ComTimeoutInvalido_DeveRetornarFalseEPreencherErro()
    {
        var svc = new FakeAppSettingsService(SettingsPadrao());
        var loc = CriarLoc();
        var idioma = new FakeIdiomaProvider();
        var vm = new SettingsViewModel(svc, loc, idioma);
        vm.Carregar();
        vm.TimeoutAutoLockMinutes = 0; // inválido (<1)

        var ok = await vm.SalvarAsync();

        ok.Should().BeFalse();
        vm.Erro.Should().NotBeNullOrWhiteSpace();
        svc.ChamadasSaveAsync.Should().Be(0); // não persistiu
    }

    [Fact]
    public async Task SalvarAsync_ComNenhumaClasseDeCaractere_DeveRetornarFalse()
    {
        var svc = new FakeAppSettingsService(SettingsPadrao());
        var loc = CriarLoc();
        var idioma = new FakeIdiomaProvider();
        var vm = new SettingsViewModel(svc, loc, idioma);
        vm.Carregar();
        vm.IncludeLowercase = false;
        vm.IncludeUppercase = false;
        vm.IncludeDigits = false;
        vm.IncludeSymbols = false;

        var ok = await vm.SalvarAsync();

        ok.Should().BeFalse();
        vm.Erro.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task SalvarAsync_QuandoIdiomaMuda_DeveMarcarRequerReinicio()
    {
        var svc = new FakeAppSettingsService(SettingsPadrao());
        var loc = CriarLoc();
        // Sistema em pt-BR, idiomaOriginal = auto -> efetivo pt-BR
        var idioma = new FakeIdiomaProvider(manifestLanguages: new[] { "pt-BR", "en-US" }, languages: new[] { "pt-BR" });
        var vm = new SettingsViewModel(svc, loc, idioma);
        vm.Carregar(); // _idiomaOriginal = auto
        vm.IdiomaSelecionado = vm.OpcoesIdioma.First(o => o.Codigo == "en-US"); // muda para en-US -> efetivo en-US != pt-BR

        var ok = await vm.SalvarAsync();

        ok.Should().BeTrue();
        vm.RequerReinicio.Should().BeTrue();
    }

    [Fact]
    public async Task SalvarAsync_QuandoIdiomaNaoMuda_NaoDeveRequerReinicio()
    {
        var settings = SettingsPadrao() with { Idioma = "pt-BR" };
        var svc = new FakeAppSettingsService(settings);
        var loc = CriarLoc();
        var idioma = new FakeIdiomaProvider(manifestLanguages: new[] { "pt-BR", "en-US" }, languages: new[] { "pt-BR" });
        var vm = new SettingsViewModel(svc, loc, idioma);
        vm.Carregar(); // original pt-BR
        vm.IdiomaSelecionado = vm.OpcoesIdioma.First(o => o.Codigo == "pt-BR");

        var ok = await vm.SalvarAsync();

        ok.Should().BeTrue();
        vm.RequerReinicio.Should().BeFalse();
    }

    [Fact]
    public async Task SalvarAsync_ComAutoQueResolveParaMesmoIdioma_NaoDeveRequerReinicio()
    {
        // original pt-BR, selecionado auto, sistema pt-BR -> efetivo pt-BR = pt-BR
        var settings = SettingsPadrao() with { Idioma = "pt-BR" };
        var svc = new FakeAppSettingsService(settings);
        var loc = CriarLoc();
        var idioma = new FakeIdiomaProvider(manifestLanguages: new[] { "pt-BR", "en-US" }, languages: new[] { "pt-BR" });
        var vm = new SettingsViewModel(svc, loc, idioma);
        vm.Carregar();
        vm.IdiomaSelecionado = vm.OpcoesIdioma.First(o => o.Codigo == "auto");

        var ok = await vm.SalvarAsync();

        ok.Should().BeTrue();
        vm.RequerReinicio.Should().BeFalse();
    }
}
