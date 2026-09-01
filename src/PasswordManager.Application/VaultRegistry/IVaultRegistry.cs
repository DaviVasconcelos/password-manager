namespace PasswordManager.Application.VaultRegistry;

/// <summary>
/// Catálogo de arquivos de cofre (metadados não sensíveis).
/// Cada cofre corresponde a um arquivo SQLite em Vaults/*.db (ADR 0008, Opção B).
/// O registry persiste em vaults.json (id, nome, arquivo, criadoEm, atualizadoEm, ativoId).
/// Operações de renomear/excluir não exigem desbloqueio.
/// </summary>
public interface IVaultRegistry
{
    /// <summary>
    /// Identificador do cofre ativo (selecionado na UnlockPage).
    /// Nulo quando não há cofres ou nenhum selecionado.
    /// </summary>
    Guid? AtivoId { get; }

    /// <summary>
    /// Descritor do cofre ativo, ou nulo se não houver.
    /// </summary>
    VaultDescriptor? Ativo { get; }

    /// <summary>
    /// Inicializa o registry: cria pastas/arquivo se necessário e
    /// migra vault.db legado para Vaults/vault-1.db quando aplicável.
    /// Deve ser chamado no startup antes de qualquer outra operação.
    /// </summary>
    Task InicializarAsync(CancellationToken ct = default);

    /// <summary>
    /// Lista todos os cofres cadastrados, ordenados por nome.
    /// Retorna lista vazia quando não há cofres.
    /// </summary>
    Task<IReadOnlyList<VaultDescriptor>> ListarAsync(CancellationToken ct = default);

    /// <summary>
    /// Cria um novo arquivo de cofre. Se <paramref name="nome"/> for nulo/vazio,
    /// gera automaticamente "vault-1", "vault-2", ...
    /// O arquivo ainda não contém blob criptografado — a criação do cofre
    /// criptografado é responsabilidade do <see cref="VaultSession.IVaultSessionService"/>.
    /// </summary>
    Task<VaultDescriptor> CriarAsync(string? nome, CancellationToken ct = default);

    /// <summary>
    /// Renomeia um cofre (metadado + arquivo físico). Não exige desbloqueio.
    /// Valida o novo nome e garante unicidade case-insensitive.
    /// </summary>
    Task RenomearAsync(Guid id, string novoNome, CancellationToken ct = default);

    /// <summary>
    /// Exclui um cofre (remove arquivo .db e entrada do registry).
    /// Não exige desbloqueio. Se o cofre excluído era o ativo, o registry
    /// define o próximo ativo (primeiro da lista) ou nulo.
    /// </summary>
    Task ExcluirAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Define o cofre ativo (seleção na UnlockPage).
    /// </summary>
    Task DefinirAtivoAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Obtém o caminho completo do arquivo .db para o cofre informado.
    /// </summary>
    string ObterCaminho(Guid id);

    /// <summary>
    /// Obtém o caminho do cofre ativo. Lança se não houver ativo.
    /// </summary>
    string ObterCaminhoAtivo();
}
