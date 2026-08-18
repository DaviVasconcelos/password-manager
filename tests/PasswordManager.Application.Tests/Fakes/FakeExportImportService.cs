using PasswordManager.Application.Abstractions;
using PasswordManager.Application.Exceptions;
using PasswordManager.Domain.Entities;

namespace PasswordManager.Application.Tests.Fakes;

/// <summary>
/// Fake do <see cref="IExportImportService"/> para os testes de sessão: a
/// exportação apenas registra o cofre passado e devolve bytes de "arquivo"
/// fictícios; a importação devolve um <see cref="Vault"/> configurável pelo
/// teste. Permite verificar a orquestração de replace/merge/primeira
/// execução sem depender do formato real do .vault.
/// </summary>
internal sealed class FakeExportImportService : IExportImportService
{
    public Vault? VaultExportado { get; private set; }
    public byte[] UltimosDadosImportados { get; private set; } = Array.Empty<byte>();
    public string? UltimaSenhaImportacao { get; private set; }

    /// <summary>
    /// Cofre que será devolvido pela importação. O teste define o conteúdo
    /// para simular o arquivo lido.
    /// </summary>
    public Vault VaultParaImportar { get; set; } = Vault.CreateNew();

    /// <summary>
    /// Senha esperada na importação. Se informada e diferente, o fake lança
    /// <see cref="CryptographicIntegrityException"/> para simular senha
    /// errada (falha de autenticação no AES-GCM real).
    /// </summary>
    public string? SenhaEsperada { get; set; }

    public byte[] Export(Vault vault, string masterPassword)
    {
        VaultExportado = vault;
        return new byte[] { 0x56, 0x41, 0x55, 0x4C };
    }

    public Vault Import(byte[] fileData, string masterPassword)
    {
        UltimosDadosImportados = fileData;
        UltimaSenhaImportacao = masterPassword;

        if (SenhaEsperada is not null && masterPassword != SenhaEsperada)
            throw new CryptographicIntegrityException("Senha incorreta (simulação de falha de autenticação).");

        return VaultParaImportar;
    }
}