namespace PasswordManager.Application.Settings;

/// <summary>
/// Preferências da aplicação (auto-lock, limpeza da área de transferência e
/// defaults do gerador de senha). Não contém segredos e é persistida fora do
/// cofre criptografado, em um arquivo JSON local simples.
/// </summary>
public sealed record AppSettings
{
    public const int DefaultAutoLockTimeoutMinutes = 2;
    public const int DefaultClipboardCleanTimeSeconds = 30;
    public const int DefaultPasswordGeneratorLength = 20;

    public const int MinAutoLockTimeoutMinutes = 1;
    public const int MinClipboardCleanTimeSeconds = 5;
    public const int MinPasswordGeneratorLength = 8;
    public const int MaxPasswordGeneratorLength = 64;

    /// <summary>
    /// Tempo de inatividade, em minutos, após o qual o cofre é trancado
    /// automaticamente.
    /// </summary>
    public int AutoLockTimeoutMinutes { get; init; } = DefaultAutoLockTimeoutMinutes;

    /// <summary>
    /// Tempo, em segundos, até a senha copiada ser removida da área de
    /// transferência.
    /// </summary>
    public int ClipboardCleanTimeSeconds { get; init; } = DefaultClipboardCleanTimeSeconds;

    /// <summary>
    /// Comprimento padrão das senhas geradas.
    /// </summary>
    public int PasswordGeneratorLength { get; init; } = DefaultPasswordGeneratorLength;

    public bool PasswordGeneratorIncludeLowercase { get; init; } = true;
    public bool PasswordGeneratorIncludeUppercase { get; init; } = true;
    public bool PasswordGeneratorIncludeDigits { get; init; } = true;
    public bool PasswordGeneratorIncludeSymbols { get; init; } = true;

    /// <summary>
    /// Idioma da interface: "auto" (sistema), "pt-BR" ou "en-US".
    /// </summary>
    public string Idioma { get; init; } = IdiomaAuto;

    public const string IdiomaAuto = "auto";
    public const string IdiomaPtBR = "pt-BR";
    public const string IdiomaEnUS = "en-US";

    /// <summary>
    /// Tema da interface: "sistema" (segue o SO), "claro" ou "escuro".
    /// </summary>
    public string Tema { get; init; } = TemaSistema;

    public const string TemaSistema = "sistema";
    public const string TemaClaro = "claro";
    public const string TemaEscuro = "escuro";

    public static AppSettings Default { get; } = new();

    /// <summary>
    /// Valida os limites de cada configuração, lançando
    /// <see cref="ArgumentException"/> (ou <see cref="ArgumentOutOfRangeException"/>)
    /// quando algum valor estiver fora dos limites aceitos.
    /// </summary>
    public void Validar()
    {
        if (AutoLockTimeoutMinutes < MinAutoLockTimeoutMinutes)
            throw new ArgumentException(
                $"O timeout de auto-lock deve ser de pelo menos {MinAutoLockTimeoutMinutes} minuto.",
                nameof(AutoLockTimeoutMinutes));

        if (ClipboardCleanTimeSeconds < MinClipboardCleanTimeSeconds)
            throw new ArgumentException(
                $"O tempo de limpeza da área de transferência deve ser de pelo menos {MinClipboardCleanTimeSeconds} segundos.",
                nameof(ClipboardCleanTimeSeconds));

        if (PasswordGeneratorLength < MinPasswordGeneratorLength || PasswordGeneratorLength > MaxPasswordGeneratorLength)
            throw new ArgumentOutOfRangeException(nameof(PasswordGeneratorLength),
                $"O tamanho da senha gerada deve estar entre {MinPasswordGeneratorLength} e {MaxPasswordGeneratorLength} caracteres.");

        if (!PasswordGeneratorIncludeLowercase && !PasswordGeneratorIncludeUppercase
            && !PasswordGeneratorIncludeDigits && !PasswordGeneratorIncludeSymbols)
            throw new ArgumentException("Pelo menos uma classe de caracteres deve estar habilitada no gerador de senha.");

        if (string.IsNullOrWhiteSpace(Idioma))
            throw new ArgumentException("O idioma não pode ser vazio.", nameof(Idioma));

        if (!string.Equals(Idioma, IdiomaAuto, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                _ = new System.Globalization.CultureInfo(Idioma);
            }
            catch (System.Globalization.CultureNotFoundException)
            {
                throw new ArgumentException($"Idioma inválido: \"{Idioma}\".", nameof(Idioma));
            }
        }

        if (string.IsNullOrWhiteSpace(Tema))
            throw new ArgumentException("O tema não pode ser vazio.", nameof(Tema));

        if (!string.Equals(Tema, TemaSistema, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(Tema, TemaClaro, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(Tema, TemaEscuro, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Tema inválido: \"{Tema}\". Use \"{TemaSistema}\", \"{TemaClaro}\" ou \"{TemaEscuro}\".", nameof(Tema));
    }
}