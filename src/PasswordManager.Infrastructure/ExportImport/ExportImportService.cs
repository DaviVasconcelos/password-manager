using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PasswordManager.Application.Abstractions;
using PasswordManager.Domain.Entities;
using PasswordManager.Infrastructure.Persistence.Serialization;

namespace PasswordManager.Infrastructure.ExportImport;

/// <summary>
/// Implementa o <see cref="IExportImportService"/> conforme o ADR 0005.
/// O arquivo .vault é autocontido e tem o layout:
/// <c>[magic "PMVT" (4)] [versão (1)] [salt Argon2id (16)] [pacote AES-GCM]</c>,
/// onde o pacote é o mesmo produzido pelo <see cref="ICryptoService"/>
/// (nonce + tag + ciphertext). O salt é novo a cada exportação, tornando o
/// arquivo independente do salt persistido localmente.
/// </summary>
public sealed class ExportImportService : IExportImportService
{
    private const int CurrentFileVersion = 1;
    private const int SaltSizeInBytes = 16;
    private const int HeaderSizeInBytes = MagicBytesLength + 1 + SaltSizeInBytes;

    private const int MagicBytesLength = 4;
    private static readonly byte[] MagicBytes = Encoding.ASCII.GetBytes("PMVT");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ICryptoService _cryptoService;

    public ExportImportService(ICryptoService cryptoService)
    {
        _cryptoService = cryptoService;
    }

    public byte[] Export(Vault vault, string masterPassword)
    {
        ArgumentNullException.ThrowIfNull(vault);
        ValidarSenhaMestra(masterPassword);

        var json = JsonSerializer.Serialize(VaultDataMapper.FromVault(vault), JsonOptions);
        var salt = _cryptoService.GenerateSalt();
        var chave = _cryptoService.DeriveKey(masterPassword, salt);

        try
        {
            var pacote = _cryptoService.Encrypt(Encoding.UTF8.GetBytes(json), chave);
            var arquivo = new byte[HeaderSizeInBytes + pacote.Length];

            MagicBytes.CopyTo(arquivo, 0);
            arquivo[MagicBytesLength] = CurrentFileVersion;
            salt.CopyTo(arquivo, MagicBytesLength + 1);
            pacote.CopyTo(arquivo, HeaderSizeInBytes);

            return arquivo;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(chave);
        }
    }

    public Vault Import(byte[] fileData, string masterPassword)
    {
        ArgumentNullException.ThrowIfNull(fileData);
        ValidarSenhaMestra(masterPassword);

        ValidarCabecalho(fileData);

        var versao = fileData[MagicBytesLength];
        if (versao > CurrentFileVersion)
        {
            throw new InvalidOperationException(
                $"O arquivo usa uma versão ({versao}) mais recente do que a suportada ({CurrentFileVersion}).");
        }

        var salt = fileData.AsSpan(MagicBytesLength + 1, SaltSizeInBytes).ToArray();
        var pacote = fileData.AsSpan(HeaderSizeInBytes).ToArray();
        var chave = _cryptoService.DeriveKey(masterPassword, salt);

        try
        {
            var json = _cryptoService.Decrypt(pacote, chave);
            var data = JsonSerializer.Deserialize<VaultData>(json, JsonOptions)
                ?? throw new InvalidOperationException("Falha ao desserializar o cofre importado.");

            return VaultDataMapper.ToVault(data);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(chave);
        }
    }

    private static void ValidarSenhaMestra(string masterPassword)
    {
        if (string.IsNullOrWhiteSpace(masterPassword))
            throw new ArgumentException("A senha mestra não pode ser vazia.", nameof(masterPassword));
    }

    private static void ValidarCabecalho(byte[] fileData)
    {
        if (fileData.Length < HeaderSizeInBytes)
        {
            throw new InvalidOperationException(
                "Arquivo inválido: tamanho insuficiente para conter o cabeçalho do .vault.");
        }

        for (int i = 0; i < MagicBytesLength; i++)
        {
            if (fileData[i] != MagicBytes[i])
            {
                throw new InvalidOperationException(
                    "Arquivo inválido: não é um arquivo de cofre (.vault) deste aplicativo.");
            }
        }
    }
}