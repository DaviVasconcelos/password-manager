namespace PasswordManager.Domain.Entities;

public class VaultFolder
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    private VaultFolder() { }

    private VaultFolder(Guid id, string name, DateTime createdAt)
    {
        Id = id;
        Name = name;
        CreatedAt = createdAt;
    }

    public static VaultFolder Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Nome da pasta não pode ser vazio.", nameof(name));

        return new VaultFolder(Guid.NewGuid(), name.Trim(), DateTime.UtcNow);
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Nome da pasta não pode ser vazio.", nameof(name));

        Name = name.Trim();
    }

    internal static VaultFolder Rehydrate(Guid id, string name, DateTime createdAt)
    {
        return new VaultFolder(id, name, createdAt);
    }
}