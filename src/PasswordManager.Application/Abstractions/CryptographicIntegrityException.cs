// Application/Exceptions/CryptographicIntegrityException.cs
namespace PasswordManager.Application.Exceptions;

/// <summary>
/// Lançada quando a descriptografia falha por senha mestra incorreta
/// ou dado corrompido/adulterado (falha na verificação da tag de
/// autenticação do AES-GCM). Não distingue as duas causas de propósito:
/// isso evita dar a um atacante a informação de "senha errada" vs
/// "arquivo corrompido" como oráculo.
/// </summary>
public sealed class CryptographicIntegrityException : Exception
{
    public CryptographicIntegrityException()
        : base("Falha ao verificar a integridade dos dados criptografados.") { }

    public CryptographicIntegrityException(string message) : base(message) { }

    public CryptographicIntegrityException(string message, Exception innerException)
        : base(message, innerException) { }
}