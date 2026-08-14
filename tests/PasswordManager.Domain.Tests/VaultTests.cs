namespace PasswordManager.Domain.Tests;
using PasswordManager.Domain.Entities;
using FluentAssertions;

public class VaultTests
{
    [Fact]
    public void CreateNew_DeveGerarVaultComIdValidoEListaVazia()
    {
        var vault = Vault.CreateNew();

        vault.Id.Should().NotBeEmpty();
        vault.Items.Should().BeEmpty();
    }

    [Fact]
    public void AddItem_ComDadosValidos_DeveAdicionarItemAoVault()
    {
        var vault = Vault.CreateNew();

        var item = vault.AddItem("GitHub", "senha123", "Dev", username: "meu_user");

        vault.Items.Should().ContainSingle();
        vault.Items.Should().Contain(item);
    }

    [Fact]
    public void AddItem_ChamadoDuasVezes_DeveManterAmbosOsItens()
    {
        var vault = Vault.CreateNew();

        vault.AddItem("GitHub", "senha123", "Dev");
        vault.AddItem("Gmail", "senha456", "Email");

        vault.Items.Should().HaveCount(2);
    }

    [Fact]
    public void RemoveItem_ComIdExistente_DeveRemoverItem()
    {
        var vault = Vault.CreateNew();
        var item = vault.AddItem("GitHub", "senha123", "Dev");

        vault.RemoveItem(item.Id);

        vault.Items.Should().BeEmpty();
    }

    [Fact]
    public void RemoveItem_ComIdInexistente_DeveLancarExcecao()
    {
        var vault = Vault.CreateNew();
        var idInexistente = Guid.NewGuid();

        var act = () => vault.RemoveItem(idInexistente);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{idInexistente}*");
    }

    [Fact]
    public void UpdateItem_ComIdExistente_DeveAtualizarCamposDoItem()
    {
        var vault = Vault.CreateNew();
        var item = vault.AddItem("GitHub", "senha123", "Dev", username: "davi");

        vault.UpdateItem(item.Id, "GitHub Enterprise", "nova-senha", "Trabalho", username: "davi@acme");

        item.Title.Should().Be("GitHub Enterprise");
        item.Password.Should().Be("nova-senha");
        item.Category.Should().Be("Trabalho");
        item.Username.Should().Be("davi@acme");
        item.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void UpdateItem_ComIdInexistente_DeveLancarExcecao()
    {
        var vault = Vault.CreateNew();
        var idInexistente = Guid.NewGuid();

        var act = () => vault.UpdateItem(idInexistente, "Título", "senha", "Categoria");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{idInexistente}*");
    }

    [Fact]
    public void RenameFolder_ComIdExistente_DeveRenomearPasta()
    {
        var vault = Vault.CreateNew();
        var pasta = vault.AddFolder("Trabalho");

        vault.RenameFolder(pasta.Id, "Pessoal");

        pasta.Name.Should().Be("Pessoal");
    }

    [Fact]
    public void RenameFolder_ComIdInexistente_DeveLancarExcecao()
    {
        var vault = Vault.CreateNew();
        var idInexistente = Guid.NewGuid();

        var act = () => vault.RenameFolder(idInexistente, "Nova");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{idInexistente}*");
    }

    [Fact]
    public void RenameFolder_ComNomeVazio_DeveLancarExcecaoESemAlterarNome()
    {
        var vault = Vault.CreateNew();
        var pasta = vault.AddFolder("Trabalho");

        var act = () => vault.RenameFolder(pasta.Id, "");

        act.Should().Throw<ArgumentException>();
        pasta.Name.Should().Be("Trabalho");
    }

    [Fact]
    public void Items_DeveSerReadOnly_NaoPermitindoMutacaoExterna()
    {
        var vault = Vault.CreateNew();
        vault.AddItem("GitHub", "senha123", "Dev");

        vault.Items.Should().BeAssignableTo<IReadOnlyCollection<VaultItem>>();
    }

    public class VaultRehydrateTests
    {
        [Fact]
        public void Rehydrate_Should_Set_Id_Items_And_Folders()
        {
            var vaultId = Guid.NewGuid();
            var folder = VaultFolder.Rehydrate(Guid.NewGuid(), "Trabalho", DateTime.UtcNow);
            var item = VaultItem.Rehydrate(
                Guid.NewGuid(), "Gmail", "pass", "Email",
                null, null, null, folder.Id, DateTime.UtcNow, DateTime.UtcNow);

            var vault = Vault.Rehydrate(vaultId, new[] { item }, new[] { folder });

            vault.Id.Should().Be(vaultId);
            vault.Items.Should().ContainSingle().Which.Id.Should().Be(item.Id);
            vault.Folders.Should().ContainSingle().Which.Id.Should().Be(folder.Id);
        }

        [Fact]
        public void Rehydrate_With_Empty_Collections_Should_Create_Empty_Vault()
        {
            var vault = Vault.Rehydrate(Guid.NewGuid(), Enumerable.Empty<VaultItem>(),
                Enumerable.Empty<VaultFolder>());

            vault.Items.Should().BeEmpty();
            vault.Folders.Should().BeEmpty();
        }

        [Fact]
        public void Rehydrate_Should_Preserve_Item_Folder_Association()
        {
            var folder = VaultFolder.Rehydrate(Guid.NewGuid(), "Pessoal", DateTime.UtcNow);
            var item = VaultItem.Rehydrate(
                Guid.NewGuid(), "Netflix", "pass", "Streaming",
                null, null, null, folder.Id, DateTime.UtcNow, DateTime.UtcNow);

            var vault = Vault.Rehydrate(Guid.NewGuid(), new[] { item }, new[] { folder });

            vault.Items.Single().FolderId.Should().Be(folder.Id);
        }

        [Fact]
        public void Rehydrate_Should_Keep_Items_Externally_ReadOnly()
        {
            // Mesma garantia de imutabilidade externa que já vale para CreateNew.
            var vault = Vault.Rehydrate(Guid.NewGuid(),
                Enumerable.Empty<VaultItem>(), Enumerable.Empty<VaultFolder>());

            vault.Items.Should().BeAssignableTo<IReadOnlyCollection<VaultItem>>();
            vault.Folders.Should().BeAssignableTo<IReadOnlyCollection<VaultFolder>>();
        }
    }
}