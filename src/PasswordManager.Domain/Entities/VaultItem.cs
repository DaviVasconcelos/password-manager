namespace PasswordManager.Domain.Entities;

public class VaultItem
{
    public Guid Id { get; private set; }
    public Guid? FolderId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Username { get; private set; }
    public string Password { get; private set; } = string.Empty;
    public string? Url { get; private set; }
    public string? Notes { get; private set; }
    public string Category { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private VaultItem() { } // EF Core needs
    private VaultItem(Guid id, string title, string password, string category,
        string? username, string? url, string? notes, DateTime createdAt)
    {
        Id = id;
        Title = title;
        Password = password;
        Category = category;
        Username = username;
        Url = url;
        Notes = notes;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    private VaultItem(Guid id, string title, string password, string category,
    string? username, string? url, string? notes, Guid? folderId,
    DateTime createdAt, DateTime updatedAt)
    {
        Id = id;
        Title = title;
        Password = password;
        Category = category;
        Username = username;
        Url = url;
        Notes = notes;
        FolderId = folderId;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// Reconstrói um VaultItem a partir de dados já persistidos (usado pela
    /// Infrastructure ao desserializar o cofre). Diferente de Create: não
    /// valida nem normaliza campos, pois assume que os dados já passaram
    /// por validação no momento em que foram salvos originalmente.
    /// </summary>
    internal static VaultItem Rehydrate(Guid id, string title, string password, string category,
        string? username, string? url, string? notes, Guid? folderId,
        DateTime createdAt, DateTime updatedAt)
    {
        return new VaultItem(id, title, password, category, username, url, notes,
            folderId, createdAt, updatedAt);
    }

    public static VaultItem Create(string title, string password, string category,
        string? username = null, string? url = null, string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Título não pode ser vazio.", nameof(title));
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Senha não pode ser vazia.", nameof(password));
        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("Categoria não pode ser vazia.", nameof(category));

        return new VaultItem(Guid.NewGuid(), title.Trim(), password, category.Trim(),
            username?.Trim(), url?.Trim(), notes?.Trim(), DateTime.UtcNow);
    }

    public void UpdateDetails(string title, string password, string category,
        string? username, string? url, string? notes)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Título não pode ser vazio.", nameof(title));

        Title = title.Trim();
        Password = password;
        Category = category.Trim();
        Username = username?.Trim();
        Url = url?.Trim();
        Notes = notes?.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    internal void AssignToFolder(Guid? folderId)
    {
        FolderId = folderId;
    }
}