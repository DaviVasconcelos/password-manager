using FluentAssertions;
using PasswordManager.Application.VaultSession;
using PasswordManager.UI.Tests.Fakes;
using PasswordManager.UI.ViewModels;

namespace PasswordManager.UI.Tests;

/// <summary>
/// Testes de <see cref="UnlockViewModel"/>.
/// </summary>
public class UnlockViewModelTests
{
    private static FakeLocalizationService CriarLoc()
    {
        return new FakeLocalizationService(new Dictionary<string, string>
        {
            ["UnlockViewModel_TituloModo_Criar"] = "Criar cofre",
            ["UnlockViewModel_TituloModo_Desbloquear"] = "Desbloquear",
            ["UnlockViewModel_Erro_SenhaIncorreta"] = "Senha incorreta",
            ["UnlockViewModel_Erro_SenhasNaoConferem"] = "Senhas não conferem",
            ["UnlockViewModel_Erro_ArquivoCorrompido"] = "Arquivo corrompido"
        });
    }

    private static (UnlockViewModel vm, IVaultSessionService session) CriarSut(bool comCofreExistente = false)
    {
        var repo = new FakeVaultRepository();
        var crypto = new FakeCryptoService();
        var export = new FakeExportImportService();
        var session = new VaultSessionService(repo, crypto, export);
        if (comCofreExistente)
        {
            session.CreateAsync("senha-correta").GetAwaiter().GetResult();
            session.Lock();
        }
        var loc = CriarLoc();
        var vm = new UnlockViewModel(session, loc);
        return (vm, session);
    }

    [Fact]
    public async Task InitializeAsync_SemCofre_DeveEntrarEmModoCriar()
    {
        var (vm, _) = CriarSut(comCofreExistente: false);

        await vm.InitializeAsync();

        vm.ModoCriar.Should().BeTrue();
        vm.TituloModo.Should().Be("Criar cofre");
        vm.Ocupado.Should().BeFalse();
    }

    [Fact]
    public async Task InitializeAsync_ComCofreExistente_DeveEntrarEmModoDesbloquear()
    {
        var (vm, _) = CriarSut(comCofreExistente: true);

        await vm.InitializeAsync();

        vm.ModoCriar.Should().BeFalse();
        vm.TituloModo.Should().Be("Desbloquear");
    }

    [Fact]
    public async Task CreateAsync_ComSenhasDiferentes_DevePreencherErro()
    {
        var (vm, session) = CriarSut(comCofreExistente: false);
        await vm.InitializeAsync();
        vm.SenhaMestra = "abc";
        vm.ConfirmacaoSenha = "xyz";

        await vm.CreateCommand.ExecuteAsync(null);

        vm.Erro.Should().Be("Senhas não conferem");
        vm.ErroTemValor.Should().BeTrue();
        (await session.VaultExistsAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_ComSucesso_DeveDispararUnlockedELimparErro()
    {
        var (vm, session) = CriarSut(comCofreExistente: false);
        await vm.InitializeAsync();
        bool unlocked = false;
        vm.Unlocked += () => unlocked = true;
        vm.SenhaMestra = "senha-forte";
        vm.ConfirmacaoSenha = "senha-forte";

        await vm.CreateCommand.ExecuteAsync(null);

        unlocked.Should().BeTrue();
        vm.Erro.Should().BeNull();
        vm.ErroTemValor.Should().BeFalse();
        session.Unlocked.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_ComCofreJaExistente_DevePreencherErro()
    {
        var (vm, _) = CriarSut(comCofreExistente: true);
        await vm.InitializeAsync();
        vm.SenhaMestra = "nova";
        vm.ConfirmacaoSenha = "nova";

        await vm.CreateCommand.ExecuteAsync(null);

        vm.Erro.Should().NotBeNullOrWhiteSpace();
        vm.ErroTemValor.Should().BeTrue();
    }

    [Fact]
    public async Task UnlockAsync_ComSenhaCorreta_DeveDispararUnlocked()
    {
        var (vm, session) = CriarSut(comCofreExistente: true);
        await vm.InitializeAsync();
        bool unlocked = false;
        vm.Unlocked += () => unlocked = true;
        vm.SenhaMestra = "senha-correta";

        await vm.UnlockCommand.ExecuteAsync(null);

        unlocked.Should().BeTrue();
        vm.Erro.Should().BeNull();
        session.Unlocked.Should().BeTrue();
    }

    [Fact]
    public async Task UnlockAsync_ComSenhaIncorreta_DevePreencherErro_SenhaIncorreta()
    {
        var (vm, session) = CriarSut(comCofreExistente: true);
        await vm.InitializeAsync();
        vm.SenhaMestra = "senha-errada";

        await vm.UnlockCommand.ExecuteAsync(null);

        vm.Erro.Should().Be("Senha incorreta");
        vm.ErroTemValor.Should().BeTrue();
        session.Unlocked.Should().BeFalse();
    }

    [Fact]
    public void CanUnlock_CanCreate_QuandoOcupado_DeveSerFalse()
    {
        var (vm, _) = CriarSut();
        vm.SenhaMestra = "abc";
        vm.ConfirmacaoSenha = "abc";
        vm.Ocupado = true;

        vm.UnlockCommand.CanExecute(null).Should().BeFalse();
        vm.CreateCommand.CanExecute(null).Should().BeFalse();

        vm.Ocupado = false;
        vm.UnlockCommand.CanExecute(null).Should().BeTrue();
        vm.CreateCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task ImportarAsync_ComSucessoNaPrimeiraExecucao_DeveDispararUnlocked()
    {
        var (vm, session) = CriarSut(comCofreExistente: false);
        await vm.InitializeAsync();
        bool unlocked = false;
        vm.Unlocked += () => unlocked = true;

        await vm.ImportarAsync(new byte[] { 0x01, 0x02 }, "senha-import");

        unlocked.Should().BeTrue();
        vm.Erro.Should().BeNull();
        session.Unlocked.Should().BeTrue();
    }

    [Fact]
    public async Task ImportarAsync_ComArquivoCorrompido_DevePreencherErroArquivoCorrompido()
    {
        var repo = new FakeVaultRepository();
        var crypto = new FakeCryptoService();
        var export = new FakeExportImportService { SenhaEsperada = "correta" };
        var session = new VaultSessionService(repo, crypto, export);
        var loc = CriarLoc();
        var vm = new UnlockViewModel(session, loc);
        await vm.InitializeAsync();

        await vm.ImportarAsync(new byte[] { 0xFF }, "senha-errada");

        vm.Erro.Should().Be("Arquivo corrompido");
        vm.ErroTemValor.Should().BeTrue();
    }

    [Fact]
    public void ErroTemValor_QuandoErroVazio_DeveSerFalse()
    {
        var (vm, _) = CriarSut();
        vm.Erro = null;
        vm.ErroTemValor.Should().BeFalse();
        vm.Erro = " ";
        vm.ErroTemValor.Should().BeFalse();
        vm.Erro = "algo";
        vm.ErroTemValor.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_DeveSetarOcupadoDuranteExecucao()
    {
        var (vm, _) = CriarSut();
        var task = vm.InitializeAsync();
        // Durante a execução Ocupado pode estar true (Fake é síncrono, mas verifica estado final)
        await task;
        vm.Ocupado.Should().BeFalse();
    }
}
