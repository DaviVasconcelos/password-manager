namespace PasswordManager.Domain.Entities;

public class Vault
{
    private readonly List<VaultItem> _items = new();

    public Guid Id { get; private set; }
    public IReadOnlyCollection<VaultItem> Items => _items.AsReadOnly();

    private Vault() { }

    public static Vault CreateNew()
    {
        var vault = new Vault();

        vault.Id = Guid.NewGuid();

        return vault;
    }

    public VaultItem AddItem(string title, string password, string category,
        string? username = null, string? url = null, string? notes = null)
    {
        var item = VaultItem.Create(title, password, category, username, url, notes);
        _items.Add(item);
        return item;
    }

    public void RemoveItem(Guid itemId)
    {
        VaultItem? item = null;

        foreach (var i in _items)
        {
            if (i.Id == itemId)
            {
                item = i;
                break;
            }
        }

        if (item is null)
            throw new InvalidOperationException($"Item {itemId} não encontrado no cofre.");

        _items.Remove(item);
    }
}