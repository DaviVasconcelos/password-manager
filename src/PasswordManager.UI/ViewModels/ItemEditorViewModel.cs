using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordManager.Application.PasswordGeneration;
using PasswordManager.Domain.Entities;

namespace PasswordManager.UI.ViewModels;

/// <summary>
/// ViewModel do editor de item (usado nos diálogos de criar/editar),
/// com gerador de senha e indicador de força.
/// </summary>
public partial class ItemEditorViewModel : ObservableObject
{
    private readonly IPasswordGenerator _passwordGenerator;
    private readonly IPasswordStrengthEvaluator _strengthEvaluator;

    public ObservableCollection<FolderOption> FolderOptions { get; } = new();

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
    private FolderOption? pastaSelecionada;

    public Guid? ItemId { get; private set; }

    public string ForcaSenhaTexto => ForcaSenha switch
    {
        ForcaSenha.Forte => "Forte",
        ForcaSenha.Media => "Média",
        _ => "Fraca"
    };

    public ItemEditorViewModel(IPasswordGenerator passwordGenerator, IPasswordStrengthEvaluator strengthEvaluator)
    {
        _passwordGenerator = passwordGenerator;
        _strengthEvaluator = strengthEvaluator;
    }

    partial void OnSenhaChanged(string value) => ForcaSenha = _strengthEvaluator.Avaliar(value);

    public void CarregarParaEdicao(VaultItem item, IEnumerable<FolderOption> opcoesPasta)
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

    public void CarregarParaCriacao(IEnumerable<FolderOption> opcoesPasta, Guid? pastaSugerida = null)
    {
        ItemId = null;
        Senha = _passwordGenerator.Generate();
        CarregarOpcoes(opcoesPasta, pastaSugerida);
    }

    private void CarregarOpcoes(IEnumerable<FolderOption> opcoes, Guid? pastaId)
    {
        FolderOptions.Clear();
        FolderOptions.Add(new FolderOption("Sem pasta", null));

        foreach (var opcao in opcoes.Where(o => o.Pasta is not null))
            FolderOptions.Add(opcao);

        PastaSelecionada = pastaId is null
            ? FolderOptions.First()
            : FolderOptions.FirstOrDefault(o => o.Pasta?.Id == pastaId) ?? FolderOptions.First();
    }

    [RelayCommand]
    private void GeneratePassword()
    {
        Senha = _passwordGenerator.Generate();
    }
}