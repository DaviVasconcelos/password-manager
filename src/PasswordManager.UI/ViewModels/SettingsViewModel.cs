using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using PasswordManager.Application.Settings;
using PasswordManager.UI.Localization;

namespace PasswordManager.UI.ViewModels;

public sealed record OpcaoIdioma(string Codigo, string NomeExibicao);

public sealed record OpcaoTema(string Codigo, string NomeExibicao);

/// <summary>
/// ViewModel da tela de configurações: timeout de auto-lock, tempo de limpeza
/// da área de transferência e defaults do gerador de senha. As opções são
/// limitadas a presets para evitar entrada inválida na UI.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly IAppSettingsService _settingsService;
    private readonly ILocalizationService _localization;

    public IReadOnlyList<int> OpcoesTimeoutAutoLock { get; } = new[] { 1, 2, 5, 10, 15, 30 };
    public IReadOnlyList<int> OpcoesLimpezaClipboard { get; } = new[] { 10, 15, 30, 60, 120 };

    public IReadOnlyList<OpcaoIdioma> OpcoesIdioma { get; }

    public IReadOnlyList<OpcaoTema> OpcoesTema { get; }

    [ObservableProperty]
    private int timeoutAutoLockMinutes;

    [ObservableProperty]
    private int clipboardCleanTimeSeconds;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TamanhoSenhaTexto))]
    private int passwordGeneratorLength;

    [ObservableProperty]
    private bool includeLowercase = true;

    [ObservableProperty]
    private bool includeUppercase = true;

    [ObservableProperty]
    private bool includeDigits = true;

    [ObservableProperty]
    private bool includeSymbols = true;

    [ObservableProperty]
    private OpcaoIdioma? idiomaSelecionado;

    [ObservableProperty]
    private OpcaoTema? temaSelecionado;

    [ObservableProperty]
    private string? erro;

    public string TamanhoSenhaTexto => $"{PasswordGeneratorLength} {_localization.GetString("ItemEditor_Tamanho_Sufixo")}";

    /// <summary>
    /// Indica se o idioma foi alterado e é necessário reiniciar o app.
    /// </summary>
    public bool RequerReinicio { get; private set; }

    private string _idiomaOriginal = AppSettings.IdiomaAuto;

    public SettingsViewModel(IAppSettingsService settingsService, ILocalizationService localization)
    {
        _settingsService = settingsService;
        _localization = localization;
        OpcoesIdioma = ConstruirOpcoesIdioma();
        OpcoesTema = ConstruirOpcoesTema();
    }

    private IReadOnlyList<OpcaoTema> ConstruirOpcoesTema()
    {
        return new List<OpcaoTema>
        {
            new(AppSettings.TemaSistema, _localization.GetString("Settings_Tema_Opcao_Sistema")),
            new(AppSettings.TemaClaro, _localization.GetString("Settings_Tema_Opcao_Claro")),
            new(AppSettings.TemaEscuro, _localization.GetString("Settings_Tema_Opcao_Escuro")),
        };
    }

    private IReadOnlyList<OpcaoIdioma> ConstruirOpcoesIdioma()
    {
        var lista = new List<OpcaoIdioma>
        {
            new(AppSettings.IdiomaAuto, _localization.GetString("Settings_Idioma_Opcao_Auto"))
        };

        IReadOnlyList<string> manifest;
        try
        {
            manifest = Windows.Globalization.ApplicationLanguages.ManifestLanguages;
            if (manifest == null || manifest.Count == 0)
                manifest = Microsoft.Windows.Globalization.ApplicationLanguages.ManifestLanguages;
        }
        catch
        {
            manifest = Array.Empty<string>();
        }

        var codigos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (manifest != null)
        {
            foreach (var c in manifest)
                if (!string.IsNullOrWhiteSpace(c))
                    codigos.Add(c);
        }

        // Garantir que pt-BR/en-US apareçam mesmo se o PRI ainda não os listar (testes).
        codigos.Add(AppSettings.IdiomaPtBR);
        codigos.Add(AppSettings.IdiomaEnUS);

        foreach (var codigo in codigos.OrderBy(c => c, StringComparer.OrdinalIgnoreCase))
        {
            // Tenta chave dedicada (ex: Settings_Idioma_Opcao_EsES), senão usa NativeName do CultureInfo.
            var chave = $"Settings_Idioma_Opcao_{codigo.Replace("-", string.Empty)}";
            var nome = _localization.GetString(chave);
            if (string.IsNullOrEmpty(nome) || nome == chave)
            {
                try
                {
                    var ci = new System.Globalization.CultureInfo(codigo);
                    // NativeName já vem capitalizado (ex: "português (Brasil)"), deixar como está mas com inicial maiúscula.
                    nome = ci.NativeName;
                    if (!string.IsNullOrEmpty(nome))
                        nome = char.ToUpperInvariant(nome[0]) + nome.Substring(1);
                    else
                        nome = codigo;
                }
                catch
                {
                    nome = codigo;
                }
            }

            lista.Add(new OpcaoIdioma(codigo, nome));
        }

        return lista;
    }

    private static string ObterIdiomaEfetivo(string codigo)
    {
        if (string.Equals(codigo, AppSettings.IdiomaAuto, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var sistema = Windows.Globalization.ApplicationLanguages.Languages.FirstOrDefault();
                if (!string.IsNullOrEmpty(sistema))
                    return sistema;
            }
            catch
            {
            }

            try
            {
                var sistema2 = Microsoft.Windows.Globalization.ApplicationLanguages.Languages.FirstOrDefault();
                if (!string.IsNullOrEmpty(sistema2))
                    return sistema2;
            }
            catch
            {
            }

            return AppSettings.IdiomaEnUS;
        }

        return codigo;
    }

    /// <summary>
    /// Carrega as configurações persistidas para os campos do formulário.
    /// </summary>
    public void Carregar()
    {
        var settings = _settingsService.Get();
        TimeoutAutoLockMinutes = settings.AutoLockTimeoutMinutes;
        ClipboardCleanTimeSeconds = settings.ClipboardCleanTimeSeconds;
        PasswordGeneratorLength = settings.PasswordGeneratorLength;
        IncludeLowercase = settings.PasswordGeneratorIncludeLowercase;
        IncludeUppercase = settings.PasswordGeneratorIncludeUppercase;
        IncludeDigits = settings.PasswordGeneratorIncludeDigits;
        IncludeSymbols = settings.PasswordGeneratorIncludeSymbols;
        _idiomaOriginal = settings.Idioma;
        IdiomaSelecionado = OpcoesIdioma.FirstOrDefault(o => o.Codigo == settings.Idioma) ?? OpcoesIdioma[0];
        TemaSelecionado = OpcoesTema.FirstOrDefault(o => string.Equals(o.Codigo, settings.Tema, StringComparison.OrdinalIgnoreCase)) ?? OpcoesTema[0];
        RequerReinicio = false;
        Erro = null;
    }

    /// <summary>
    /// Valida e persiste as configurações. Retorna <c>false</c> e preenche
    /// <see cref="Erro"/> quando algum valor é inválido.
    /// </summary>
    public async Task<bool> SalvarAsync()
    {
        Erro = null;

        try
        {
            var codigoIdioma = IdiomaSelecionado?.Codigo ?? AppSettings.IdiomaAuto;
            var codigoTema = TemaSelecionado?.Codigo ?? AppSettings.TemaSistema;
            var settings = new AppSettings
            {
                AutoLockTimeoutMinutes = TimeoutAutoLockMinutes,
                ClipboardCleanTimeSeconds = ClipboardCleanTimeSeconds,
                PasswordGeneratorLength = PasswordGeneratorLength,
                PasswordGeneratorIncludeLowercase = IncludeLowercase,
                PasswordGeneratorIncludeUppercase = IncludeUppercase,
                PasswordGeneratorIncludeDigits = IncludeDigits,
                PasswordGeneratorIncludeSymbols = IncludeSymbols,
                Idioma = codigoIdioma,
                Tema = codigoTema
            };

            await _settingsService.SaveAsync(settings);
            RequerReinicio = !string.Equals(
                ObterIdiomaEfetivo(codigoIdioma),
                ObterIdiomaEfetivo(_idiomaOriginal),
                StringComparison.OrdinalIgnoreCase);
            return true;
        }
        catch (ArgumentException ex)
        {
            Erro = ex.Message;
            return false;
        }
    }
}