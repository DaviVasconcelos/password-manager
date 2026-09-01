namespace PasswordManager.Application.VaultRegistry;

/// <summary>
/// Descritor de um arquivo de cofre (metadados não sensíveis).
/// O conteúdo criptografado fica no arquivo SQLite correspondente.
/// </summary>
/// <param name="Id">Identificador único do cofre (GUID do registry).</param>
/// <param name="Nome">Nome de exibição (ex: "vault-1", "Pessoal"). Único case-insensitive.</param>
/// <param name="Arquivo">Nome do arquivo em Vaults/ (ex: "vault-1.db").</param>
/// <param name="CriadoEm">Data de criação (UTC).</param>
/// <param name="AtualizadoEm">Data da última atualização (UTC).</param>
public sealed record VaultDescriptor(
    Guid Id,
    string Nome,
    string Arquivo,
    DateTime CriadoEm,
    DateTime AtualizadoEm);
