using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordManager.Application.Exceptions;
using PasswordManager.Application.VaultSession;
using PasswordManager.UI.Localization;
using System;
using System.Threading.Tasks;

namespace PasswordManager.UI.ViewModels;

/// <summary>
/// ViewModel da tela de desbloqueio: decide entre "criar" e "desbloquear"
/// conforme a existência de cofre e orquestra a derivação de chave fora da
/// thread da UI (o Argon2id custa da ordem de 1 s em produção).
/// </summary>
public partial class UnlockViewModel : ObservableObject
{
    private readonly IVaultSessionService _sessionService;
    private readonly ILocalizationService _localization;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UnlockCommand))]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private string senhaMestra = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private string confirmacaoSenha = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ErroTemValor))]
    private string? erro;

    /// <summary>
    /// Indica se há mensagem de erro a exibir (controla a visibilidade do
    /// ícone + texto de erro na tela de desbloqueio).
    /// </summary>
    public bool ErroTemValor => !string.IsNullOrWhiteSpace(Erro);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TituloModo))]
    private bool modoCriar;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UnlockCommand))]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private bool ocupado;

    /// <summary>
    /// Disparado na thread da UI após desbloquear/criar com sucesso.
    /// </summary>
    public event Action? Unlocked;

    public string TituloModo => ModoCriar
        ? _localization.GetString("UnlockViewModel_TituloModo_Criar")
        : _localization.GetString("UnlockViewModel_TituloModo_Desbloquear");

    public UnlockViewModel(IVaultSessionService sessionService, ILocalizationService localization)
    {
        _sessionService = sessionService;
        _localization = localization;
    }

    public async Task InitializeAsync()
    {
        Ocupado = true;
        try
        {
            ModoCriar = !await _sessionService.VaultExistsAsync();
        }
        finally
        {
            Ocupado = false;
        }
    }

    private bool CanUnlock() => !Ocupado && !string.IsNullOrWhiteSpace(SenhaMestra);

    private bool CanCreate() => !Ocupado && !string.IsNullOrWhiteSpace(SenhaMestra)
        && !string.IsNullOrWhiteSpace(ConfirmacaoSenha);

    [RelayCommand(CanExecute = nameof(CanUnlock))]
    private async Task UnlockAsync()
    {
        Erro = null;
        Ocupado = true;
        try
        {
            await Task.Run(async () => await _sessionService.UnlockAsync(SenhaMestra));
            Unlocked?.Invoke();
        }
        catch (CryptographicIntegrityException)
        {
            Erro = _localization.GetString("UnlockViewModel_Erro_SenhaIncorreta");
        }
        catch (InvalidOperationException ex)
        {
            Erro = ex.Message;
        }
        finally
        {
            Ocupado = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task CreateAsync()
    {
        if (SenhaMestra != ConfirmacaoSenha)
        {
            Erro = _localization.GetString("UnlockViewModel_Erro_SenhasNaoConferem");
            return;
        }

        Erro = null;
        Ocupado = true;
        try
        {
            await Task.Run(async () => await _sessionService.CreateAsync(SenhaMestra));
            Unlocked?.Invoke();
        }
        catch (InvalidOperationException ex)
        {
            Erro = ex.Message;
        }
        finally
        {
            Ocupado = false;
        }
    }

    /// <summary>
    /// Importa um backup .vault na primeira execução (ainda não existe
    /// cofre local): o cofre do arquivo vira o cofre da instalação e a
    /// sessão fica desbloqueada com a mesma senha mestra do arquivo.
    /// </summary>
    public async Task ImportarAsync(byte[] fileData, string masterPassword)
    {
        Erro = null;
        Ocupado = true;
        try
        {
            await Task.Run(async () => await _sessionService.ImportAsync(fileData, masterPassword, replace: true));
            Unlocked?.Invoke();
        }
        catch (CryptographicIntegrityException)
        {
            Erro = _localization.GetString("UnlockViewModel_Erro_ArquivoCorrompido");
        }
        catch (InvalidOperationException ex)
        {
            Erro = ex.Message;
        }
        finally
        {
            Ocupado = false;
        }
    }
}