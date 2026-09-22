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

    [Fact]
    public void UpdateDetails_ComDadosValidos_DeveAtualizarCampos()
    {
        var item = VaultItem.Create("GitHub", "senha123", "Dev", username: "davi");

        item.UpdateDetails("GitHub Enterprise", "nova-senha", "Trabalho",
            username: "davi@acme", url: "https://github.com", notes: "mudou");

        item.Title.Should().Be("GitHub Enterprise");
        item.Password.Should().Be("nova-senha");
        item.Category.Should().Be("Trabalho");
        item.Username.Should().Be("davi@acme");
        item.Url.Should().Be("https://github.com");
        item.Notes.Should().Be("mudou");
        item.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void UpdateDetails_ComTituloInvalido_DeveLancarExcecao(string? titulo)
    {
        var item = VaultItem.Create("GitHub", "senha123", "Dev");

        var act = () => item.UpdateDetails(titulo!, "senha123", "Dev");

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void UpdateDetails_ComSenhaInvalida_DeveLancarExcecao(string? senha)
    {
        var item = VaultItem.Create("GitHub", "senha123", "Dev");

        var act = () => item.UpdateDetails("GitHub", senha!, "Dev");

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void UpdateDetails_ComCategoriaInvalida_DeveLancarExcecao(string? categoria)
    {
        var item = VaultItem.Create("GitHub", "senha123", "Dev");

        var act = () => item.UpdateDetails("GitHub", "senha123", categoria!);

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

    public class VaultItemTotpTests
    {
        private const string SecretValido = "JBSWY3DPEHPK3PXP"; // 10 bytes decodificados
        private const string SecretRfc6238 = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ"; // 20 bytes

        [Fact]
        public void Create_ComTotpValido_DeveNormalizarESalvar()
        {
            var item = VaultItem.Create("GitHub", "senha123", "Dev", totpSecret: "jbsw y3dp-ehpk 3pxp");

            item.TotpSecret.Should().Be(SecretValido);
        }

        [Fact]
        public void Create_SemTotp_TotpSecretDeveSerNulo()
        {
            var item = VaultItem.Create("GitHub", "senha123", "Dev");

            item.TotpSecret.Should().BeNull();
        }

        [Theory]
        [InlineData("ABC!123")]
        [InlineData("AAAA")] // curto demais: decodifica menos de 10 bytes
        [InlineData("JBSWY3DP EH PK 3PXP!!!!")]
        public void Create_ComTotpInvalido_DeveLancarArgumentException(string secret)
        {
            var act = () => VaultItem.Create("GitHub", "senha123", "Dev", totpSecret: secret);

            act.Should().Throw<ArgumentException>().WithParameterName("totpSecret");
        }

        [Fact]
        public void DefinirTotpSecret_ComValorValido_DeveAtualizarSecret()
        {
            var item = VaultItem.Create("GitHub", "senha123", "Dev");

            item.DefinirTotpSecret(SecretRfc6238.ToLowerInvariant());

            item.TotpSecret.Should().Be(SecretRfc6238);
            item.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void DefinirTotpSecret_ComNuloOuVazio_DeveRemoverSecret(string? secret)
        {
            var item = VaultItem.Create("GitHub", "senha123", "Dev", totpSecret: SecretValido);

            item.DefinirTotpSecret(secret);

            item.TotpSecret.Should().BeNull();
        }

        [Fact]
        public void RemoverTotpSecret_ComSecretDefinido_DeveLimparSecret()
        {
            var item = VaultItem.Create("GitHub", "senha123", "Dev", totpSecret: SecretValido);

            item.RemoverTotpSecret();

            item.TotpSecret.Should().BeNull();
        }

        [Fact]
        public void UpdateDetails_ComTotp_DeveAtualizarSecret()
        {
            var item = VaultItem.Create("GitHub", "senha123", "Dev");

            item.UpdateDetails("GitHub", "senha123", "Dev", totpSecret: SecretValido);

            item.TotpSecret.Should().Be(SecretValido);
        }

        [Fact]
        public void UpdateDetails_SemTotp_DeveLimparSecret()
        {
            var item = VaultItem.Create("GitHub", "senha123", "Dev", totpSecret: SecretValido);

            item.UpdateDetails("GitHub", "senha123", "Dev");

            item.TotpSecret.Should().BeNull();
        }

        [Fact]
        public void Rehydrate_ComTotp_DevePreservarSecret()
        {
            var item = VaultItem.Rehydrate(
                Guid.NewGuid(), "GitHub", "senha123", "Dev",
                null, null, null, null, SecretValido, DateTime.UtcNow, DateTime.UtcNow);

            item.TotpSecret.Should().Be(SecretValido);
        }

        [Fact]
        public void Rehydrate_LegadoSemTotp_TotpSecretDeveSerNulo()
        {
            // Overload antigo (cofres gravados antes da ADR 0009): sem 2FA.
            var item = VaultItem.Rehydrate(
                Guid.NewGuid(), "GitHub", "senha123", "Dev",
                null, null, null, null, DateTime.UtcNow, DateTime.UtcNow);

            item.TotpSecret.Should().BeNull();
        }
    }
}