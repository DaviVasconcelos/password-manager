using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PasswordManager.Application.Exceptions;
using PasswordManager.Domain.Entities;
using PasswordManager.Infrastructure.Cryptography;
using PasswordManager.Infrastructure.Persistence;
using FluentAssertions;

namespace PasswordManager.Infrastructure.Tests;

public class VaultRepositoryTests : IDisposable
{
    private const string SenhaMestra = "senha-mestra-de-teste";
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly InMemoryVaultStore _store = new();
    private readonly CryptoService _cryptoService = new(
        argon2MemorySizeInKb: 32,
        argon2Iterations: 1,
        argon2DegreeOfParallelism: 1);

    public void Dispose() => _store.Dispose();

    [Fact]
    public async Task ExistsAsync_QuandoNaoHaCofre_DeveRetornarFalse()
    {
        var repository = CriarRepositorio();

        var exists = await repository.ExistsAsync(Ct);

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_QuandoHaCofrePersistido_DeveRetornarTrue()
    {
        var repository = CriarRepositorio();
        await CriarCofreAsync(repository, Vault.CreateNew(), SenhaMestra);

        var exists = await repository.ExistsAsync(Ct);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_ComVaultNovo_DevePersistirUmRegistroUnicoComSaltEBlob()
    {
        var repository = CriarRepositorio();
        var salt = _cryptoService.GenerateSalt();
        var chave = DerivarChave(SenhaMestra, salt);

        await repository.CreateAsync(Vault.CreateNew(), chave, salt, Ct);

        var registro = ObterUnicoRegistro();
        registro.Should().NotBeNull();
        registro!.SchemaVersion.Should().Be(1);
        registro.Salt.Should().Equal(salt);
        registro.EncryptedBlob.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateAsync_QuandoJaExisteCofre_DeveLancarInvalidOperationException()
    {
        var repository = CriarRepositorio();
        await CriarCofreAsync(repository, Vault.CreateNew(), SenhaMestra);
        var outroSalt = _cryptoService.GenerateSalt();

        var act = () => repository.CreateAsync(Vault.CreateNew(), DerivarChave(SenhaMestra, outroSalt), outroSalt, Ct);

        await act.Should().ThrowAsync<InvalidOperationException>();
        ObterUnicoRegistro().Should().NotBeNull();
    }

    [Fact]
    public async Task SaveAsync_ChamadoDuasVezes_DeveSobrescreverOUnicoRegistro()
    {
        var repository = CriarRepositorio();
        var vault = Vault.CreateNew();
        vault.AddItem("GitHub", "senha123", "Dev");
        await CriarCofreAsync(repository, vault, SenhaMestra);
        vault.AddItem("Gmail", "senha456", "Email");
        await SalvarAsync(repository, vault, SenhaMestra);

        using var contexto = _store.CriarContexto();
        contexto.Vaults.Count().Should().Be(1);

        var carregado = await CarregarAsync(repository, SenhaMestra);
        carregado.Should().NotBeNull();
        carregado!.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task SaveAsync_QuandoRegistroJaExiste_DeveManterOSaltAnterior()
    {
        var repository = CriarRepositorio();
        await CriarCofreAsync(repository, Vault.CreateNew(), SenhaMestra);
        var saltInicial = ObterUnicoRegistro()!.Salt;

        await SalvarAsync(repository, Vault.CreateNew(), SenhaMestra);

        var saltFinal = ObterUnicoRegistro()!.Salt;
        saltInicial.Should().Equal(saltFinal);
    }

    [Fact]
    public async Task SaveAsync_QuandoNaoExisteCofre_DeveLancarInvalidOperationException()
    {
        var repository = CriarRepositorio();
        var salSemCofre = _cryptoService.GenerateSalt();

        var act = () => repository.SaveAsync(Vault.CreateNew(), DerivarChave(SenhaMestra, salSemCofre), Ct);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetSaltAsync_QuandoNaoHaCofre_DeveRetornarNull()
    {
        var repository = CriarRepositorio();

        var salt = await repository.GetSaltAsync(Ct);

        salt.Should().BeNull();
    }

    [Fact]
    public async Task GetSaltAsync_QuandoHaCofre_DeveRetornarOSaltPersistido()
    {
        var repository = CriarRepositorio();
        await CriarCofreAsync(repository, Vault.CreateNew(), SenhaMestra);
        var saltPersistido = ObterUnicoRegistro()!.Salt;

        var salt = await repository.GetSaltAsync(Ct);

        salt.Should().Equal(saltPersistido);
    }

    [Fact]
    public async Task LoadAsync_QuandoNaoHaCofre_DeveRetornarNull()
    {
        var repository = CriarRepositorio();

        var carregado = await repository.LoadAsync(DerivarChave(SenhaMestra, _cryptoService.GenerateSalt()), Ct);

        carregado.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_ELoadAsync_RoundTripComItensEPastas_DevePreservarVault()
    {
        var repository = CriarRepositorio();

        var vault = Vault.CreateNew();
        var github = vault.AddItem("GitHub", "senha123", "Dev",
            username: "davi", url: "https://github.com", notes: "conta pessoal");
        var gmail = vault.AddItem("Gmail", "senha456", "Email");
        var pasta = vault.AddFolder("Trabalho");
        vault.AssignItemToFolder(github.Id, pasta.Id);
        await CriarCofreAsync(repository, vault, SenhaMestra);

        var carregado = await CarregarAsync(repository, SenhaMestra);

        carregado.Should().NotBeNull();
        carregado!.Id.Should().Be(vault.Id);
        carregado.Items.Should().HaveCount(2);
        carregado.Folders.Should().ContainSingle().Which.Name.Should().Be("Trabalho");

        var githubCarregado = carregado.Items.Single(i => i.Id == github.Id);
        githubCarregado.Title.Should().Be("GitHub");
        githubCarregado.Password.Should().Be("senha123");
        githubCarregado.Category.Should().Be("Dev");
        githubCarregado.Username.Should().Be("davi");
        githubCarregado.Url.Should().Be("https://github.com");
        githubCarregado.Notes.Should().Be("conta pessoal");
        githubCarregado.FolderId.Should().Be(pasta.Id);
        githubCarregado.CreatedAt.Should().Be(github.CreatedAt);
        githubCarregado.UpdatedAt.Should().Be(github.UpdatedAt);

        var gmailCarregado = carregado.Items.Single(i => i.Id == gmail.Id);
        gmailCarregado.FolderId.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_ELoadAsync_RoundTripComVaultVazio_DeveRetornarVaultSemItens()
    {
        var repository = CriarRepositorio();
        await CriarCofreAsync(repository, Vault.CreateNew(), SenhaMestra);

        var carregado = await CarregarAsync(repository, SenhaMestra);

        carregado.Should().NotBeNull();
        carregado!.Items.Should().BeEmpty();
        carregado.Folders.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAsync_ComChaveErrada_DeveLancarCryptographicIntegrityException()
    {
        var repository = CriarRepositorio();
        await CriarCofreAsync(repository, Vault.CreateNew(), "senha-original");

        var act = () => CarregarAsync(repository, "senha-errada");

        await act.Should().ThrowAsync<CryptographicIntegrityException>();
    }

    [Fact]
    public async Task LoadAsync_ComBlobAdulteradoNoBanco_DeveLancarCryptographicIntegrityException()
    {
        var repository = CriarRepositorio();
        await CriarCofreAsync(repository, Vault.CreateNew(), SenhaMestra);

        using (var contexto = _store.CriarContexto())
        {
            var registro = contexto.Vaults.Single();
            var blobAdulterado = registro.EncryptedBlob.ToArray();
            blobAdulterado[^1] ^= 0xFF;
            registro.EncryptedBlob = blobAdulterado;
            await contexto.SaveChangesAsync(Ct);
        }

        var act = () => CarregarAsync(repository, SenhaMestra);

        await act.Should().ThrowAsync<CryptographicIntegrityException>();
    }

    [Fact]
    public async Task ChangeMasterPasswordAsync_DeveRotacionarSaltEChaveAntigaDeixaDeAbrir()
    {
        var repository = CriarRepositorio();
        var vault = Vault.CreateNew();
        vault.AddItem("GitHub", "senha123", "Dev");
        await CriarCofreAsync(repository, vault, "senha-antiga");
        var saltAntigo = ObterUnicoRegistro()!.Salt;

        var novoSalt = _cryptoService.GenerateSalt();
        var novaChave = DerivarChave("senha-nova", novoSalt);
        await repository.ChangeMasterPasswordAsync(vault, novaChave, novoSalt, Ct);

        var saltFinal = ObterUnicoRegistro()!.Salt;
        saltFinal.Should().NotEqual(saltAntigo, "o salt deve ser rotacionado na troca de senha");

        var carregadoComNova = await CarregarAsync(repository, "senha-nova");
        carregadoComNova.Should().NotBeNull();
        carregadoComNova!.Items.Should().ContainSingle().Which.Title.Should().Be("GitHub");

        var actAntiga = () => CarregarAsync(repository, "senha-antiga");
        await actAntiga.Should().ThrowAsync<CryptographicIntegrityException>();
    }

    [Fact]
    public async Task ChangeMasterPasswordAsync_QuandoNaoExisteCofre_DeveLancarInvalidOperationException()
    {
        var repository = CriarRepositorio();
        var salSemCofre = _cryptoService.GenerateSalt();

        var act = () => repository.ChangeMasterPasswordAsync(
            Vault.CreateNew(), DerivarChave("senha-nova", salSemCofre), salSemCofre, Ct);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SaveAsync_ComVaultNulo_DeveLancarArgumentNullException()
    {
        var repository = CriarRepositorio();

        var act = () => repository.SaveAsync(null!, DerivarChave(SenhaMestra, _cryptoService.GenerateSalt()), Ct);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    private VaultRepository CriarRepositorio()
    {
        var contexto = _store.CriarContexto();
        return new VaultRepository(contexto, _cryptoService);
    }

    private byte[] DerivarChave(string senha, byte[] salt) => _cryptoService.DeriveKey(senha, salt);

    private async Task CriarCofreAsync(VaultRepository repository, Vault vault, string senha)
    {
        var salt = _cryptoService.GenerateSalt();
        await repository.CreateAsync(vault, DerivarChave(senha, salt), salt, Ct);
    }

    private async Task SalvarAsync(VaultRepository repository, Vault vault, string senha)
    {
        var salt = ObterUnicoRegistro()!.Salt;
        await repository.SaveAsync(vault, DerivarChave(senha, salt), Ct);
    }

    private async Task<Vault?> CarregarAsync(VaultRepository repository, string senha)
    {
        var salt = ObterUnicoRegistro()!.Salt;
        return await repository.LoadAsync(DerivarChave(senha, salt), Ct);
    }

    private VaultRecord? ObterUnicoRegistro()
    {
        using var contexto = _store.CriarContexto();
        return contexto.Vaults.AsNoTracking().SingleOrDefault();
    }

    private sealed class InMemoryVaultStore : IDisposable
    {
        private readonly SqliteConnection _connection;
        private bool _schemaCreated;

        public InMemoryVaultStore()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
        }

        public VaultDbContext CriarContexto()
        {
            var options = new DbContextOptionsBuilder<VaultDbContext>()
                .UseSqlite(_connection)
                .Options;

            var contexto = new VaultDbContext(options);

            if (!_schemaCreated)
            {
                contexto.Database.EnsureCreated();
                _schemaCreated = true;
            }

            return contexto;
        }

        public void Dispose() => _connection.Dispose();
    }
}