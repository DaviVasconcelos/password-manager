using System.Data;
using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PasswordManager.Infrastructure.Persistence;

/// <summary>
/// Aplica as migrations do EF Core ao banco, tratando bancos legados criados
/// com <c>Database.EnsureCreated()</c>: esses bancos já têm a tabela
/// <c>VaultStore</c>, mas não registram a migration inicial no histórico
/// (<c>__EFMigrationsHistory</c>) — nem quando a tabela de histórico existe
/// porém vazia (ex.: um <c>Migrate()</c> anterior que falhou logo após criar
/// o histórico). Sem o ajuste, o <c>Migrate()</c> tentaria recriar a tabela e
/// falharia com "table VaultStore already exists". Nesses casos, o histórico é
/// criado (se faltar) e a migration inicial é registrada como já aplicada
/// (baseline) antes do <c>Migrate()</c> — os dados existentes são preservados.
/// </summary>
public static class VaultDatabaseMigrator
{
    public static void ApplyMigrations(VaultDbContext context)
    {
        RegistrarBaselineDeBancoLegado(context);
        context.Database.Migrate();
    }

    private static void RegistrarBaselineDeBancoLegado(VaultDbContext context)
    {
        var migrationInicial = context.Database.GetMigrations().FirstOrDefault();
        if (migrationInicial is null)
        {
            return;
        }

        var conexao = context.Database.GetDbConnection();
        var fecharConexao = false;
        if (conexao.State != ConnectionState.Open)
        {
            conexao.Open();
            fecharConexao = true;
        }

        try
        {
            // Banco novo: nada a baselinhar; o Migrate() cuida de tudo.
            if (!TabelaExiste(conexao, "VaultStore"))
            {
                return;
            }

            using (var criarHistorico = conexao.CreateCommand())
            {
                criarHistorico.CommandText =
                    """
                    CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                        "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                        "ProductVersion" TEXT NOT NULL);
                    """;
                criarHistorico.ExecuteNonQuery();
            }

            using (var verificarBaseline = conexao.CreateCommand())
            {
                verificarBaseline.CommandText = "SELECT COUNT(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = $id";
                verificarBaseline.Parameters.Add(new SqliteParameter("$id", migrationInicial));
                if (Convert.ToInt64(verificarBaseline.ExecuteScalar()!) > 0)
                {
                    // Banco já usa migrations e a inicial está registrada.
                    return;
                }
            }

            using (var inserirBaseline = conexao.CreateCommand())
            {
                inserirBaseline.CommandText = "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ($id, $versao)";
                inserirBaseline.Parameters.Add(new SqliteParameter("$id", migrationInicial));
                inserirBaseline.Parameters.Add(new SqliteParameter("$versao", ObterVersaoEf()));
                inserirBaseline.ExecuteNonQuery();
            }
        }
        finally
        {
            if (fecharConexao)
            {
                conexao.Close();
            }
        }
    }

    private static bool TabelaExiste(System.Data.Common.DbConnection conexao, string nomeTabela)
    {
        using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $nome";
        comando.Parameters.Add(new SqliteParameter("$nome", nomeTabela));
        return Convert.ToInt64(comando.ExecuteScalar()!) > 0;
    }

    private static string ObterVersaoEf()
    {
        var versaoCompleta = typeof(Migration).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        return versaoCompleta?.Split('+')[0] ?? string.Empty;
    }
}
