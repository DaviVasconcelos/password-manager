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

    [Fact]
    public async Task ExisteCofreAsync_QuandoNaoHaCofre_DeveRetornarFalse()
    {
        var servico = CriarServico();

        (await servico.ExisteCofreAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task ExisteCofreAsync_QuandoHaCofre_DeveRetornarTrue()
    {
        var servico = CriarServico();
        await servico.CriarAsync(SenhaMestra);

        (await servico.ExisteCofreAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task AdicionarItemAsync_QuandoDesbloqueado_DeveAdicionarEPersistir()
    {
        var servico = CriarServico();
        await servico.CriarAsync(SenhaMestra);

        var item = await servico.AdicionarItemAsync("GitHub", "senha123", "Dev");

        servico.VaultAtual.Items.Should().Contain(item);
        servico.Trancar();
        var recarregado = await servico.DesbloquearAsync(SenhaMestra);
        recarregado.Items.Should().ContainSingle().Which.Id.Should().Be(item.Id);
    }

    [Fact]
    public async Task AdicionarItemAsync_QuandoTrancado_DeveLancarInvalidOperationException()
    {
        var servico = CriarServico();

        var act = () => servico.AdicionarItemAsync("GitHub", "senha123", "Dev");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task AtualizarItemAsync_QuandoDesbloqueado_DeveAtualizarEPersistir()
    {
        var servico = CriarServico();
        await servico.CriarAsync(SenhaMestra);
        var item = await servico.AdicionarItemAsync("GitHub", "senha123", "Dev");

        await servico.AtualizarItemAsync(item.Id, "GitHub Enterprise", "nova-senha", "Trabalho");

        var atualizado = servico.VaultAtual.Items.Single(i => i.Id == item.Id);
        atualizado.Title.Should().Be("GitHub Enterprise");
        atualizado.Password.Should().Be("nova-senha");
        atualizado.Category.Should().Be("Trabalho");
    }

    [Fact]
    public async Task AtualizarItemAsync_ComIdInexistente_DeveLancarInvalidOperationException()
    {
        var servico = CriarServico();
        await servico.CriarAsync(SenhaMestra);

        var act = () => servico.AtualizarItemAsync(Guid.NewGuid(), "Título", "senha", "Categoria");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task RemoverItemAsync_QuandoDesbloqueado_DeveRemoverEPersistir()
    {
        var servico = CriarServico();
        await servico.CriarAsync(SenhaMestra);
        var item = await servico.AdicionarItemAsync("GitHub", "senha123", "Dev");

        await servico.RemoverItemAsync(item.Id);

        servico.VaultAtual.Items.Should().BeEmpty();
        servico.Trancar();
        var recarregado = await servico.DesbloquearAsync(SenhaMestra);
        recarregado.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task AdicionarPastaAsync_QuandoDesbloqueado_DeveAdicionarEPersistir()
    {
        var servico = CriarServico();
        await servico.CriarAsync(SenhaMestra);

        var pasta = await servico.AdicionarPastaAsync("Trabalho");

        servico.VaultAtual.Folders.Should().Contain(pasta);
        servico.Trancar();
        var recarregado = await servico.DesbloquearAsync(SenhaMestra);
        recarregado.Folders.Should().ContainSingle().Which.Id.Should().Be(pasta.Id);
    }

    [Fact]
    public async Task RenomearPastaAsync_QuandoDesbloqueado_DeveRenomearEPersistir()
    {
        var servico = CriarServico();
        await servico.CriarAsync(SenhaMestra);
        var pasta = await servico.AdicionarPastaAsync("Trabalho");

        await servico.RenomearPastaAsync(pasta.Id, "Pessoal");

        servico.VaultAtual.Folders.Single().Name.Should().Be("Pessoal");
    }

    [Fact]
    public async Task RemoverPastaAsync_QuandoDesbloqueado_DeveRemoverPastaEDesassociarItens()
    {
        var servico = CriarServico();
        await servico.CriarAsync(SenhaMestra);
        var pasta = await servico.AdicionarPastaAsync("Trabalho");
        var item = await servico.AdicionarItemAsync("GitHub", "senha123", "Dev");
        await servico.AtribuirItemAPastaAsync(item.Id, pasta.Id);

        await servico.RemoverPastaAsync(pasta.Id);

        servico.VaultAtual.Folders.Should().BeEmpty();
        servico.VaultAtual.Items.Single().FolderId.Should().BeNull();
    }

    [Fact]
    public async Task AtribuirItemAPastaAsync_QuandoDesbloqueado_DeveAssociarEPersistir()
    {
        var servico = CriarServico();
        await servico.CriarAsync(SenhaMestra);
        var pasta = await servico.AdicionarPastaAsync("Trabalho");
        var item = await servico.AdicionarItemAsync("GitHub", "senha123", "Dev");

        await servico.AtribuirItemAPastaAsync(item.Id, pasta.Id);

        servico.VaultAtual.Items.Single(i => i.Id == item.Id).FolderId.Should().Be(pasta.Id);
        servico.Trancar();
        var recarregado = await servico.DesbloquearAsync(SenhaMestra);
        recarregado.Items.Single(i => i.Id == item.Id).FolderId.Should().Be(pasta.Id);
    }

    [Fact]
    public async Task BuscarItens_SemFiltros_DeveRetornarTodosOsItens()
    {
        var servico = CriarServico();
        await servico.CriarAsync(SenhaMestra);
        await servico.AdicionarItemAsync("GitHub", "senha123", "Dev");
        await servico.AdicionarItemAsync("Gmail", "senha456", "Email");

        var resultado = servico.BuscarItens();

        resultado.Should().HaveCount(2);
    }

    [Fact]
    public async Task BuscarItens_ComTermoNoTitulo_DeveFiltrar()
    {
        var servico = CriarServico();
        await servico.CriarAsync(SenhaMestra);
        await servico.AdicionarItemAsync("GitHub", "senha123", "Dev");
        await servico.AdicionarItemAsync("Gmail", "senha456", "Email");

        var resultado = servico.BuscarItens(termo: "git");

        resultado.Should().ContainSingle().Which.Title.Should().Be("GitHub");
    }

    [Fact]
    public async Task BuscarItens_ComTermoNoUsuario_DeveFiltrar()
    {
        var servico = CriarServico();
        await servico.CriarAsync(SenhaMestra);
        await servico.AdicionarItemAsync("GitHub", "senha123", "Dev", username: "davi@acme");
        await servico.AdicionarItemAsync("Gmail", "senha456", "Email", username: "joao@acme");

        var resultado = servico.BuscarItens(termo: "davi");

        resultado.Should().ContainSingle().Which.Username.Should().Be("davi@acme");
    }

    [Fact]
    public async Task BuscarItens_ComPasta_DeveFiltrarPorPasta()
    {
        var servico = CriarServico();
        await servico.CriarAsync(SenhaMestra);
        var pasta = await servico.AdicionarPastaAsync("Trabalho");
        var itemNaPasta = await servico.AdicionarItemAsync("GitHub", "senha123", "Dev");
        var itemSolto = await servico.AdicionarItemAsync("Gmail", "senha456", "Email");
        await servico.AtribuirItemAPastaAsync(itemNaPasta.Id, pasta.Id);

        var resultado = servico.BuscarItens(pastaId: pasta.Id);

        resultado.Should().ContainSingle().Which.Id.Should().Be(itemNaPasta.Id);
    }

    [Fact]
    public void BuscarItens_QuandoTrancado_DeveLancarInvalidOperationException()
    {
        var servico = CriarServico();

        var act = () => servico.BuscarItens();

        act.Should().Throw<InvalidOperationException>();
    }
}