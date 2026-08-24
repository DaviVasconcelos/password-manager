using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using PasswordManager.Application.Settings;
using PasswordManager.UI.Localization;

namespace PasswordManager.UI.ViewModels;

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
    private string? erro;

    public string TamanhoSenhaTexto => $"{PasswordGeneratorLength} {_localization.GetString("ItemEditor_Tamanho_Sufixo")}";

    public SettingsViewModel(IAppSettingsService settingsService, ILocalizationService localization)
    {
        _settingsService = settingsService;
        _localization = localization;
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
            var settings = new AppSettings
            {
                AutoLockTimeoutMinutes = TimeoutAutoLockMinutes,
                ClipboardCleanTimeSeconds = ClipboardCleanTimeSeconds,
                PasswordGeneratorLength = PasswordGeneratorLength,
                PasswordGeneratorIncludeLowercase = IncludeLowercase,
                PasswordGeneratorIncludeUppercase = IncludeUppercase,
                PasswordGeneratorIncludeDigits = IncludeDigits,
                PasswordGeneratorIncludeSymbols = IncludeSymbols
            };

            await _settingsService.SaveAsync(settings);
            return true;
        }
        catch (ArgumentException ex)
        {
            Erro = ex.Message;
            return false;
        }
    }
}