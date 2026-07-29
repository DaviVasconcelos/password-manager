namespace PasswordManager.Domain.Tests;
using PasswordManager.Domain.Entities;
using FluentAssertions;

public class VaultItemTests
{
    [Fact]
    public void Create_ComDadosValidos_DeveCriarItem()
    {
        var item = VaultItem.Create("GitHub", "senha123", "Dev", username: "meu_user");

        item.Title.Should().Be("GitHub");
        item.Id.Should().NotBeEmpty();
        item.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_ComTituloInvalido_DeveLancarExcecao(string? titulo)
    {
        var act = () => VaultItem.Create(titulo!, "senha123", "Dev");
        act.Should().Throw<ArgumentException>();
    }
}