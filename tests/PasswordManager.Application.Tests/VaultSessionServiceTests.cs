using PasswordManager.Application.Exceptions;
using PasswordManager.Application.Tests.Fakes;
using PasswordManager.Application.VaultSession;
using FluentAssertions;

namespace PasswordManager.Application.Tests;

public class VaultSessionServiceTests
{
    private const string SenhaMestra = "senha-mestra-teste";
    private const string NovaSenha = "nova-senha-teste";

    private readonly FakeVaultRepository _repository = new();
    private readonly FakeCryptoService _cryptoService = new();

    private VaultSessionService CriarServico() => new(_repository, _cryptoService);

    [Fact]
    public void Desbloqueado_QuandoSessaoInicial_DeveRetornarFalse()
    {
        var servico = CriarServico();

        servico.Desbloqueado.Should().BeFalse();
    }

    [Fact]
    public void VaultAtual_QuandoSessaoTrancada_DeveLancarInvalidOperationException()
    {
        var servico = CriarServico();

        var act = () => servico.VaultAtual;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task CriarAsync_QuandoNaoHaCofre_DeveCriarPersistirEDesbloquear()
    {
        var servico = CriarServico();

        var vault = await servico.CriarAsync(SenhaMestra);

        vault.Should().NotBeNull();
        servico.Desbloqueado.Should().BeTrue();
        servico.VaultAtual.Should().BeSameAs(vault);
        _repository.VaultPersistido.Should().BeSameAs(vault);
        _repository.SaltPersistido.Should().NotBeNull();
    }

    [Fact]
    public async Task CriarAsync_QuandoJaHaCofre_DeveLancarInvalidOperationException()
    {
        var servico = CriarServico();
        await servico.CriarAsync(SenhaMestra);
        servico.Trancar();

        var act = () => servico.CriarAsync("outra-senha");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CriarAsync_QuandoJaDesbloqueado_DeveLancarInvalidOperationException()
    {
        var servico = CriarServico();
        await servico.CriarAsync(SenhaMestra);

        var act = () => servico.CriarAsync("outra-senha");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CriarAsync_ComSenhaVazia_DeveLancarArgumentExceptionESemDesbloquear()
    {
        var servico = CriarServico();

        var act = () => servico.CriarAsync(string.Empty);

        await act.Should().ThrowAsync<ArgumentException>();
        servico.Desbloqueado.Should().BeFalse();
    }

    [Fact]
    public async Task DesbloquearAsync_ComSenhaCorreta_DeveCarregarVaultEDesbloquear()
    {
        var servico = CriarServico();
        var criado = await servico.CriarAsync(SenhaMestra);
        servico.Trancar();

        var vault = await servico.DesbloquearAsync(SenhaMestra);

        vault.Should().NotBeNull();
        vault.Id.Should().Be(criado.Id);
        servico.Desbloqueado.Should().BeTrue();
    }

    [Fact]
    public async Task DesbloquearAsync_ComSenhaErrada_DeveLancarIntegrityExceptionEManterSessaoTrancada()
    {
        var servico = CriarServico();
        await servico.CriarAsync(SenhaMestra);
        servico.Trancar();

        var act = () => servico.DesbloquearAsync("senha-errada");

        await act.Should().ThrowAsync<CryptographicIntegrityException>();
        servico.Desbloqueado.Should().BeFalse();
    }

    [Fact]
    public async Task DesbloquearAsync_QuandoNaoHaCofre_DeveLancarInvalidOperationException()
    {
        var servico = CriarServico();

        var act = () => servico.DesbloquearAsync(SenhaMestra);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task DesbloquearAsync_QuandoJaDesbloqueado_DeveLancarInvalidOperationException()
    {
        var servico = CriarServico();
        await servico.CriarAsync(SenhaMestra);

        var act = () => servico.DesbloquearAsync(SenhaMestra);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Trancar_QuandoDesbloqueado_DeveLimparVaultEEstado()
    {
        var servico = CriarServico();
        await servico.CriarAsync(SenhaMestra);

        servico.Trancar();

        servico.Desbloqueado.Should().BeFalse();
        var act = () => servico.VaultAtual;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task Trancar_QuandoJaTrancado_DeveSerInofensivo()
    {
        var servico = CriarServico();
        await servico.CriarAsync(SenhaMestra);
        servico.Trancar();

        var act = () => servico.Trancar();

        act.Should().NotThrow();
    }

    [Fact]
    public async Task TrocarSenhaMestraAsync_QuandoDesbloqueado_DeveRotacionarSaltEChave()
    {
        var servico = CriarServico();
        await servico.CriarAsync(SenhaMestra);
        var saltAntigo = _repository.SaltPersistido!;

        await servico.TrocarSenhaMestraAsync(NovaSenha);

        var saltNovo = _repository.SaltPersistido!;
        saltNovo.Should().NotEqual(saltAntigo);

        servico.Trancar();
        (await servico.DesbloquearAsync(NovaSenha)).Should().NotBeNull();

        servico.Trancar();
        var act = () => servico.DesbloquearAsync(SenhaMestra);
        await act.Should().ThrowAsync<CryptographicIntegrityException>();
    }

    [Fact]
    public async Task TrocarSenhaMestraAsync_QuandoTrancado_DeveLancarInvalidOperationException()
    {
        var servico = CriarServico();

        var act = () => servico.TrocarSenhaMestraAsync(NovaSenha);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task TrocarSenhaMestraAsync_ComSenhaVazia_DeveLancarArgumentExceptionESemAlterarSalt()
    {
        var servico = CriarServico();
        await servico.CriarAsync(SenhaMestra);
        var saltAntigo = _repository.SaltPersistido!;

        var act = () => servico.TrocarSenhaMestraAsync(string.Empty);

        await act.Should().ThrowAsync<ArgumentException>();
        _repository.SaltPersistido.Should().Equal(saltAntigo);
    }

    [Fact]
    public async Task SalvarAsync_QuandoDesbloqueado_DevePersistirVaultComChaveRetida()
    {
        var servico = CriarServico();
        var vault = await servico.CriarAsync(SenhaMestra);
        vault.AddItem("GitHub", "senha123", "Dev");

        await servico.SalvarAsync();

        servico.Trancar();
        var recarregado = await servico.DesbloquearAsync(SenhaMestra);
        recarregado.Items.Should().ContainSingle().Which.Title.Should().Be("GitHub");
    }

    [Fact]
    public async Task SalvarAsync_QuandoTrancado_DeveLancarInvalidOperationException()
    {
        var servico = CriarServico();

        var act = () => servico.SalvarAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}