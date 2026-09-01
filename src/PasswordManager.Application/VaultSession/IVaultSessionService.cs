using PasswordManager.Application.VaultRegistry;
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
    bool Unlocked { get; }

    /// <summary>
    /// O <see cref="Vault"/> carregado na sessão desbloqueada.
    /// Lança exceção se a sessão estiver trancada.
    /// </summary>
    Vault CurrentVault { get; }

    /// <summary>
    /// Indica se já existe um cofre persistido (usado pela UI para decidir
    /// entre "criar" e "desbloquear" no primeiro acesso).
    /// </summary>
    Task<bool> VaultExistsAsync(CancellationToken ct = default);

    /// <summary>
    /// Cria um novo cofre, gera o salt de derivação, persiste o registro
    /// e deixa a sessão desbloqueada. Lança exceção se já existir cofre
    /// persistido ou se a sessão já estiver desbloqueada.
    /// </summary>
    Task<Vault> CreateAsync(string masterPassword, CancellationToken ct = default);

    /// <summary>
    /// Cria um novo arquivo de cofre com nome específico (ADR 0008).
    /// Se <paramref name="nome"/> for nulo/vazio, gera "vault-1", "vault-2", ...
    /// </summary>
    Task<Vault> CreateAsync(string? nome, string masterPassword, CancellationToken ct = default);

    /// <summary>
    /// Desbloqueia o cofre existente: deriva a chave a partir do salt
    /// persistido e da senha mestra, carrega o <see cref="Vault"/> e retém
    /// apenas a chave em memória (a senha é descartada). Lança
    /// <see cref="PasswordManager.Application.Exceptions.CryptographicIntegrityException"/>
    /// se a senha estiver errada.
    /// </summary>
    Task<Vault> UnlockAsync(string masterPassword, CancellationToken ct = default);

    /// <summary>
    /// Tranca a sessão: zera a chave em memória e descarta a referência
    /// ao <see cref="Vault"/>. A senha mestra nunca fica retida.
    /// </summary>
    void Lock();

    /// <summary>
    /// Troca a senha mestra, rotacionando o salt e re-criptografando o cofre.
    /// Verifica a senha atual por derivação de chave (comparação em tempo
    /// constante com a chave retida); lança
    /// <see cref="PasswordManager.Application.Exceptions.CryptographicIntegrityException"/>
    /// se a senha atual estiver incorreta. Exige sessão desbloqueada.
    /// </summary>
    Task ChangeMasterPasswordAsync(string senhaAtual, string novaSenhaMestra, CancellationToken ct = default);

    /// <summary>
    /// Persiste o estado atual do <see cref="Vault"/> da sessão usando a
    /// chave retida em memória. Exige sessão desbloqueada.
    /// </summary>
    Task SaveAsync(CancellationToken ct = default);

    /// <summary>
    /// Adiciona um item ao cofre e persiste imediatamente.
    /// Exige sessão desbloqueada.
    /// </summary>
    Task<VaultItem> AddItemAsync(string title, string password, string category,
        string? username = null, string? url = null, string? notes = null, CancellationToken ct = default);

    /// <summary>
    /// Atualiza um item existente e persiste imediatamente.
    /// Exige sessão desbloqueada.
    /// </summary>
    Task ReloadItemAsync(Guid itemId, string title, string password, string category,
        string? username = null, string? url = null, string? notes = null, CancellationToken ct = default);

    /// <summary>
    /// Remove um item do cofre e persiste imediatamente.
    /// Exige sessão desbloqueada.
    /// </summary>
    Task RemoveItemAsync(Guid itemId, CancellationToken ct = default);

    /// <summary>
    /// Adiciona uma pasta ao cofre e persiste imediatamente.
    /// Exige sessão desbloqueada.
    /// </summary>
    Task<VaultFolder> AddFolderAsync(string name, CancellationToken ct = default);

    /// <summary>
    /// Renomeia uma pasta e persiste imediatamente.
    /// Exige sessão desbloqueada.
    /// </summary>
    Task RenameFolderAsync(Guid folderId, string name, CancellationToken ct = default);

    /// <summary>
    /// Remove uma pasta (sem apagar os itens dela — eles ficam sem pasta)
    /// e persiste imediatamente. Exige sessão desbloqueada.
    /// </summary>
    Task RemoveFolderAsync(Guid folderId, CancellationToken ct = default);

    /// <summary>
    /// Associa um item a uma pasta (ou o desassocia, com folderId nulo)
    /// e persiste imediatamente. Exige sessão desbloqueada.
    /// </summary>
    Task AssignItemToFolderAsync(Guid itemId, Guid? folderId, CancellationToken ct = default);

    /// <summary>
    /// Filtra os itens do cofre em memória por termo (título, usuário, URL,
    /// notas ou categoria) e/ou por pasta. Operação somente leitura.
    /// Exige sessão desbloqueada.
    /// </summary>
    IReadOnlyList<VaultItem> SearchItems(string? termo = null, Guid? pastaId = null);

    /// <summary>
    /// Serializa e criptografa o cofre atual para o formato .vault usando a
    /// senha mestra informada (re-digitada pelo usuário). Exige sessão
    /// desbloqueada. O resultado são bytes prontos para escrita em arquivo
    /// pela UI.
    /// </summary>
    Task<byte[]> ExportAsync(string masterPassword, CancellationToken ct = default);

    /// <summary>
    /// Importa um arquivo .vault, validando-o com a senha mestra informada.
    /// Com a sessão desbloqueada, <paramref name="replace"/> decide entre
    /// substituir o cofre atual ou mesclar com ele (ADR 0005). Com a sessão
    /// trancada, só é permitido quando ainda não há cofre persistido —
    /// nesse caso o cofre importado vira o cofre local (primeira execução).
    /// Lança
    /// <see cref="PasswordManager.Application.Exceptions.CryptographicIntegrityException"/>
    /// se a senha estiver errada ou o arquivo estiver corrompido.
    /// </summary>
    Task ImportAsync(byte[] fileData, string masterPassword, bool replace, CancellationToken ct = default);

    // --- Multi-arquivo (ADR 0008, Opção B) ---

    /// <summary>
    /// Cofre ativo (selecionado na UnlockPage). Nulo quando não há cofres.
    /// </summary>
    VaultDescriptor? CofreAtivo { get; }

    /// <summary>
    /// Lista todos os arquivos de cofre cadastrados (vaults.json).
    /// Não exige sessão desbloqueada.
    /// </summary>
    Task<IReadOnlyList<VaultDescriptor>> ListarCofresAsync(CancellationToken ct = default);

    /// <summary>
    /// Renomeia um arquivo de cofre (metadado + arquivo físico).
    /// Não exige sessão desbloqueada. Valida nome e unicidade.
    /// </summary>
    Task RenomearCofreAsync(Guid id, string novoNome, CancellationToken ct = default);

    /// <summary>
    /// Exclui um arquivo de cofre. Não exige desbloqueio. Se o cofre
    /// excluído era o ativo e a sessão estava desbloqueada, tranca a sessão.
    /// </summary>
    Task ExcluirCofreAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Define o cofre ativo (seleção na UnlockPage). Tranca a sessão atual
    /// (zerando a chave) antes de trocar.
    /// </summary>
    Task SelecionarCofreAsync(Guid id, CancellationToken ct = default);
}