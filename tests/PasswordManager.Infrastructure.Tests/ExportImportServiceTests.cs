using System.Text;
using FluentAssertions;
using PasswordManager.Application.Exceptions;
using PasswordManager.Domain.Entities;
using PasswordManager.Infrastructure.Cryptography;
using PasswordManager.Infrastructure.ExportImport;

namespace PasswordManager.Infrastructure.Tests;

public class ExportImportServiceTests
{
    private const string SenhaMestra = "senha-mestra-de-teste";

    private readonly ExportImportService _servico = new(new CryptoService(
        argon2MemorySizeInKb: 32,
        argon2Iterations: 1,
        argon2DegreeOfParallelism: 1));

    [Fact]
    public void Export_EImport_RoundTripComItensEPastas_DevePreservarVault()
    {
        var vault = Vault.CreateNew();
        var github = vault.AddItem("GitHub", "senha123", "Dev",
            username: "davi", url: "https://github.com", notes: "conta pessoal");
        vault.AddItem("Gmail", "senha456", "Email");
        var pasta = vault.AddFolder("Trabalho");
        vault.AssignItemToFolder(github.Id, pasta.Id);

        var arquivo = _servico.Export(vault, SenhaMestra);
        var importado = _servico.Import(arquivo, SenhaMestra);

        importado.Id.Should().Be(vault.Id);
        importado.Items.Should().HaveCount(2);
        importado.Folders.Should().ContainSingle().Which.Name.Should().Be("Trabalho");

        var githubImportado = importado.Items.Single(i => i.Title == "GitHub");
        githubImportado.Password.Should().Be("senha123");
        githubImportado.Category.Should().Be("Dev");
        githubImportado.Username.Should().Be("davi");
        githubImportado.Url.Should().Be("https://github.com");
        githubImportado.Notes.Should().Be("conta pessoal");
        githubImportado.FolderId.Should().Be(importado.Folders.Single().Id);
        githubImportado.CreatedAt.Should().Be(github.CreatedAt);
        githubImportado.UpdatedAt.Should().Be(github.UpdatedAt);

        importado.Items.Single(i => i.Title == "Gmail").FolderId.Should().BeNull();
    }

    [Fact]
    public void Export_EImport_RoundTripComVaultVazio_DeveRetornarVaultSemItens()
    {
        var arquivo = _servico.Export(Vault.CreateNew(), SenhaMestra);

        var importado = _servico.Import(arquivo, SenhaMestra);

        importado.Items.Should().BeEmpty();
        importado.Folders.Should().BeEmpty();
    }

    [Fact]
    public void Import_ComSenhaErrada_DeveLancarCryptographicIntegrityException()
    {
        var arquivo = _servico.Export(Vault.CreateNew(), SenhaMestra);

        var act = () => _servico.Import(arquivo, "senha-errada");

        act.Should().ThrowExactly<CryptographicIntegrityException>();
    }

    [Fact]
    public void Import_ComDadosAdulterados_DeveLancarCryptographicIntegrityException()
    {
        var arquivo = _servico.Export(Vault.CreateNew(), SenhaMestra);
        arquivo[^1] ^= 0xFF;

        var act = () => _servico.Import(arquivo, SenhaMestra);

        act.Should().ThrowExactly<CryptographicIntegrityException>();
    }

    [Fact]
    public void Import_ComCabecalhoInvalido_DeveLancarInvalidOperationException()
    {
        var arquivo = Encoding.ASCII.GetBytes("XXXX")
            .Concat(new byte[32]).ToArray();

        var act = () => _servico.Import(arquivo, SenhaMestra);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*não é um arquivo de cofre*");
    }

    [Fact]
    public void Import_ComArquivoTruncado_DeveLancarInvalidOperationException()
    {
        var arquivo = new byte[5];

        var act = () => _servico.Import(arquivo, SenhaMestra);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*tamanho insuficiente*");
    }

    [Fact]
    public void Import_ComVersaoFutura_DeveLancarInvalidOperationException()
    {
        var arquivo = _servico.Export(Vault.CreateNew(), SenhaMestra);
        arquivo[4] = 2;

        var act = () => _servico.Import(arquivo, SenhaMestra);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*versão*");
    }

    [Fact]
    public void Export_ChamadoDuasVezes_DeveGerarArquivosDistintos()
    {
        var vault = Vault.CreateNew();
        vault.AddItem("GitHub", "senha123", "Dev");

        var primeiro = _servico.Export(vault, SenhaMestra);
        var segundo = _servico.Export(vault, SenhaMestra);

        primeiro.Should().NotEqual(segundo);
    }

    [Fact]
    public void Export_ComVaultNulo_DeveLancarArgumentNullException()
    {
        var act = () => _servico.Export(null!, SenhaMestra);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Export_ComSenhaVazia_DeveLancarArgumentException()
    {
        var act = () => _servico.Export(Vault.CreateNew(), string.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Import_ComDadosNulos_DeveLancarArgumentNullException()
    {
        var act = () => _servico.Import(null!, SenhaMestra);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Import_ComSenhaVazia_DeveLancarArgumentException()
    {
        var act = () => _servico.Import(new byte[] { 1, 2, 3 }, string.Empty);

        act.Should().Throw<ArgumentException>();
    }
}