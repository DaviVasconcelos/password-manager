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

    public class VaultItemRehydrateTests
    {
        [Fact]
        public void Rehydrate_Should_Set_All_Properties_Correctly()
        {
            var id = Guid.NewGuid();
            var folderId = Guid.NewGuid();
            var createdAt = new DateTime(2024, 1, 10, 0, 0, 0, DateTimeKind.Utc);
            var updatedAt = new DateTime(2024, 3, 5, 0, 0, 0, DateTimeKind.Utc);

            var item = VaultItem.Rehydrate(
                id: id,
                title: "Gmail",
                password: "super-secret",
                category: "Email",
                username: "user@gmail.com",
                url: "https://gmail.com",
                notes: "conta pessoal",
                folderId: folderId,
                createdAt: createdAt,
                updatedAt: updatedAt);

            item.Id.Should().Be(id);
            item.Title.Should().Be("Gmail");
            item.Password.Should().Be("super-secret");
            item.Category.Should().Be("Email");
            item.Username.Should().Be("user@gmail.com");
            item.Url.Should().Be("https://gmail.com");
            item.Notes.Should().Be("conta pessoal");
            item.FolderId.Should().Be(folderId);
        }

        [Fact]
        public void Rehydrate_Should_Preserve_CreatedAt_And_UpdatedAt_Exactly()
        {
            // Diferente de Create/UpdateDetails, Rehydrate NÃO deve usar
            // DateTime.UtcNow — as datas vêm do que já estava persistido.
            var createdAt = new DateTime(2023, 5, 1, 8, 30, 0, DateTimeKind.Utc);
            var updatedAt = new DateTime(2023, 6, 15, 14, 0, 0, DateTimeKind.Utc);

            var item = VaultItem.Rehydrate(
                Guid.NewGuid(), "Title", "pass", "Category",
                null, null, null, null, createdAt, updatedAt);

            item.CreatedAt.Should().Be(createdAt);
            item.UpdatedAt.Should().Be(updatedAt);
        }

        [Fact]
        public void Rehydrate_Should_Allow_Null_Optional_Fields()
        {
            var item = VaultItem.Rehydrate(
                Guid.NewGuid(), "Title", "pass", "Category",
                username: null, url: null, notes: null, folderId: null,
                createdAt: DateTime.UtcNow, updatedAt: DateTime.UtcNow);

            item.Username.Should().BeNull();
            item.Url.Should().BeNull();
            item.Notes.Should().BeNull();
            item.FolderId.Should().BeNull();
        }

        [Fact]
        public void Rehydrate_Should_Not_Reexecute_Create_Validation()
        {
            /* Documenta explicitamente a premissa: Rehydrate confia que os
               dados já foram validados no momento em que foram salvos.
               Se isso um dia lançar exceção, algo no contrato mudou e o
               teste deve ser revisitado. */
            var act = () => VaultItem.Rehydrate(
                Guid.NewGuid(),
                title: "",           // inválido para Create
                password: "",        // inválido para Create
                category: "",        // inválido para Create
                username: null, url: null, notes: null, folderId: null,
                createdAt: DateTime.UtcNow, updatedAt: DateTime.UtcNow);

            act.Should().NotThrow();
        }
    }
}