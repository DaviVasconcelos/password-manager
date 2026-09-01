namespace PasswordManager.Application.VaultRegistry;

/// <summary>
/// Validação de nomes de cofre (usado pelo registry e pela UI).
/// Nomes padrão são "vault-1", "vault-2", ...
/// </summary>
public static class VaultNameValidator
{
    public const int MinLength = 1;
    public const int MaxLength = 64;

    // Caracteres proibidos em nomes de arquivo Windows.
    private static readonly char[] CaracteresProibidos = ['\\', '/', ':', '*', '?', '"', '<', '>', '|'];

    // Nomes reservados do Windows (case-insensitive, sem extensão).
    private static readonly HashSet<string> NomesReservados = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    /// <summary>
    /// Valida o nome de um cofre. Lança <see cref="ArgumentException"/> quando inválido.
    /// </summary>
    public static void Validar(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new ArgumentException("O nome do cofre não pode ser vazio.", nameof(nome));

        var trimmed = nome.Trim();

        if (trimmed.Length < MinLength || trimmed.Length > MaxLength)
            throw new ArgumentException($"O nome do cofre deve ter entre {MinLength} e {MaxLength} caracteres.", nameof(nome));

        if (trimmed.IndexOfAny(CaracteresProibidos) >= 0)
            throw new ArgumentException($"O nome do cofre não pode conter caracteres inválidos: {string.Join(" ", CaracteresProibidos)}.", nameof(nome));

        // Nome não pode terminar com ponto ou espaço (Windows).
        if (trimmed.EndsWith('.') || trimmed.EndsWith(' '))
            throw new ArgumentException("O nome do cofre não pode terminar com ponto ou espaço.", nameof(nome));

        var semExtensao = trimmed.Split('.')[0];
        if (NomesReservados.Contains(semExtensao))
            throw new ArgumentException($"O nome \"{trimmed}\" é reservado pelo sistema.", nameof(nome));
    }

    /// <summary>
    /// Gera um slug de arquivo a partir do nome (minúsculas, hífens, alfanuméricos).
    /// </summary>
    public static string GerarSlug(string nome)
    {
        var trimmed = nome.Trim().ToLowerInvariant();
        var chars = trimmed.Select(c => char.IsLetterOrDigit(c) ? c : c == '-' || c == '_' ? c : '-').ToArray();
        var slug = new string(chars);
        // Colapsa hífens consecutivos e remove bordas.
        while (slug.Contains("--"))
            slug = slug.Replace("--", "-");
        slug = slug.Trim('-', '_');
        if (string.IsNullOrWhiteSpace(slug))
            slug = "vault";
        // Limita tamanho do slug para evitar path longo.
        if (slug.Length > 32)
            slug = slug[..32].Trim('-', '_');
        return slug;
    }
}
