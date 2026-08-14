using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordManager.Application.Exceptions;
using PasswordManager.Application.VaultSession;
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

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DesbloquearCommand))]
    [NotifyCanExecuteChangedFor(nameof(CriarCommand))]
    private string senhaMestra = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CriarCommand))]
    private string confirmacaoSenha = string.Empty;

    [ObservableProperty]
    private string? erro;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TituloModo))]
    private bool modoCriar;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DesbloquearCommand))]
    [NotifyCanExecuteChangedFor(nameof(CriarCommand))]
    private bool ocupado;

    /// <summary>
    /// Disparado na thread da UI após desbloquear/criar com sucesso.
    /// </summary>
    public event Action? Desbloqueado;

    public string TituloModo => ModoCriar ? "Criar novo cofre" : "Desbloquear cofre";

    public UnlockViewModel(IVaultSessionService sessionService)
    {
        _sessionService = sessionService;
    }

    public async Task InicializarAsync()
    {
        Ocupado = true;
        try
        {
            ModoCriar = !await _sessionService.ExisteCofreAsync();
        }
        finally
        {
            Ocupado = false;
        }
    }

    private bool CanDesbloquear() => !Ocupado && !string.IsNullOrWhiteSpace(SenhaMestra);

    private bool CanCriar() => !Ocupado && !string.IsNullOrWhiteSpace(SenhaMestra)
        && !string.IsNullOrWhiteSpace(ConfirmacaoSenha);

    [RelayCommand]
    private async Task DesbloquearAsync()
    {
        Erro = null;
        Ocupado = true;
        try
        {
            await Task.Run(async () => await _sessionService.DesbloquearAsync(SenhaMestra));
            Desbloqueado?.Invoke();
        }
        catch (CryptographicIntegrityException)
        {
            Erro = "Senha mestra incorreta ou cofre corrompido.";
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

    [RelayCommand]
    private async Task CriarAsync()
    {
        if (SenhaMestra != ConfirmacaoSenha)
        {
            Erro = "As senhas não conferem.";
            return;
        }

        Erro = null;
        Ocupado = true;
        try
        {
            await Task.Run(async () => await _sessionService.CriarAsync(SenhaMestra));
            Desbloqueado?.Invoke();
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