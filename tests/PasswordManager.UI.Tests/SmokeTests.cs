using FluentAssertions;
using PasswordManager.Application.VaultSession;
using PasswordManager.UI.Tests.Fakes;
using PasswordManager.UI.ViewModels;

namespace PasswordManager.UI.Tests;

/// <summary>
/// Smoke tests para validar que o projeto <c>PasswordManager.UI.Tests</c>
/// compila e roda no CI (etapa 6.4). Cobertura real dos ViewModels fica nas
/// etapas 6.5 e 6.6.
/// </summary>
public class SmokeTests
{
    private static async Task<(VaultViewModel vm, FakeClipboardService clipboard, FakeTimerFactory timers, IVaultSessionService session)> CriarVaultViewModelAsync()
    {
        var repo = new FakeVaultRepository();
        var crypto = new FakeCryptoService();
        var export = new FakeExportImportService();
        var session = new VaultSessionService(repo, crypto, export);
        // Cria cofre e desbloqueia para que CurrentVault esteja disponível.
        await session.CreateAsync("senha-mestra-123");

        var settings = new FakeAppSettingsService();
        var loc = new FakeLocalizationService(new Dictionary<string, string>
        {
            ["VaultViewModel_TodasPastas"] = "Todas as pastas",
            ["VaultPage_ToastSenhaCopiada.Text"] = "Senha copiada! Limpa em {0}s",
            ["VaultPage_ToastCofreExportado.Text"] = "Cofre exportado",
            ["VaultPage_ToastCofreImportado.Text"] = "Cofre importado"
        });
        var clipboard = new FakeClipboardService();
        var timers = new FakeTimerFactory();

        var vm = new VaultViewModel(session, settings, loc, timers, clipboard);
        return (vm, clipboard, timers, session);
    }

    [Fact]
    public async Task VaultViewModel_Construtor_ComFakes_DeveCriarTresTimers()
    {
        var (_, _, timers, _) = await CriarVaultViewModelAsync();

        timers.Timers.Should().HaveCount(3);
        timers.TimerClipboard.IsRunning.Should().BeFalse();
        timers.TimerInatividade.IsRunning.Should().BeFalse();
        timers.TimerInfoBanner.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task VaultViewModel_CopiarSenha_DeveUsarClipboardServiceECopiarSenha()
    {
        var (vm, clipboard, timers, session) = await CriarVaultViewModelAsync();
        var item = session.CurrentVault.AddItem("GitHub", "s3nh@!", "Dev", "user");
        await session.SaveAsync();

        vm.CopiarSenhaCommand.Execute(item);

        clipboard.UltimoTexto.Should().Be("s3nh@!");
        clipboard.ChamadasSetText.Should().Be(1);
        timers.TimerClipboard.IsRunning.Should().BeTrue();
        vm.SenhaCopiada.Should().BeTrue();
    }

    [Fact]
    public void SettingsViewModel_Construtor_ComFakeIdiomaProvider_DeveConstruirOpcoes()
    {
        var settings = new FakeAppSettingsService();
        var loc = new FakeLocalizationService(new Dictionary<string, string>
        {
            ["Settings_Idioma_Opcao_Auto"] = "Automático",
            ["Settings_Tema_Opcao_Sistema"] = "Sistema",
            ["Settings_Tema_Opcao_Claro"] = "Claro",
            ["Settings_Tema_Opcao_Escuro"] = "Escuro"
        });
        var idiomaProvider = new FakeIdiomaProvider(
            manifestLanguages: new[] { "pt-BR", "en-US", "es-ES" },
            languages: new[] { "pt-BR" });

        var vm = new SettingsViewModel(settings, loc, idiomaProvider);

        vm.OpcoesIdioma.Should().Contain(o => o.Codigo == "pt-BR");
        vm.OpcoesIdioma.Should().Contain(o => o.Codigo == "en-US");
        vm.OpcoesIdioma.Should().Contain(o => o.Codigo == "auto");
        vm.OpcoesTema.Should().HaveCount(3);
    }
}
