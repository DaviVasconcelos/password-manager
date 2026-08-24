using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PasswordManager.Domain.Entities;
using PasswordManager.Infrastructure.Cryptography;
using PasswordManager.Infrastructure.Persistence;
using FluentAssertions;

namespace PasswordManager.Infrastructure.Tests;

/// <summary>
/// Testes das migrations do EF Core: o schema criado por Database.Migrate()
/// deve conter a tabela VaultStore esperada, ficar registrado no histórico,
/// não deixar mudanças de modelo pendentes e funcionar com o VaultRepository.
/// </summary>
public class VaultDbContextMigrationTests
{
    private const string SenhaMestra = "senha-mestra-de-teste";

    [Fact]
    public void Migrate_EmBancoNovo_DeveCriarTabelaVaultStoreComColunasEsperadas()
    {
        using var conexao = new SqliteConnection("Data Source=:memory:");
        conexao.Open();
        using var contexto = CriarContextoMigrado(conexao);

        using var comandoTabela = conexao.CreateCommand();
        comandoTabela.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name = 'VaultStore'";
        comandoTabela.ExecuteScalar().Should().Be("VaultStore");

        using var comandoColunas = conexao.CreateCommand();
        comandoColunas.CommandText = "PRAGMA table_info(VaultStore)";
        using var leitor = comandoColunas.ExecuteReader();

        var colunas = new List<(string Nome, string Tipo, long NotNull, long Pk)>();
        while (leitor.Read())
        {
            colunas.Add((leitor.GetString(1), leitor.GetString(2), leitor.GetInt64(3), leitor.GetInt64(5)));
        }

        colunas.Select(c => c.Nome).Should().BeEquivalentTo(
            ["Id", "SchemaVersion", "Salt", "EncryptedBlob", "UpdatedAt"]);

        colunas.Single(c => c.Nome == "Id").Pk.Should().Be(1);
        colunas.Should().OnlyContain(c => c.NotNull == 1, "todas as colunas do registro único são obrigatórias");
    }

    [Fact]
    public void Migrate_ChamadoDuasVezes_NoMesmoBanco_DeveSerIdempotente()
    {
        using var conexao = new SqliteConnection("Data Source=:memory:");
        conexao.Open();

        var act = () =>
        {
            using var primeiro = CriarContextoMigrado(conexao);
            using var segundo = CriarContextoMigrado(conexao);
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void Migrate_AposAplicar_DeveRegistrarInitialCreateNoHistorico()
    {
        using var conexao = new SqliteConnection("Data Source=:memory:");
        conexao.Open();
        using var contexto = CriarContextoMigrado(conexao);

        contexto.Database.GetAppliedMigrations().Should().ContainSingle()
            .Which.Should().EndWith("InitialCreate");
    }

    [Fact]
    public void Migrate_AposAplicar_NaoDeveHaverMudancasPendentesNoModelo()
    {
        using var conexao = new SqliteConnection("Data Source=:memory:");
        conexao.Open();
        using var contexto = CriarContextoMigrado(conexao);

        contexto.Database.HasPendingModelChanges().Should().BeFalse(
            "qualquer alteração no modelo exige uma nova migration antes de ser usada");
    }

    [Fact]
    public async Task Migrate_EmBancoMigrado_RepositorioDeveFazerRoundTripDoCofre()
    {
        using var conexao = new SqliteConnection("Data Source=:memory:");
        conexao.Open();

        var cryptoService = new CryptoService(
            argon2MemorySizeInKb: 32,
            argon2Iterations: 1,
            argon2DegreeOfParallelism: 1);

        using var contexto = CriarContextoMigrado(conexao);
        var repository = new VaultRepository(contexto, cryptoService);

        var vault = Vault.CreateNew();
        vault.AddItem("GitHub", "senha123", "Dev");
        var salt = cryptoService.GenerateSalt();
        await repository.CreateAsync(vault, cryptoService.DeriveKey(SenhaMestra, salt), salt, CancellationToken.None);

        var carregado = await repository.LoadAsync(cryptoService.DeriveKey(SenhaMestra, salt), CancellationToken.None);

        carregado.Should().NotBeNull();
        carregado!.Id.Should().Be(vault.Id);
        carregado.Items.Should().ContainSingle().Which.Title.Should().Be("GitHub");
    }

    [Fact]
    public void ApplyMigrations_EmBancoLegadoCriadoComEnsureCreated_DeveRegistrarBaselineEPreservarDados()
    {
        using var conexao = new SqliteConnection("Data Source=:memory:");
        conexao.Open();

        var idOriginal = Guid.NewGuid();
        using (var contextoLegado = CriarContexto(conexao))
        {
            // Simula um banco criado pela versão antiga do app (EnsureCreated,
            // sem histórico de migrations) que já tem cofre persistido.
            contextoLegado.Database.EnsureCreated();
            contextoLegado.Vaults.Add(new VaultRecord
            {
                Id = idOriginal,
                SchemaVersion = 1,
                Salt = [1, 2, 3, 4],
                EncryptedBlob = [5, 6, 7, 8],
                UpdatedAt = DateTime.UtcNow,
            });
            contextoLegado.SaveChanges();
        }

        var act = () =>
        {
            using var contextoMigrado = CriarContexto(conexao);
            VaultDatabaseMigrator.ApplyMigrations(contextoMigrado);
        };

        act.Should().NotThrow("o baseline deve registrar a migration inicial sem recriar a tabela");

        using (var contextoVerificacao = CriarContexto(conexao))
        {
            contextoVerificacao.Database.GetAppliedMigrations().Should().ContainSingle()
                .Which.Should().EndWith("InitialCreate");
            contextoVerificacao.Vaults.AsNoTracking().Should().ContainSingle()
                .Which.Id.Should().Be(idOriginal);
        }
    }

    [Fact]
    public void ApplyMigrations_ComHistoricoVazioETabelaExistente_DeveRegistrarBaselineEPreservarDados()
    {
        using var conexao = new SqliteConnection("Data Source=:memory:");
        conexao.Open();

        var idOriginal = Guid.NewGuid();
        using (var contextoLegado = CriarContexto(conexao))
        {
            // Simula o estado deixado por um Migrate() que falhou logo após
            // criar o histórico: tabela VaultStore existe (EnsureCreated) e
            // __EFMigrationsHistory existe, porém vazia.
            contextoLegado.Database.EnsureCreated();
            using var criarHistoricoVazio = conexao.CreateCommand();
            criarHistoricoVazio.CommandText =
                """
                CREATE TABLE "__EFMigrationsHistory" (
                    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                    "ProductVersion" TEXT NOT NULL);
                """;
            criarHistoricoVazio.ExecuteNonQuery();

            contextoLegado.Vaults.Add(new VaultRecord
            {
                Id = idOriginal,
                SchemaVersion = 1,
                Salt = [1, 2, 3, 4],
                EncryptedBlob = [5, 6, 7, 8],
                UpdatedAt = DateTime.UtcNow,
            });
            contextoLegado.SaveChanges();
        }

        var act = () =>
        {
            using var contextoMigrado = CriarContexto(conexao);
            VaultDatabaseMigrator.ApplyMigrations(contextoMigrado);
        };

        act.Should().NotThrow("o baseline deve ser registrado mesmo com o histórico já criado e vazio");

        using (var contextoVerificacao = CriarContexto(conexao))
        {
            contextoVerificacao.Database.GetAppliedMigrations().Should().ContainSingle()
                .Which.Should().EndWith("InitialCreate");
            contextoVerificacao.Vaults.AsNoTracking().Should().ContainSingle()
                .Which.Id.Should().Be(idOriginal);
        }
    }

    [Fact]
    public void ApplyMigrations_EmBancoNovo_DeveAplicarMigrationInicialESerIdempotente()
    {
        using var conexao = new SqliteConnection("Data Source=:memory:");
        conexao.Open();

        var act = () =>
        {
            using var primeiro = CriarContexto(conexao);
            VaultDatabaseMigrator.ApplyMigrations(primeiro);
            using var segundo = CriarContexto(conexao);
            VaultDatabaseMigrator.ApplyMigrations(segundo);
        };

        act.Should().NotThrow();

        using var contexto = CriarContexto(conexao);
        contexto.Database.GetAppliedMigrations().Should().ContainSingle()
            .Which.Should().EndWith("InitialCreate");
    }

    private static VaultDbContext CriarContexto(SqliteConnection conexao)
    {
        var options = new DbContextOptionsBuilder<VaultDbContext>()
            .UseSqlite(conexao)
            .Options;

        return new VaultDbContext(options);
    }

    private static VaultDbContext CriarContextoMigrado(SqliteConnection conexao)
    {
        var options = new DbContextOptionsBuilder<VaultDbContext>()
            .UseSqlite(conexao)
            .Options;

        var contexto = new VaultDbContext(options);
        contexto.Database.Migrate();
        return contexto;
    }
}
