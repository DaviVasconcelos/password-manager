using System.IO;
using System.Text.Json;
using PasswordManager.Application.Settings;

namespace PasswordManager.Infrastructure.Settings;

/// <summary>
/// Persiste as preferências da aplicação em um arquivo JSON simples (sem
/// segredos), fora do cofre criptografado. Cria o arquivo com os padrões
/// quando não existe e tolera arquivos corrompidos voltando aos padrões.
/// </summary>
public sealed class AppSettingsService : IAppSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _filePath;
    private AppSettings _cache;

    public AppSettingsService(string filePath)
    {
        _filePath = filePath;
        _cache = CarregarDoDisco();
    }

    public AppSettings Get() => _cache;

    public async Task SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validar();

        var diretorio = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(diretorio))
            Directory.CreateDirectory(diretorio);

        await File.WriteAllTextAsync(_filePath, JsonSerializer.Serialize(settings, JsonOptions), ct)
            .ConfigureAwait(false);

        _cache = settings;
    }

    private AppSettings CarregarDoDisco()
    {
        if (!File.Exists(_filePath))
            return AppSettings.Default;

        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_filePath), JsonOptions);
            if (settings is null)
                return AppSettings.Default;

            settings.Validar();
            return settings;
        }
        catch (JsonException)
        {
            return AppSettings.Default;
        }
        catch (ArgumentException)
        {
            return AppSettings.Default;
        }
        catch (IOException)
        {
            return AppSettings.Default;
        }
        catch (UnauthorizedAccessException)
        {
            return AppSettings.Default;
        }
    }
}