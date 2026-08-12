namespace PasswordManager.Infrastructure.Persistence;

/// <summary>
/// Registro único no SQLite que guarda o cofre inteiro como um blob
/// criptografado (ADR 0003), junto com o salt de derivação de chave
/// e a versão do schema de serialização.
/// </summary>
public class VaultRecord
{
    public Guid Id { get; set; }
    public int SchemaVersion { get; set; }
    public byte[] Salt { get; set; } = Array.Empty<byte>();
    public byte[] EncryptedBlob { get; set; } = Array.Empty<byte>();
    public DateTime UpdatedAt { get; set; }
}
