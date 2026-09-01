using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordManager.Application.Exceptions;
using PasswordManager.Application.VaultRegistry;
using PasswordManager.Application.VaultSession;
using PasswordManager.UI.Localization;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace PasswordManager.UI.ViewModels;

/// <summary>
/// ViewModel da tela de desbloqueio: decide entre "criar" e "desbloquear"
/// conforme a existência de cofres, orquestra a derivação de chave fora da
/// thread da UI (o Argon2id custa da ordem de 1 s em produção) e gerencia
/// múltiplos arquivos de cofre (ADR 0008, Opção B) antes do desbloqueio.
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
    /// Lista de arquivos de cofre disponíveis antes do desbloqueio.
    /// </summary>
    public ObservableCollection<VaultDescriptor> Cofres { get; } = new();

    [ObservableProperty]
    private VaultDescriptor? cofreSelecionado;

    [ObservableProperty]
    private string novoNomeCofre = string.Empty;

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
            // Carrega lista de cofres (multi-arquivo); fallback legado usa VaultExistsAsync.
            try
            {
                var lista = await _sessionService.ListarCofresAsync();
                Cofres.Clear();
                foreach (var d in lista.OrderBy(x => x.Nome, StringComparer.OrdinalIgnoreCase))
                    Cofres.Add(d);

                if (Cofres.Count > 0)
                {
                    CofreSelecionado = _sessionService.CofreAtivo
                                       ?? Cofres.FirstOrDefault();
                    ModoCriar = false;
                    return;
                }
            }
            catch
            {
                // Fallback legado: ignora e usa VaultExistsAsync
            }

            ModoCriar = !await _sessionService.VaultExistsAsync();
            if (Cofres.Count > 0 && CofreSelecionado is null)
                CofreSelecionado = Cofres.FirstOrDefault();
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
            // Garante que o cofre selecionado está ativo antes de desbloquear (multi-arquivo).
            if (CofreSelecionado is not null)
            {
                try { await _sessionService.SelecionarCofreAsync(CofreSelecionado.Id); } catch { }
            }

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
            var nome = string.IsNullOrWhiteSpace(NovoNomeCofre) ? null : NovoNomeCofre.Trim();
            await Task.Run(async () => await _sessionService.CreateAsync(nome, SenhaMestra));
            await RecarregarCofresAsync();
            Unlocked?.Invoke();
        }
        catch (InvalidOperationException ex)
        {
            Erro = ex.Message;
        }
        catch (ArgumentException ex)
        {
            Erro = ex.Message;
        }
        finally
        {
            Ocupado = false;
        }
    }

    /// <summary>
    /// Cria um novo arquivo de cofre com nome específico (fluxo multi-arquivo).
    /// Se <paramref name="nome"/> for nulo/vazio, gera "vault-1", "vault-2", ...
    /// </summary>
    public async Task CriarNovoArquivoAsync(string? nome)
    {
        Erro = null;
        Ocupado = true;
        try
        {
            if (string.IsNullOrWhiteSpace(SenhaMestra))
            {
                Erro = _localization.GetString("UnlockViewModel_Erro_SenhaIncorreta");
                return;
            }
            if (SenhaMestra != ConfirmacaoSenha && !string.IsNullOrWhiteSpace(ConfirmacaoSenha))
            {
                Erro = _localization.GetString("UnlockViewModel_Erro_SenhasNaoConferem");
                return;
            }

            var nomeEfetivo = string.IsNullOrWhiteSpace(nome) ? NovoNomeCofre : nome;
            nomeEfetivo = string.IsNullOrWhiteSpace(nomeEfetivo) ? null : nomeEfetivo;

            await Task.Run(async () => await _sessionService.CreateAsync(nomeEfetivo, SenhaMestra));
            await RecarregarCofresAsync();
            Unlocked?.Invoke();
        }
        catch (InvalidOperationException ex)
        {
            Erro = ex.Message;
        }
        catch (ArgumentException ex)
        {
            Erro = ex.Message;
        }
        finally
        {
            Ocupado = false;
        }
    }

    /// <summary>
    /// Seleciona o cofre ativo (troca antes do desbloqueio). Tranca a sessão atual.
    /// </summary>
    public async Task SelecionarCofreAsync(Guid id)
    {
        Erro = null;
        Ocupado = true;
        try
        {
            await _sessionService.SelecionarCofreAsync(id);
            CofreSelecionado = Cofres.FirstOrDefault(c => c.Id == id) ?? _sessionService.CofreAtivo;
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
    /// Renomeia um arquivo de cofre sem precisar desbloqueio.
    /// </summary>
    public async Task RenomearCofreAsync(Guid id, string novoNome)
    {
        Erro = null;
        Ocupado = true;
        try
        {
            await _sessionService.RenomearCofreAsync(id, novoNome);
            await RecarregarCofresAsync();
        }
        catch (InvalidOperationException ex)
        {
            Erro = ex.Message;
        }
        catch (ArgumentException ex)
        {
            Erro = ex.Message;
        }
        finally
        {
            Ocupado = false;
        }
    }

    /// <summary>
    /// Exclui um arquivo de cofre sem precisar desbloqueio.
    /// </summary>
    public async Task ExcluirCofreAsync(Guid id)
    {
        Erro = null;
        Ocupado = true;
        try
        {
            await _sessionService.ExcluirCofreAsync(id);
            await RecarregarCofresAsync();
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

    private async Task RecarregarCofresAsync()
    {
        try
        {
            var lista = await _sessionService.ListarCofresAsync();
            var selecionadoId = CofreSelecionado?.Id ?? _sessionService.CofreAtivo?.Id;
            Cofres.Clear();
            foreach (var d in lista.OrderBy(x => x.Nome, StringComparer.OrdinalIgnoreCase))
                Cofres.Add(d);

            if (Cofres.Count == 0)
            {
                CofreSelecionado = null;
                ModoCriar = true;
            }
            else
            {
                CofreSelecionado = Cofres.FirstOrDefault(c => c.Id == selecionadoId) ?? Cofres.First();
                ModoCriar = false;
            }
        }
        catch
        {
            // Fallback legado: mantém ModoCriar via VaultExists
            ModoCriar = !await _sessionService.VaultExistsAsync();
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
            try { await RecarregarCofresAsync(); } catch { }
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
