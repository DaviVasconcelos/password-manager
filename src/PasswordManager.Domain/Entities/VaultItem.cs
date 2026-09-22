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
    /// <summary>
    /// Secret TOTP do item em Base32 normalizado (maiúsculas, sem espaços).
    /// Nulo quando o item não tem 2FA (ADR 0009).
    /// </summary>
    public string? TotpSecret { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private VaultItem() { } // EF Core needs
    private VaultItem(Guid id, string title, string password, string category,
        string? username, string? url, string? notes, string? totpSecret, DateTime createdAt)
    {
        Id = id;
        Title = title;
        Password = password;
        Category = category;
        Username = username;
        Url = url;
        Notes = notes;
        TotpSecret = totpSecret;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    private VaultItem(Guid id, string title, string password, string category,
    string? username, string? url, string? notes, Guid? folderId,
    string? totpSecret, DateTime createdAt, DateTime updatedAt)
    {
        Id = id;
        Title = title;
        Password = password;
        Category = category;
        Username = username;
        Url = url;
        Notes = notes;
        FolderId = folderId;
        TotpSecret = totpSecret;
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
        return Rehydrate(id, title, password, category, username, url, notes,
            folderId, totpSecret: null, createdAt, updatedAt);
    }

    /// <summary>
    /// Reconstrói um VaultItem com secret TOTP a partir de dados já
    /// persistidos. Cofres gravados antes da ADR 0009 usam o overload sem
    /// secret (que resulta em <c>null</c>, ou seja, item sem 2FA).
    /// </summary>
    internal static VaultItem Rehydrate(Guid id, string title, string password, string category,
        string? username, string? url, string? notes, Guid? folderId,
        string? totpSecret, DateTime createdAt, DateTime updatedAt)
    {
        return new VaultItem(id, title, password, category, username, url, notes,
            folderId, totpSecret, createdAt, updatedAt);
    }

    public static VaultItem Create(string title, string password, string category,
        string? username = null, string? url = null, string? notes = null,
        string? totpSecret = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Título não pode ser vazio.", nameof(title));
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Senha não pode ser vazia.", nameof(password));
        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("Categoria não pode ser vazia.", nameof(category));

        return new VaultItem(Guid.NewGuid(), title.Trim(), password, category.Trim(),
            username?.Trim(), url?.Trim(), notes?.Trim(),
            NormalizarTotpSecret(totpSecret, nameof(totpSecret)), DateTime.UtcNow);
    }

    public void UpdateDetails(string title, string password, string category,
        string? username = null, string? url = null, string? notes = null,
        string? totpSecret = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Título não pode ser vazio.", nameof(title));
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Senha não pode ser vazia.", nameof(password));
        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("Categoria não pode ser vazia.", nameof(category));

        Title = title.Trim();
        Password = password;
        Category = category.Trim();
        Username = username?.Trim();
        Url = url?.Trim();
        Notes = notes?.Trim();
        TotpSecret = NormalizarTotpSecret(totpSecret, nameof(totpSecret));
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Define (ou substitui) o secret TOTP do item. Secret nulo/vazio remove
    /// o 2FA do item.
    /// </summary>
    public void DefinirTotpSecret(string? secret)
    {
        TotpSecret = NormalizarTotpSecret(secret, nameof(secret));
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Remove o 2FA do item (equivale a definir secret nulo).
    /// </summary>
    public void RemoverTotpSecret()
    {
        TotpSecret = null;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Normaliza um secret TOTP para o formato canônico (maiúsculas, sem
    /// espaços/hífens/padding) e valida alfabeto e tamanho. Retorna nulo
    /// para entrada nula/vazia (item sem 2FA).
    /// </summary>
    private static string? NormalizarTotpSecret(string? secret, string nomeParametro)
    {
        if (string.IsNullOrWhiteSpace(secret))
            return null;

        var normalizado = secret.Trim().ToUpperInvariant()
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .TrimEnd('=');

        if (normalizado.Length == 0)
            return null;

        foreach (var c in normalizado)
        {
            if (!EhCaractereBase32(c))
                throw new ArgumentException(
                    "Secret TOTP inválido: use apenas caracteres Base32 (A-Z, 2-7).",
                    nomeParametro);
        }

        var tamanhoEmBytes = DecodificarBase32(normalizado).Length;
        if (tamanhoEmBytes < 10 || tamanhoEmBytes > 64)
            throw new ArgumentException(
                "Secret TOTP inválido: o tamanho decodificado deve ficar entre 10 e 64 bytes.",
                nomeParametro);

        return normalizado;
    }

    private static bool EhCaractereBase32(char c) =>
        (c >= 'A' && c <= 'Z') || (c >= '2' && c <= '7');

    /// <summary>
    /// Decodifica Base32 (RFC 4648) sem exigir padding; bits restantes
    /// incompletos ao final são descartados.
    /// </summary>
    private static byte[] DecodificarBase32(string normalizado)
    {
        var bytes = new List<byte>(normalizado.Length * 5 / 8);
        var buffer = 0;
        var bitsNoBuffer = 0;

        foreach (var c in normalizado)
        {
            var valor = c <= '9' ? c - '2' + 26 : c - 'A';
            buffer = (buffer << 5) | valor;
            bitsNoBuffer += 5;

            if (bitsNoBuffer >= 8)
            {
                bitsNoBuffer -= 8;
                bytes.Add((byte)(buffer >> bitsNoBuffer));
                buffer &= (1 << bitsNoBuffer) - 1;
            }
        }

        return bytes.ToArray();
    }

    internal void AssignToFolder(Guid? folderId)
    {
        FolderId = folderId;
    }
}