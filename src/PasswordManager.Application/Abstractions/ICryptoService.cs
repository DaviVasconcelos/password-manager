namespace PasswordManager.Application.Abstractions;

public interface ICryptoService
{
    /// <summary>
    /// Deriva uma chave simétrica a partir da senha mestra e um salt,
    /// usando Argon2id. O salt deve ser persistido junto ao cofre.
    /// </summary>
    byte[] DeriveKey(string masterPassword, byte[] salt);

    /// <summary>
    /// Gera um salt aleatório criptograficamente seguro para uso
    /// na derivação de chave (novo cofre ou rotação de senha mestra).
    /// </summary>
    byte[] GenerateSalt();

    /// <summary>
    /// Criptografa dados em claro usando AES-256-GCM.
    /// Retorna o pacote completo (nonce + ciphertext + tag) já serializado,
    /// pronto para persistência.
    /// </summary>
    byte[] Encrypt(byte[] plainData, byte[] key);

    /// <summary>
    /// Descriptografa um pacote gerado por Encrypt. Lança exceção
    /// específica se a tag de autenticação falhar (dado corrompido/adulterado
    /// ou chave errada).
    /// </summary>
    byte[] Decrypt(byte[] encryptedPackage, byte[] key);
}