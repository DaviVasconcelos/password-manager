namespace PasswordManager.Application.Settings;

/// <summary>
/// Acessa as preferências da aplicação persistidas em JSON local simples
/// (sem segredos), fora do cofre criptografado.
/// </summary>
public interface IAppSettingsService
{
    /// <summary>
    /// Retorna as configurações atuais (carregadas na inicialização do serviço).
    /// </summary>
    AppSettings Get();

    /// <summary>
    /// Valida e persiste as configurações, atualizando o cache em memória.
    /// Lança <see cref="ArgumentException"/> quando os valores são inválidos.
    /// </summary>
    Task SaveAsync(AppSettings settings, CancellationToken ct = default);
}