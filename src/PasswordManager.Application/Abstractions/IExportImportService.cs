using PasswordManager.Domain.Entities;

namespace PasswordManager.Application.Abstractions;

/// <summary>
/// Serializa/deserializa o cofre para o formato de arquivo de exportação
/// (.vault), reutilizando o mesmo par Encrypt/Decrypt do
/// <see cref="ICryptoService"/> previsto no ADR 0003. O arquivo é
/// autocontido: contém o salt de derivação e o blob criptografado com
/// AES-256-GCM sob uma chave derivada da senha mestra digitada pelo
/// usuário. A Application recebe apenas bytes — o I/O de arquivo é
/// responsabilidade da UI.
/// </summary>
public interface IExportImportService
{
    /// <summary>
    /// Serializa e criptografa o <paramref name="vault"/> para o formato
    /// .vault, usando a senha mestra informada (um salt novo é gerado a
    /// cada exportação, tornando o arquivo independente do salt local).
    /// </summary>
    byte[] Export(Vault vault, string masterPassword);

    /// <summary>
    /// Valida o cabeçalho, descriptografa e deserializa um arquivo .vault.
    /// Lança
    /// <see cref="PasswordManager.Application.Exceptions.CryptographicIntegrityException"/>
    /// se a senha estiver errada ou o arquivo estiver adulterado/corrompido.
    /// </summary>
    Vault Import(byte[] fileData, string masterPassword);
}