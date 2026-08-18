using PasswordManager.Application.Exceptions;
using PasswordManager.Application.Tests.Fakes;
using PasswordManager.Application.VaultSession;
using PasswordManager.Domain.Entities;
using FluentAssertions;

namespace PasswordManager.Application.Tests;

public class VaultSessionServiceTests
{
    private const string SenhaMestra = "senha-mestra-teste";
    private const string NovaSenha = "nova-senha-teste";

    private readonly FakeVaultRepository _repository = new();
    private readonly FakeCryptoService _cryptoService = new();
    private readonly FakeExportImportService _exportImportService = new();

    private VaultSessionService CriarServico() => new(_repository, _cryptoService, _exportImportService);

    [Fact]
    public void Unlocked_QuandoSessaoInicial_DeveRetornarFalse()
    {
        var servico = CriarServico();

        servico.Unlocked.Should().BeFalse();
    }

    [Fact]
    public void CurrentVault_QuandoSessaoTrancada_DeveLancarInvalidOperationException()
    {
        var servico = CriarServico();

        var act = () => servico.CurrentVault;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateAsync_QuandoNaoHaCofre_DeveCriarPersistirEDesbloquear()
    {
        var servico = CriarServico();

        var vault = await servico.CreateAsync(SenhaMestra);

        vault.Should().NotBeNull();
        servico.Unlocked.Should().BeTrue();
        servico.CurrentVault.Should().BeSameAs(vault);
        _repository.VaultPersistido.Should().BeSameAs(vault);
        _repository.SaltPersistido.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateAsync_QuandoJaHaCofre_DeveLancarInvalidOperationException()
    {
        var servico = CriarServico();
        await servico.CreateAsync(SenhaMestra);
        servico.Lock();

        var act = () => servico.CreateAsync("outra-senha");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateAsync_QuandoJaUnlocked_DeveLancarInvalidOperationException()
    {
        var servico = CriarServico();
        await servico.CreateAsync(SenhaMestra);

        var act = () => servico.CreateAsync("outra-senha");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateAsync_ComSenhaVazia_DeveLancarArgumentExceptionESemDesbloquear()
    {
        var servico = CriarServico();

        var act = () => servico.CreateAsync(string.Empty);

        await act.Should().ThrowAsync<ArgumentException>();
        servico.Unlocked.Should().BeFalse();
    }

    [Fact]
    public async Task UnlockAsync_ComSenhaCorreta_DeveCarregarVaultEDesbloquear()
    {
        var servico = CriarServico();
        var criado = await servico.CreateAsync(SenhaMestra);
        servico.Lock();

        var vault = await servico.UnlockAsync(SenhaMestra);

        vault.Should().NotBeNull();
        vault.Id.Should().Be(criado.Id);
        servico.Unlocked.Should().BeTrue();
    }

    [Fact]
    public async Task UnlockAsync_ComSenhaErrada_DeveLancarIntegrityExceptionEManterSessaoTrancada()
    {
        var servico = CriarServico();
        await servico.CreateAsync(SenhaMestra);
        servico.Lock();

        var act = () => servico.UnlockAsync("senha-errada");

        await act.Should().ThrowAsync<CryptographicIntegrityException>();
        servico.Unlocked.Should().BeFalse();
    }

    [Fact]
    public async Task UnlockAsync_QuandoNaoHaCofre_DeveLancarInvalidOperationException()
    {
        var servico = CriarServico();

        var act = () => servico.UnlockAsync(SenhaMestra);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UnlockAsync_QuandoJaUnlocked_DeveLancarInvalidOperationException()
    {
        var servico = CriarServico();
        await servico.CreateAsync(SenhaMestra);

        var act = () => servico.UnlockAsync(SenhaMestra);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Lock_QuandoUnlocked_DeveLimparVaultEEstado()
    {
        var servico = CriarServico();
        await servico.CreateAsync(SenhaMestra);

        servico.Lock();

        servico.Unlocked.Should().BeFalse();
        var act = () => servico.CurrentVault;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task Lock_QuandoJaTrancado_DeveSerInofensivo()
    {
        var servico = CriarServico();
        await servico.CreateAsync(SenhaMestra);
        servico.Lock();

        var act = () => servico.Lock();

        act.Should().NotThrow();
    }

    [Fact]
    public async Task ChangeMasterPasswordAsync_QuandoUnlocked_DeveRotacionarSaltEChave()
    {
        var servico = CriarServico();
        await servico.CreateAsync(SenhaMestra);
        var saltAntigo = _repository.SaltPersistido!;

        await servico.ChangeMasterPasswordAsync(SenhaMestra, NovaSenha);

        var saltNovo = _repository.SaltPersistido!;
        saltNovo.Should().NotEqual(saltAntigo);

        servico.Lock();
        (await servico.UnlockAsync(NovaSenha)).Should().NotBeNull();

        servico.Lock();
        var act = () => servico.UnlockAsync(SenhaMestra);
        await act.Should().ThrowAsync<CryptographicIntegrityException>();
    }

    [Fact]
    public async Task ChangeMasterPasswordAsync_QuandoTrancado_DeveLancarInvalidOperationException()
    {
        var servico = CriarServico();

        var act = () => servico.ChangeMasterPasswordAsync(SenhaMestra, NovaSenha);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ChangeMasterPasswordAsync_ComSenhaVazia_DeveLancarArgumentExceptionESemAlterarSalt()
    {
        var servico = CriarServico();
        await servico.CreateAsync(SenhaMestra);
        var saltAntigo = _repository.SaltPersistido!;

        var act = () => servico.ChangeMasterPasswordAsync(string.Empty, NovaSenha);

        await act.Should().ThrowAsync<ArgumentException>();
        _repository.SaltPersistido.Should().Equal(saltAntigo);
    }

    [Fact]
    public async Task ChangeMasterPasswordAsync_ComSenhaAtualIncorreta_DeveLancarCryptographicIntegrityException()
    {
        var servico = CriarServico();
        await servico.CreateAsync(SenhaMestra);
        var saltAntigo = _repository.SaltPersistido!;

        var act = () => servico.ChangeMasterPasswordAsync("senha-errada", NovaSenha);

        await act.Should().ThrowAsync<CryptographicIntegrityException>();
        _repository.SaltPersistido.Should().Equal(saltAntigo);
    }

    [Fact]
    public async Task SaveAsync_QuandoUnlocked_DevePersistirVaultComChaveRetida()
    {
        var servico = CriarServico();
        var vault = await servico.CreateAsync(SenhaMestra);
        vault.AddItem("GitHub", "senha123", "Dev");

        await servico.SaveAsync();

        servico.Lock();
        var recarregado = await servico.UnlockAsync(SenhaMestra);
        recarregado.Items.Should().ContainSingle().Which.Title.Should().Be("GitHub");
    }

    [Fact]
    public async Task SaveAsync_QuandoTrancado_DeveLancarInvalidOperationException()
    {
        var servico = CriarServico();

        var act = () => servico.SaveAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task VaultExistsAsync_QuandoNaoHaCofre_DeveRetornarFalse()
    {
        var servico = CriarServico();

        (await servico.VaultExistsAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task VaultExistsAsync_QuandoHaCofre_DeveRetornarTrue()
    {
        var servico = CriarServico();
        await servico.CreateAsync(SenhaMestra);

        (await servico.VaultExistsAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task AddItemAsync_QuandoUnlocked_DeveAdicionarEPersistir()
    {
        var servico = CriarServico();
        await servico.CreateAsync(SenhaMestra);

        var item = await servico.AddItemAsync("GitHub", "senha123", "Dev");

        servico.CurrentVault.Items.Should().Contain(item);
        servico.Lock();
        var recarregado = await servico.UnlockAsync(SenhaMestra);
        recarregado.Items.Should().ContainSingle().Which.Id.Should().Be(item.Id);
    }

    [Fact]
    public async Task AddItemAsync_QuandoTrancado_DeveLancarInvalidOperationException()
    {
        var servico = CriarServico();

        var act = () => servico.AddItemAsync("GitHub", "senha123", "Dev");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ReloadItemAsync_QuandoUnlocked_DeveAtualizarEPersistir()
    {
        var servico = CriarServico();
        await servico.CreateAsync(SenhaMestra);
        var item = await servico.AddItemAsync("GitHub", "senha123", "Dev");

        await servico.ReloadItemAsync(item.Id, "GitHub Enterprise", "nova-senha", "Trabalho");

        var atualizado = servico.CurrentVault.Items.Single(i => i.Id == item.Id);
        atualizado.Title.Should().Be("GitHub Enterprise");
        atualizado.Password.Should().Be("nova-senha");
        atualizado.Category.Should().Be("Trabalho");
    }

    [Fact]
    public async Task ReloadItemAsync_ComIdInexistente_DeveLancarInvalidOperationException()
    {
        var servico = CriarServico();
        await servico.CreateAsync(SenhaMestra);

        var act = () => servico.ReloadItemAsync(Guid.NewGuid(), "Título", "senha", "Categoria");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task RemoveItemAsync_QuandoUnlocked_DeveRemoverEPersistir()
    {
        var servico = CriarServico();
        await servico.CreateAsync(SenhaMestra);
        var item = await servico.AddItemAsync("GitHub", "senha123", "Dev");

        await servico.RemoveItemAsync(item.Id);

        servico.CurrentVault.Items.Should().BeEmpty();
        servico.Lock();
        var recarregado = await servico.UnlockAsync(SenhaMestra);
        recarregado.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task AddFolderAsync_QuandoUnlocked_DeveAdicionarEPersistir()
    {
        var servico = CriarServico();
        await servico.CreateAsync(SenhaMestra);

        var pasta = await servico.AddFolderAsync("Trabalho");

        servico.CurrentVault.Folders.Should().Contain(pasta);
        servico.Lock();
        var recarregado = await servico.UnlockAsync(SenhaMestra);
        recarregado.Folders.Should().ContainSingle().Which.Id.Should().Be(pasta.Id);
    }

    [Fact]
    public async Task RenameFolderAsync_QuandoUnlocked_DeveRenomearEPersistir()
    {
        var servico = CriarServico();
        await servico.CreateAsync(SenhaMestra);
        var pasta = await servico.AddFolderAsync("Trabalho");

        await servico.RenameFolderAsync(pasta.Id, "Pessoal");

        servico.CurrentVault.Folders.Single().Name.Should().Be("Pessoal");
    }

    [Fact]
    public async Task RemoverPastaAsync_QuandoUnlocked_DeveRemoverPastaEDesassociarItens()
    {
        var servico = CriarServico();
        await servico.CreateAsync(SenhaMestra);
        var pasta = await servico.AddFolderAsync("Trabalho");
        var item = await servico.AddItemAsync("GitHub", "senha123", "Dev");
        await servico.AssignItemToFolderAsync(item.Id, pasta.Id);

        await servico.RemoveFolderAsync(pasta.Id);

        servico.CurrentVault.Folders.Should().BeEmpty();
        servico.CurrentVault.Items.Single().FolderId.Should().BeNull();
    }

    [Fact]
    public async Task AssignItemToFolderAsync_QuandoUnlocked_DeveAssociarEPersistir()
    {
        var servico = CriarServico();
        await servico.CreateAsync(SenhaMestra);
        var pasta = await servico.AddFolderAsync("Trabalho");
        var item = await servico.AddItemAsync("GitHub", "senha123", "Dev");

        await servico.AssignItemToFolderAsync(item.Id, pasta.Id);

        servico.CurrentVault.Items.Single(i => i.Id == item.Id).FolderId.Should().Be(pasta.Id);
        servico.Lock();
        var recarregado = await servico.UnlockAsync(SenhaMestra);
        recarregado.Items.Single(i => i.Id == item.Id).FolderId.Should().Be(pasta.Id);
    }

    [Fact]
    public async Task SearchItems_SemFiltros_DeveRetornarTodosOsItens()
    {
        var servico = CriarServico();
        await servico.CreateAsync(SenhaMestra);
        await servico.AddItemAsync("GitHub", "senha123", "Dev");
        await servico.AddItemAsync("Gmail", "senha456", "Email");

        var resultado = servico.SearchItems();

        resultado.Should().HaveCount(2);
    }

    [Fact]
    public async Task SearchItems_ComTermoNoTitulo_DeveFiltrar()
    {
        var servico = CriarServico();
        await servico.CreateAsync(SenhaMestra);
        await servico.AddItemAsync("GitHub", "senha123", "Dev");
        await servico.AddItemAsync("Gmail", "senha456", "Email");

        var resultado = servico.SearchItems(termo: "git");

        resultado.Should().ContainSingle().Which.Title.Should().Be("GitHub");
    }

    [Fact]
    public async Task SearchItems_ComTermoNoUsuario_DeveFiltrar()
    {
        var servico = CriarServico();
        await servico.CreateAsync(SenhaMestra);
        await servico.AddItemAsync("GitHub", "senha123", "Dev", username: "davi@acme");
        await servico.AddItemAsync("Gmail", "senha456", "Email", username: "joao@acme");

        var resultado = servico.SearchItems(termo: "davi");

        resultado.Should().ContainSingle().Which.Username.Should().Be("davi@acme");
    }

    [Fact]
    public async Task SearchItems_ComPasta_DeveFiltrarPorPasta()
    {
        var servico = CriarServico();
        await servico.CreateAsync(SenhaMestra);
        var pasta = await servico.AddFolderAsync("Trabalho");
        var itemNaPasta = await servico.AddItemAsync("GitHub", "senha123", "Dev");
        var itemSolto = await servico.AddItemAsync("Gmail", "senha456", "Email");
        await servico.AssignItemToFolderAsync(itemNaPasta.Id, pasta.Id);

        var resultado = servico.SearchItems(pastaId: pasta.Id);

        resultado.Should().ContainSingle().Which.Id.Should().Be(itemNaPasta.Id);
    }

    [Fact]
    public void SearchItems_QuandoTrancado_DeveLancarInvalidOperationException()
    {
        var servico = CriarServico();

        var act = () => servico.SearchItems();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task ExportAsync_QuandoUnlocked_DeveExportarOCofreAtualComASenha()
    {
        var servico = CriarServico();
        await servico.CreateAsync(SenhaMestra);

        var dados = await servico.ExportAsync(SenhaMestra);

        dados.Should().NotBeEmpty();
        _exportImportService.VaultExportado.Should().BeSameAs(servico.CurrentVault);
    }

    [Fact]
    public async Task ExportAsync_QuandoTrancado_DeveLancarInvalidOperationException()
    {
        var servico = CriarServico();

        var act = () => servico.ExportAsync(SenhaMestra);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ExportAsync_ComSenhaVazia_DeveLancarArgumentException()
    {
        var servico = CriarServico();
        await servico.CreateAsync(SenhaMestra);

        var act = () => servico.ExportAsync(string.Empty);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ImportAsync_QuandoUnlockedComReplace_DeveSubstituirOCofreAtualEPersistir()
    {
        var servico = CriarServico();
        await servico.CreateAsync(SenhaMestra);
        await servico.AddItemAsync("GitHub", "senha123", "Dev");

        var importado = Vault.CreateNew();
        importado.AddItem("Gmail", "senha456", "Email");
        _exportImportService.VaultParaImportar = importado;

        await servico.ImportAsync(new byte[] { 1, 2, 3 }, SenhaMestra, replace: true);

        servico.CurrentVault.Should().BeSameAs(importado);
        servico.CurrentVault.Items.Should().ContainSingle().Which.Title.Should().Be("Gmail");

        servico.Lock();
        var recarregado = await servico.UnlockAsync(SenhaMestra);
        recarregado.Items.Should().ContainSingle().Which.Title.Should().Be("Gmail");
    }

    [Fact]
    public async Task ImportAsync_QuandoUnlockedComMerge_DeveMesclarEPersistir()
    {
        var servico = CriarServico();
        await servico.CreateAsync(SenhaMestra);
        await servico.AddItemAsync("GitHub", "senha123", "Dev");

        var importado = Vault.CreateNew();
        importado.AddItem("Gmail", "senha456", "Email");
        importado.AddFolder("Trabalho");
        _exportImportService.VaultParaImportar = importado;

        await servico.ImportAsync(new byte[] { 1, 2, 3 }, SenhaMestra, replace: false);

        servico.CurrentVault.Items.Should().HaveCount(2);
        servico.CurrentVault.Folders.Should().ContainSingle().Which.Name.Should().Be("Trabalho");

        servico.Lock();
        var recarregado = await servico.UnlockAsync(SenhaMestra);
        recarregado.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task ImportAsync_ComSenhaErrada_DeveLancarIntegrityExceptionESemAlterarOCofre()
    {
        var servico = CriarServico();
        await servico.CreateAsync(SenhaMestra);
        _exportImportService.SenhaEsperada = SenhaMestra;
        var vaultOriginal = servico.CurrentVault;

        var act = () => servico.ImportAsync(new byte[] { 1, 2, 3 }, "senha-errada", replace: true);

        await act.Should().ThrowAsync<CryptographicIntegrityException>();
        servico.CurrentVault.Should().BeSameAs(vaultOriginal);
        servico.CurrentVault.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ImportAsync_QuandoTrancadoESemCofre_DeveCriarOCofreAPartirDoArquivo()
    {
        var servico = CriarServico();
        _exportImportService.SenhaEsperada = SenhaMestra;

        var importado = Vault.CreateNew();
        importado.AddItem("Gmail", "senha456", "Email");
        _exportImportService.VaultParaImportar = importado;

        await servico.ImportAsync(new byte[] { 1, 2, 3 }, SenhaMestra, replace: true);

        servico.Unlocked.Should().BeTrue();
        servico.CurrentVault.Should().BeSameAs(importado);
        _repository.VaultPersistido.Should().BeSameAs(importado);

        servico.Lock();
        var recarregado = await servico.UnlockAsync(SenhaMestra);
        recarregado.Items.Should().ContainSingle().Which.Title.Should().Be("Gmail");
    }

    [Fact]
    public async Task ImportAsync_QuandoTrancadoEJaExisteCofre_DeveLancarInvalidOperationException()
    {
        var servico = CriarServico();
        await servico.CreateAsync(SenhaMestra);
        servico.Lock();

        var act = () => servico.ImportAsync(new byte[] { 1, 2, 3 }, SenhaMestra, replace: true);

        await act.Should().ThrowAsync<InvalidOperationException>();
        servico.Unlocked.Should().BeFalse();
    }

    [Fact]
    public async Task ImportAsync_ComDadosNulos_DeveLancarArgumentNullException()
    {
        var servico = CriarServico();
        await servico.CreateAsync(SenhaMestra);

        var act = () => servico.ImportAsync(null!, SenhaMestra, replace: true);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}