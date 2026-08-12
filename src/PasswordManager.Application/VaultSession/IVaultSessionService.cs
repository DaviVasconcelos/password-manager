using PasswordManager.Domain.Entities;

namespace PasswordManager.Application.VaultSession;

/// <summary>
/// Gerencia o ciclo de vida da sessão do cofre: criar, desbloquear,
/// trancar, trocar a senha mestra e persistir alterações.
/// </summary>
public interface IVaultSessionService
{
    /// <summary>
    /// Indica se a sessão está desbloqueada (chave e cofre em memória).
    /// </summary>
    bool Desbloqueado { get; }

    /// <summary>
    /// O <see cref="Vault"/> carregado na sessão desbloqueada.
    /// Lança exceção se a sessão estiver trancada.
    /// </summary>
    Vault VaultAtual { get; }

    /// <summary>
    /// Cria um novo cofre, gera o salt de derivação, persiste o registro
    /// e deixa a sessão desbloqueada. Lança exceção se já existir cofre
    /// persistido ou se a sessão já estiver desbloqueada.
    /// </summary>
    Task<Vault> CriarAsync(string senhaMestra, CancellationToken ct = default);

    /// <summary>
    /// Desbloqueia o cofre existente: deriva a chave a partir do salt
    /// persistido e da senha mestra, carrega o <see cref="Vault"/> e retém
    /// apenas a chave em memória (a senha é descartada). Lança
    /// <see cref="PasswordManager.Application.Exceptions.CryptographicIntegrityException"/>
    /// se a senha estiver errada.
    /// </summary>
    Task<Vault> DesbloquearAsync(string senhaMestra, CancellationToken ct = default);

    /// <summary>
    /// Tranca a sessão: zera a chave em memória e descarta a referência
    /// ao <see cref="Vault"/>. A senha mestra nunca fica retida.
    /// </summary>
    void Trancar();

    /// <summary>
    /// Troca a senha mestra, rotacionando o salt e re-criptografando o cofre.
    /// Exige sessão desbloqueada.
    /// </summary>
    Task TrocarSenhaMestraAsync(string novaSenhaMestra, CancellationToken ct = default);

    /// <summary>
    /// Persiste o estado atual do <see cref="Vault"/> da sessão usando a
    /// chave retida em memória. Exige sessão desbloqueada.
    /// </summary>
    Task SalvarAsync(CancellationToken ct = default);
}