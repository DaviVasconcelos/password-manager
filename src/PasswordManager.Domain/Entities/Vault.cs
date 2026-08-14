namespace PasswordManager.Domain.Entities;

public class Vault
{
    private readonly List<VaultItem> _items = new();

    public Guid Id { get; private set; }
    public IReadOnlyCollection<VaultItem> Items => _items.AsReadOnly();

    private Vault() { }
    private readonly List<VaultFolder> _folders = new();
    public IReadOnlyCollection<VaultFolder> Folders => _folders.AsReadOnly();

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

    public void UpdateItem(Guid itemId, string title, string password, string category,
        string? username = null, string? url = null, string? notes = null)
    {
        var item = _items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException($"Item {itemId} não encontrado no cofre.");

        item.UpdateDetails(title, password, category, username, url, notes);
    }

    public VaultFolder AddFolder(string name)
    {
        var folder = VaultFolder.Create(name);
        _folders.Add(folder);
        return folder;
    }

    public void RemoveFolder(Guid folderId)
    {
        var folder = _folders.FirstOrDefault(f => f.Id == folderId);
        if (folder is null)
            throw new InvalidOperationException($"Pasta {folderId} não encontrada no cofre.");

        // folder items sholdnt be deleted when the folder is, just turns to no folder
        foreach (var item in _items.Where(i => i.FolderId == folderId))
            item.AssignToFolder(null);

        _folders.Remove(folder);
    }

    public void RenameFolder(Guid folderId, string name)
    {
        var folder = _folders.FirstOrDefault(f => f.Id == folderId)
            ?? throw new InvalidOperationException($"Pasta {folderId} não encontrada no cofre.");

        folder.Rename(name);
    }

    public void AssignItemToFolder(Guid itemId, Guid? folderId)
    {
        VaultItem? item = _items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException($"Item {itemId} não encontrado no cofre.");

        if (folderId != null)
        {
            bool pastaExiste = _folders.Any(f => f.Id == folderId);

            if (!pastaExiste)
            {
                throw new InvalidOperationException($"Pasta {folderId} não encontrada no cofre.");
            }
        }

        item.AssignToFolder(folderId);
    }

    /// <summary>
    /// Reconstrói um Vault a partir de dados já persistidos (usado pela
    /// Infrastructure ao carregar do storage). Diferente de CreateNew: não
    /// gera nova identidade, apenas remonta o agregado com itens e pastas
    /// já existentes.
    /// </summary>
    public static Vault Rehydrate(Guid id, IEnumerable<VaultItem> items, IEnumerable<VaultFolder> folders)
    {
        var vault = new Vault { Id = id };
        vault._items.AddRange(items);
        vault._folders.AddRange(folders);
        return vault;
    }
}