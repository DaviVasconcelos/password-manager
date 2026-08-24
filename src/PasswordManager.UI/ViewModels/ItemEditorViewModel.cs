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
    [NotifyPropertyChangedFor(nameof(ForcaValor))]
    private ForcaSenha forcaSenha = ForcaSenha.Fraca;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TamanhoSenhaTexto))]
    private int tamanhoSenha;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeGerar))]
    [NotifyCanExecuteChangedFor(nameof(GerarSenhaCommand))]
    private bool incluirMinusculas = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeGerar))]
    [NotifyCanExecuteChangedFor(nameof(GerarSenhaCommand))]
    private bool incluirMaiusculas = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeGerar))]
    [NotifyCanExecuteChangedFor(nameof(GerarSenhaCommand))]
    private bool incluirNumeros = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeGerar))]
    [NotifyCanExecuteChangedFor(nameof(GerarSenhaCommand))]
    private bool incluirSimbolos;

    [ObservableProperty]
    private OpcoesPasta? pastaSelecionada;

    public Guid? ItemId { get; private set; }

    public bool PodeGerar => IncluirMinusculas || IncluirMaiusculas || IncluirNumeros || IncluirSimbolos;

    /// <summary>
    /// Valor numérico da força (0=fraca, 1=média, 2=forte) para a barra de progresso.
    /// </summary>
    public int ForcaValor => (int)ForcaSenha;

    public string ForcaSenhaTexto => string.Format(_localization.GetString("ItemEditor_Forca_Formato"), ForcaSenha switch
    {
        ForcaSenha.Forte => _localization.GetString("ItemEditor_Forca_Forte"),
        ForcaSenha.Media => _localization.GetString("ItemEditor_Forca_Media"),
        _ => _localization.GetString("ItemEditor_Forca_Fraca")
    });

    public string TamanhoSenhaTexto => $"{TamanhoSenha} {_localization.GetString("ItemEditor_Tamanho_Sufixo")}";

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
        CarregarDefaults();
        CarregarOpcoes(opcoesPasta, item.FolderId);
    }

    public void CarregarParaCriacao(IEnumerable<OpcoesPasta> opcoesPasta, Guid? pastaSugerida = null)
    {
        ItemId = null;
        CarregarDefaults();
        Senha = GerarSenhaComDefaults();
        CarregarOpcoes(opcoesPasta, pastaSugerida);
    }

    /// <summary>
    /// Inicializa o gerador embutido do diálogo com os defaults
    /// configurados nas preferências da aplicação.
    /// </summary>
    private void CarregarDefaults()
    {
        var settings = _settingsService.Get();
        TamanhoSenha = settings.PasswordGeneratorLength;
        IncluirMinusculas = settings.PasswordGeneratorIncludeLowercase;
        IncluirMaiusculas = settings.PasswordGeneratorIncludeUppercase;
        IncluirNumeros = settings.PasswordGeneratorIncludeDigits;
        IncluirSimbolos = settings.PasswordGeneratorIncludeSymbols;
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

    [RelayCommand(CanExecute = nameof(PodeGerar))]
    private void GerarSenha() => Senha = GerarSenhaComDefaults();

    /// <summary>
    /// Gera uma senha usando as opções do gerador embutido do diálogo
    /// (inicializadas com os defaults das preferências).
    /// </summary>
    private string GerarSenhaComDefaults()
    {
        return _passwordGenerator.Generate(
            TamanhoSenha,
            IncluirMinusculas,
            IncluirMaiusculas,
            IncluirNumeros,
            IncluirSimbolos);
    }
}