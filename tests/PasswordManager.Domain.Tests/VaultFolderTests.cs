using FluentAssertions;
using PasswordManager.Domain.Entities;

namespace PasswordManager.Tests.Domain.Entities;

public class VaultFolderTests
{
    // Create

    [Fact]
    public void Create_Should_Set_Name()
    {
        var folder = VaultFolder.Create("Trabalho");

        folder.Name.Should().Be("Trabalho");
    }

    [Fact]
    public void Create_Should_Generate_Id()
    {
        var folder = VaultFolder.Create("Trabalho");

        folder.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Create_Should_Set_CreatedAt()
    {
        var before = DateTime.UtcNow;

        var folder = VaultFolder.Create("Trabalho");

        var after = DateTime.UtcNow;

        folder.CreatedAt.Should().BeOnOrAfter(before);
        folder.CreatedAt.Should().BeOnOrBefore(after);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void Create_Should_Throw_When_Name_Is_Empty(string name)
    {
        var act = () => VaultFolder.Create(name);

        act.Should()
            .Throw<ArgumentException>()
            .WithMessage("*Nome da pasta não pode ser vazio.*");
    }

    [Fact]
    public void Create_Should_Trim_Name()
    {
        var folder = VaultFolder.Create("  Trabalho  ");

        folder.Name.Should().Be("Trabalho");
    }

    // Rename

    [Fact]
    public void Rename_Should_Change_Name()
    {
        var folder = VaultFolder.Create("Trabalho");

        folder.Rename("Pessoal");

        folder.Name.Should().Be("Pessoal");
    }

    [Fact]
    public void Rename_Should_Trim_Name()
    {
        var folder = VaultFolder.Create("Trabalho");

        folder.Rename("  Pessoal  ");

        folder.Name.Should().Be("Pessoal");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void Rename_Should_Throw_When_Name_Is_Empty(string name)
    {
        var folder = VaultFolder.Create("Trabalho");

        var act = () => folder.Rename(name);

        act.Should()
            .Throw<ArgumentException>()
            .WithMessage("*Nome da pasta não pode ser vazio.*");
    }

    [Fact]
    public void Rename_Should_Not_Change_Name_When_Invalid()
    {
        var folder = VaultFolder.Create("Trabalho");

        var act = () => folder.Rename("");

        act.Should().Throw<ArgumentException>();
        folder.Name.Should().Be("Trabalho");
    }

    // Rehydrate

    [Fact]
    public void Rehydrate_Should_Set_All_Properties_Correctly()
    {
        var id = Guid.NewGuid();
        var createdAt = new DateTime(2024, 2, 20, 10, 0, 0, DateTimeKind.Utc);

        var folder = VaultFolder.Rehydrate(
            id,
            "Trabalho",
            createdAt);

        folder.Id.Should().Be(id);
        folder.Name.Should().Be("Trabalho");
        folder.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void Rehydrate_Should_Preserve_CreatedAt_Exactly()
    {
        var createdAt = new DateTime(
            2022,
            1,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc);

        var folder = VaultFolder.Rehydrate(
            Guid.NewGuid(),
            "Pasta",
            createdAt);

        folder.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void Rehydrate_Should_Not_Reexecute_Create_Validation()
    {
        var act = () => VaultFolder.Rehydrate(
            Guid.NewGuid(),
            "",
            DateTime.UtcNow);

        act.Should().NotThrow();
    }
}