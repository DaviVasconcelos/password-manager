using PasswordManager.Application.Abstractions;
using PasswordManager.Application.Exceptions;
using PasswordManager.Domain.Entities;

namespace PasswordManager.UI.Tests.Fakes;

/// <summary>
/// Fake de <see cref="IExportImportService"/> para testes de ViewModel.
/// </summary>
internal sealed class FakeExportImportService : IExportImportService
{
    public Vault VaultParaImportar { get; set; } = Vault.CreateNew();
    public string? SenhaEsperada { get; set; }

    public byte[] Export(Vault vault, string masterPassword) => new byte[] { 0x56, 0x41, 0x55, 0x4C };

    public Vault Import(byte[] fileData, string masterPassword)
    {
        if (SenhaEsperada is not null && masterPassword != SenhaEsperada)
            throw new CryptographicIntegrityException("Senha incorreta (simulação de falha de autenticação).");
        return VaultParaImportar;
    }
}
