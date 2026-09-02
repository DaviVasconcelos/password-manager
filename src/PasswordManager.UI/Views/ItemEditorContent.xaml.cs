using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PasswordManager.Application.PasswordGeneration;
using PasswordManager.UI.Localization;
using PasswordManager.UI.ViewModels;
using System;
using System.ComponentModel;

namespace PasswordManager.UI.Views;

/// <summary>
/// Conteúdo do diálogo de criação/edição de item. Expõe o
/// <see cref="ItemEditorViewModel"/> para a página coletar os valores ao
/// confirmar o diálogo. A senha é sincronizada com o
/// <see cref="PasswordBox"/> via code-behind (o Password não é bindable).
/// </summary>
public sealed partial class ItemEditorContent : UserControl
{
    private readonly ILocalizationService _localizacao;

    public ItemEditorViewModel ViewModel { get; }

    /// <summary>
    /// Disparado em qualquer interação (pointer/key ou PropertyChanged via
    /// Popup do ComboBox/Slider) para reiniciar o timer de inatividade.
    /// </summary>
    public event Action? Atividade;

    public ItemEditorContent()
    {
        ViewModel = App.Services.GetRequiredService<ItemEditorViewModel>();
        _localizacao = App.Services.GetRequiredService<ILocalizationService>();
        InitializeComponent();

        SenhaBox.PasswordChanged += (_, _) => ViewModel.Senha = SenhaBox.Password;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        AtualizarCorBarraForca();

        // Limpa o erro do campo assim que o usuário começa a corrigi-lo.
        CampoTitulo.TextChanged += (_, _) => ErroTitulo.Visibility = Visibility.Collapsed;
        SenhaBox.PasswordChanged += (_, _) => ErroSenha.Visibility = Visibility.Collapsed;
        CampoCategoria.TextChanged += (_, _) => ErroCategoria.Visibility = Visibility.Collapsed;

        // Limita a altura do conteúdo à janela, habilitando o scroll
        // (o ContentDialog corta o conteúdo que excede a tela).
        Loaded += (_, _) =>
        {
            var alturaDialogo = XamlRoot?.Size.Height ?? 0;
            ScrollEditor.MaxHeight = Math.Max(320, alturaDialogo - 180);
        };

        PointerMoved += (_, _) => Atividade?.Invoke();
        PointerPressed += (_, _) => Atividade?.Invoke();
        KeyDown += (_, _) => Atividade?.Invoke();
        ViewModel.PropertyChanged += (_, _) => Atividade?.Invoke();
    }

    /// <summary>
    /// Valida os campos obrigatórios (título, senha e categoria, exigidos
    /// pelo domínio). Exibe mensagem em vermelho abaixo de cada campo em
    /// branco e retorna <c>false</c> se algum estiver inválido.
    /// </summary>
    public bool ValidarCamposObrigatorios()
    {
        var formato = _localizacao.GetString("ItemEditor_Erro_Obrigatorio");
        var valido = true;

        valido &= MarcarErroSeEmBranco(string.IsNullOrWhiteSpace(ViewModel.Titulo),
            ErroTitulo, _localizacao.GetString("ItemEditor_Titulo.Header"), formato);
        valido &= MarcarErroSeEmBranco(string.IsNullOrWhiteSpace(ViewModel.Senha),
            ErroSenha, _localizacao.GetString("ItemEditor_SenhaBox.Header"), formato);
        valido &= MarcarErroSeEmBranco(string.IsNullOrWhiteSpace(ViewModel.Categoria),
            ErroCategoria, _localizacao.GetString("ItemEditor_Categoria.Header"), formato);

        return valido;
    }

    private static bool MarcarErroSeEmBranco(bool emBranco, TextBlock erro, string campo, string formato)
    {
        if (!emBranco)
        {
            erro.Visibility = Visibility.Collapsed;
            return true;
        }

        erro.Text = string.Format(formato, campo);
        erro.Visibility = Visibility.Visible;
        return false;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ItemEditorViewModel.Senha))
            SenhaBox.Password = ViewModel.Senha;

        if (e.PropertyName == nameof(ItemEditorViewModel.ForcaValor))
            AtualizarCorBarraForca();
    }

    /// <summary>
    /// Aplica o estilo da barra de força conforme o nível avaliado
    /// (fraca=danger, média=warning, forte=success), resolvendo os brushes
    /// do tema ativo.
    /// </summary>
    private void AtualizarCorBarraForca()
    {
        var chave = ViewModel.ForcaSenha switch
        {
            ForcaSenha.Forte => "PMBarraForte",
            ForcaSenha.Media => "PMBarraMedia",
            _ => "PMBarraFraca"
        };

        if (App.Current.Resources.TryGetValue(chave, out object? valor) && valor is Style estilo)
            BarraForca.Style = estilo;
    }

    private void OnAlternarVisibilidadeClick(object sender, RoutedEventArgs e)
    {
        SenhaBox.PasswordRevealMode = SenhaBox.PasswordRevealMode == PasswordRevealMode.Hidden
            ? PasswordRevealMode.Visible
            : PasswordRevealMode.Hidden;
    }
}