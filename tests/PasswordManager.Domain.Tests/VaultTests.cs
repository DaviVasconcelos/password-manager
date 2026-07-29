namespace PasswordManager.Domain.Tests;

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
    public void Items_DeveSerReadOnly_NaoPermitindoMutacaoExterna()
    {
        var vault = Vault.CreateNew();
        vault.AddItem("GitHub", "senha123", "Dev");

        vault.Items.Should().BeAssignableTo<IReadOnlyCollection<VaultItem>>();
    }
}