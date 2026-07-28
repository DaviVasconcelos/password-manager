namespace PasswordManager.Domain.Entities;

public class Vault
{
    private readonly List<VaultItem> _items = new();

    public Guid Id { get; private set; }
    public IReadOnlyCollection<VaultItem> Items => _items.AsReadOnly();

    private Vault() { }

    public static Vault CreateNew() => new() { Id = Guid.NewGuid() };

    public VaultItem AddItem(string title, string password, string category,
        string? username = null, string? url = null, string? notes = null)
    {
        var item = VaultItem.Create(title, password, category, username, url, notes);
        _items.Add(item);
        return item;
    }

    public void RemoveItem(Guid itemId)
    {
        var item = _items.FirstOrDefault(i => i.Id == itemId);
        if (item is null)
            throw new InvalidOperationException($"Item {itemId} não encontrado no cofre.");
        _items.Remove(item);
    }
}