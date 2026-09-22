using System.Text.Json;
using FluentAssertions;
using PasswordManager.Domain.Entities;
using PasswordManager.Infrastructure.Persistence.Serialization;

namespace PasswordManager.Infrastructure.Tests;

/// <summary>
/// Testes do <see cref="VaultDataMapper"/> (serialização do blob),
/// incluindo compatibilidade com JSON gravado antes da ADR 0009.
/// </summary>
public class VaultDataMapperTests
{
    private const string SecretValido = "JBSWY3DPEHPK3PXP";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void FromVault_ToVault_RoundTripComTotp_DevePreservarSecret()
    {
        var vault = Vault.CreateNew();
        var item = vault.AddItem("GitHub", "senha123", "Dev", totpSecret: SecretValido);
        vault.AddItem("Gmail", "senha456", "Email");

        var restaurado = VaultDataMapper.ToVault(VaultDataMapper.FromVault(vault));

        restaurado.Items.Single(i => i.Id == item.Id).TotpSecret.Should().Be(SecretValido);
        restaurado.Items.Single(i => i.Title == "Gmail").TotpSecret.Should().BeNull();
    }

    [Fact]
    public void ToVault_ComJsonLegadoSemTotp_TotpSecretDeveSerNulo()
    {
        // JSON no formato gravado antes da ADR 0009: sem a propriedade TotpSecret.
        var idVault = Guid.NewGuid();
        var idItem = Guid.NewGuid();
        var jsonLegado = $$"""
            {"Id":"{{idVault}}","Items":[{"Id":"{{idItem}}","FolderId":null,"Title":"GitHub","Username":null,"Password":"senha123","Url":null,"Notes":null,"Category":"Dev","CreatedAt":"2024-01-10T00:00:00Z","UpdatedAt":"2024-01-10T00:00:00Z"}],"Folders":[]}
            """;

        var data = JsonSerializer.Deserialize<VaultData>(jsonLegado, JsonOptions);

        var restaurado = VaultDataMapper.ToVault(data!);

        restaurado.Id.Should().Be(idVault);
        restaurado.Items.Should().ContainSingle()
            .Which.TotpSecret.Should().BeNull();
    }
}
