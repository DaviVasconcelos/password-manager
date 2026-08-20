using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordManager.Application.PasswordGeneration;
using PasswordManager.Application.Settings;
using PasswordManager.Domain.Entities;
using PasswordManager.UI.Localization;

namespace PasswordManager.UI.ViewModels;

/// <summary>
/// ViewModel do editor de item (usado nos diálogos de criar/editar),
/// com gerador de senha e indicador de força.
/// </summary>
public partial class ItemEditorViewModel : ObservableObject
{
    private readonly IPasswordGenerator _passwordGenerator;
    private readonly IPasswordStrengthEvaluator _strengthEvaluator;
    private readonly IAppSettingsService _settingsService;
    private readonly ILocalizationService _localization;

    public ObservableCollection<OpcoesPasta> OpcoesPasta { get; } = new();

    [ObservableProperty]
    private string titulo = string.Empty;

    [ObservableProperty]
    private string usuario = string.Empty;

    [ObservableProperty]
    private string categoria = string.Empty;

    [ObservableProperty]
    private string notas = string.Empty;

    [ObservableProperty]
    private string url = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ForcaSenhaTexto))]
    private string senha = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ForcaSenhaTexto))]
    private ForcaSenha forcaSenha = ForcaSenha.Fraca;

    [ObservableProperty]
    private OpcoesPasta? pastaSelecionada;

    public Guid? ItemId { get; private set; }

    public string ForcaSenhaTexto => ForcaSenha switch
    {
        ForcaSenha.Forte => _localization.GetString("ItemEditor_Forca_Forte"),
        ForcaSenha.Media => _localization.GetString("ItemEditor_Forca_Media"),
        _ => _localization.GetString("ItemEditor_Forca_Fraca")
    };

    public ItemEditorViewModel(
        IPasswordGenerator passwordGenerator,
        IPasswordStrengthEvaluator strengthEvaluator,
        IAppSettingsService settingsService,
        ILocalizationService localization)
    {
        _passwordGenerator = passwordGenerator;
        _strengthEvaluator = strengthEvaluator;
        _settingsService = settingsService;
        _localization = localization;
    }

    partial void OnSenhaChanged(string value) => ForcaSenha = _strengthEvaluator.Avaliar(value);

    public void CarregarParaEdicao(VaultItem item, IEnumerable<OpcoesPasta> opcoesPasta)
    {
        ItemId = item.Id;
        Titulo = item.Title;
        Usuario = item.Username ?? string.Empty;
        Categoria = item.Category;
        Senha = item.Password;
        Url = item.Url ?? string.Empty;
        Notas = item.Notes ?? string.Empty;
        CarregarOpcoes(opcoesPasta, item.FolderId);
    }

    public void CarregarParaCriacao(IEnumerable<OpcoesPasta> opcoesPasta, Guid? pastaSugerida = null)
    {
        ItemId = null;
        Senha = GerarSenhaComDefaults();
        CarregarOpcoes(opcoesPasta, pastaSugerida);
    }

    private void CarregarOpcoes(IEnumerable<OpcoesPasta> opcoes, Guid? pastaId)
    {
        OpcoesPasta.Clear();
        OpcoesPasta.Add(new OpcoesPasta(_localization.GetString("ItemEditor_SemPasta"), null));

        foreach (var opcao in opcoes.Where(o => o.Pasta is not null))
            OpcoesPasta.Add(opcao);

        PastaSelecionada = pastaId is null
            ? OpcoesPasta.First()
            : OpcoesPasta.FirstOrDefault(o => o.Pasta?.Id == pastaId) ?? OpcoesPasta.First();
    }

    [RelayCommand]
    private void GerarSenha() => Senha = GerarSenhaComDefaults();

    /// <summary>
    /// Gera uma senha usando os defaults configurados nas preferências da
    /// aplicação.
    /// </summary>
    private string GerarSenhaComDefaults()
    {
        var settings = _settingsService.Get();
        return _passwordGenerator.Generate(
            settings.PasswordGeneratorLength,
            settings.PasswordGeneratorIncludeLowercase,
            settings.PasswordGeneratorIncludeUppercase,
            settings.PasswordGeneratorIncludeDigits,
            settings.PasswordGeneratorIncludeSymbols);
    }
}