using FluentAssertions;
using PasswordManager.Application.Settings;
using PasswordManager.Application.VaultSession;
using PasswordManager.UI.Tests.Fakes;
using PasswordManager.UI.ViewModels;

namespace PasswordManager.UI.Tests;

/// <summary>
/// Testes de <see cref="VaultViewModel"/> com fakes de timers e clipboard.
/// Nomes em pt-BR no padrão Método_Cenário_ResultadoEsperado.
/// </summary>
public class VaultViewModelTests
{
    private sealed record Sut(
        VaultViewModel Vm,
        FakeClipboardService Clipboard,
        FakeTimerFactory Timers,
        FakeAppSettingsService Settings,
        FakeLocalizationService Loc,
        IVaultSessionService Session);

    private static async Task<Sut> CriarSutAsync(AppSettings? settings = null, Dictionary<string, string>? locMapa = null)
    {
        var repo = new FakeVaultRepository();
        var crypto = new FakeCryptoService();
        var export = new FakeExportImportService();
        var session = new VaultSessionService(repo, crypto, export);
        await session.CreateAsync("senha-mestra-123");

        var appSettings = settings ?? new AppSettings
        {
            AutoLockTimeoutMinutes = 2,
            ClipboardCleanTimeSeconds = 30,
            PasswordGeneratorLength = 16,
            PasswordGeneratorIncludeLowercase = true,
            PasswordGeneratorIncludeUppercase = true,
            PasswordGeneratorIncludeDigits = true,
            PasswordGeneratorIncludeSymbols = false
        };
        var settingsService = new FakeAppSettingsService(appSettings);
        var loc = new FakeLocalizationService(locMapa ?? new Dictionary<string, string>
        {
            ["VaultViewModel_TodasPastas"] = "Todas as pastas",
            ["VaultPage_ToastSenhaCopiada.Text"] = "Senha copiada! Limpa em {0}s",
            ["VaultPage_ToastCofreExportado.Text"] = "Cofre exportado com sucesso",
            ["VaultPage_ToastCofreImportado.Text"] = "Cofre importado com sucesso"
        });
        var clipboard = new FakeClipboardService();
        var timers = new FakeTimerFactory();
        var vm = new VaultViewModel(session, settingsService, loc, timers, clipboard);
        return new Sut(vm, clipboard, timers, settingsService, loc, session);
    }

    [Fact]
    public async Task Inicializar_DeveAplicarConfiguracoesERecarregarPastas()
    {
        var sut = await CriarSutAsync();
        await sut.Session.AddFolderAsync("Pessoal");

        sut.Vm.Inicializar();

        sut.Vm.FolderOptions.Should().HaveCount(2);
        sut.Vm.FolderOptions[0].Nome.Should().Be("Todas as pastas");
        sut.Vm.FolderOptions[1].Nome.Should().Be("Pessoal");
        sut.Timers.TimerInatividade.IsRunning.Should().BeTrue();
        sut.Timers.TimerInatividade.Interval.Should().Be(TimeSpan.FromMinutes(2));
    }

    [Fact]
    public async Task AplicarConfiguracoes_DeveAtualizarTimeoutsEReiniciarTimerInatividade()
    {
        var sut = await CriarSutAsync();
        sut.Vm.Inicializar();
        sut.Timers.TimerInatividade.Stop();
        sut.Settings.Get().Should().NotBeNull();

        // Altera settings via SaveAsync e reaplica
        await sut.Settings.SaveAsync(new AppSettings
        {
            AutoLockTimeoutMinutes = 5,
            ClipboardCleanTimeSeconds = 15,
            PasswordGeneratorLength = 20
        });
        sut.Vm.AplicarConfiguracoes();

        sut.Timers.TimerInatividade.Interval.Should().Be(TimeSpan.FromMinutes(5));
        sut.Timers.TimerInatividade.IsRunning.Should().BeTrue();
        sut.Vm.TextoToastSenhaCopiada.Should().Contain("15");
    }

    [Fact]
    public async Task ReloadFolders_ComPastasExistentes_DeveReconstruirOpcoesPreservandoSelecao()
    {
        var sut = await CriarSutAsync();
        sut.Vm.Inicializar();
        var pasta1 = await sut.Session.AddFolderAsync("Trabalho");
        var pasta2 = await sut.Session.AddFolderAsync("Banco");
        sut.Vm.ReloadFolders();
        sut.Vm.OpcaoPastaSelecionada = sut.Vm.FolderOptions.First(o => o.Pasta?.Id == pasta1.Id);

        // Adiciona nova pasta e recarrega — seleção deve permanecer em Trabalho
        await sut.Session.AddFolderAsync("Pessoal");
        sut.Vm.ReloadFolders();

        sut.Vm.FolderOptions.Should().HaveCount(4); // Todas + 3
        sut.Vm.OpcaoPastaSelecionada!.Pasta!.Id.Should().Be(pasta1.Id);
    }

    [Fact]
    public async Task AddFilter_ComTermoBusca_DeveFiltrarPorTituloUsuarioUrlNotasCategoria()
    {
        var sut = await CriarSutAsync();
        sut.Vm.Inicializar();
        await sut.Session.AddItemAsync("GitHub", "s1", "Dev", "alice", "https://github.com", "notas github");
        await sut.Session.AddItemAsync("Gmail", "s2", "Email", "bob", "https://mail.google.com", "email pessoal");
        await sut.Session.AddItemAsync("Banco", "s3", "Financeiro", "alice", "https://banco.com", "conta");
        sut.Vm.ReloadFolders(); // carrega DisplayedItems sem filtro

        sut.Vm.TermoBusca = "alice";
        sut.Vm.DisplayedItems.Should().HaveCount(2);

        sut.Vm.TermoBusca = "github";
        sut.Vm.DisplayedItems.Should().HaveCount(1);
        sut.Vm.DisplayedItems[0].Title.Should().Be("GitHub");

        sut.Vm.TermoBusca = "Financeiro";
        sut.Vm.DisplayedItems.Should().HaveCount(1);

        sut.Vm.TermoBusca = "mail.google";
        sut.Vm.DisplayedItems.Should().HaveCount(1);

        sut.Vm.TermoBusca = "notas github";
        sut.Vm.DisplayedItems.Should().HaveCount(1);
    }

    [Fact]
    public async Task AddFilter_ComPastaSelecionada_DeveFiltrarPorPasta()
    {
        var sut = await CriarSutAsync();
        sut.Vm.Inicializar();
        var pasta = await sut.Session.AddFolderAsync("Trabalho");
        var item1 = await sut.Session.AddItemAsync("Jira", "s1", "Dev", "user");
        await sut.Session.AssignItemToFolderAsync(item1.Id, pasta.Id);
        await sut.Session.AddItemAsync("GitHub", "s2", "Dev");
        sut.Vm.ReloadFolders();

        sut.Vm.OpcaoPastaSelecionada = sut.Vm.FolderOptions.First(o => o.Pasta?.Id == pasta.Id);

        sut.Vm.DisplayedItems.Should().HaveCount(1);
        sut.Vm.DisplayedItems[0].Title.Should().Be("Jira");
    }

    [Fact]
    public async Task AddFilter_QuandoResultadoIgual_NaoDeveRecarregarDisplayedItems()
    {
        var sut = await CriarSutAsync();
        sut.Vm.Inicializar();
        await sut.Session.AddItemAsync("GitHub", "s1", "Dev");
        sut.Vm.ReloadFolders();
        var antes = sut.Vm.DisplayedItems.ToList();

        // Chamando ReloadFolders sem mudar nada deve manter a lista (SequenceEqual evita Clear+Add)
        sut.Vm.ReloadFolders();

        sut.Vm.DisplayedItems.Should().BeEquivalentTo(antes, o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task AddItemAsync_DeveCriarItemEAtribuirPastaEAtualizarFiltro()
    {
        var sut = await CriarSutAsync();
        sut.Vm.Inicializar();
        var pasta = await sut.Session.AddFolderAsync("Pessoal");
        sut.Vm.ReloadFolders();

        await sut.Vm.AddItemAsync("Novo", "s3nh@", "Teste", "user", null, null, pasta.Id);

        sut.Session.CurrentVault.Items.Should().HaveCount(1);
        sut.Session.CurrentVault.Items.First().FolderId.Should().Be(pasta.Id);
        sut.Vm.DisplayedItems.Should().HaveCount(1);
    }

    [Fact]
    public async Task ReloadItemAsync_DeveAtualizarItemEReatribuirPastaComForcarAtualizacao()
    {
        var sut = await CriarSutAsync();
        sut.Vm.Inicializar();
        var pasta = await sut.Session.AddFolderAsync("Work");
        var item = await sut.Session.AddItemAsync("Old", "old", "Cat");
        sut.Vm.ReloadFolders();
        var countAntes = sut.Vm.DisplayedItems.Count;

        await sut.Vm.ReloadItemAsync(item.Id, "New", "newPass", "Cat2", "user2", "https://x", "notas", pasta.Id);

        sut.Session.CurrentVault.Items.First().Title.Should().Be("New");
        sut.Session.CurrentVault.Items.First().FolderId.Should().Be(pasta.Id);
        // Forçar atualização garante que mesmo com mesma contagem a UI reflita mutação
        sut.Vm.DisplayedItems.Should().HaveCount(countAntes);
        sut.Vm.DisplayedItems[0].Title.Should().Be("New");
    }

    [Fact]
    public async Task AdicionarPasta_RenomearPasta_RemoverPasta_DevemRecarregarPastas()
    {
        var sut = await CriarSutAsync();
        sut.Vm.Inicializar();

        await sut.Vm.AdicionarPastaAsync("Nova");
        sut.Vm.FolderOptions.Should().Contain(o => o.Nome == "Nova");

        var pasta = sut.Session.CurrentVault.Folders.First(f => f.Name == "Nova");
        await sut.Vm.RenomearPastaAsync(pasta.Id, "Renomeada");
        sut.Vm.FolderOptions.Should().Contain(o => o.Nome == "Renomeada");

        await sut.Vm.RemoverPastaAsync(pasta.Id);
        sut.Vm.FolderOptions.Should().NotContain(o => o.Nome == "Renomeada");
    }

    [Fact]
    public async Task CopiarSenha_DeveCopiarParaClipboardEIniciarTimerLimpeza()
    {
        var sut = await CriarSutAsync();
        sut.Vm.Inicializar();
        var item = await sut.Session.AddItemAsync("Site", "P@ss123", "Cat");

        sut.Vm.CopiarSenhaCommand.Execute(item);

        sut.Clipboard.UltimoTexto.Should().Be("P@ss123");
        sut.Clipboard.ChamadasSetText.Should().Be(1);
        sut.Vm.SenhaCopiada.Should().BeTrue();
        sut.Timers.TimerClipboard.IsRunning.Should().BeTrue();
        sut.Timers.TimerClipboard.Interval.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task CopiarSenha_AoDispararTimer_DeveLimparClipboardEResetarFlag()
    {
        var sut = await CriarSutAsync();
        sut.Vm.Inicializar();
        var item = await sut.Session.AddItemAsync("Site", "secret", "Cat");
        sut.Vm.CopiarSenhaCommand.Execute(item);

        sut.Timers.TimerClipboard.DispararTick();

        sut.Clipboard.UltimoTexto.Should().Be(string.Empty);
        sut.Clipboard.ChamadasClear.Should().Be(1);
        sut.Vm.SenhaCopiada.Should().BeFalse();
        sut.Timers.TimerClipboard.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task CopiarSenha_ComItemNulo_NaoDeveCopiar()
    {
        var sut = await CriarSutAsync();
        sut.Vm.Inicializar();

        sut.Vm.CopiarSenhaCommand.Execute(null);

        sut.Clipboard.ChamadasSetText.Should().Be(0);
        sut.Vm.SenhaCopiada.Should().BeFalse();
    }

    [Fact]
    public async Task CopiarUsuario_ComUsernameVazio_NaoDeveCopiar()
    {
        var sut = await CriarSutAsync();
        sut.Vm.Inicializar();
        var itemSemUser = await sut.Session.AddItemAsync("Site", "pass", "Cat", null);

        // Username vazio
        sut.Vm.CopiarUsuarioCommand.Execute(itemSemUser);
        sut.Clipboard.ChamadasSetText.Should().Be(0);

        // Username preenchido deve copiar
        var itemComUser = await sut.Session.AddItemAsync("Site2", "pass", "Cat", "alice");
        sut.Vm.CopiarUsuarioCommand.Execute(itemComUser);
        sut.Clipboard.UltimoTexto.Should().Be("alice");
    }

    [Fact]
    public async Task CopiarUsuario_DeveCopiarSemIniciarTimerNemToast()
    {
        var sut = await CriarSutAsync();
        sut.Vm.Inicializar();
        var item = await sut.Session.AddItemAsync("Site", "pass", "Cat", "bob");

        sut.Vm.CopiarUsuarioCommand.Execute(item);

        sut.Clipboard.UltimoTexto.Should().Be("bob");
        sut.Vm.SenhaCopiada.Should().BeFalse();
        sut.Timers.TimerClipboard.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task NotificarAtividade_DeveReiniciarTimerInatividade()
    {
        var sut = await CriarSutAsync();
        sut.Vm.Inicializar();
        sut.Timers.TimerInatividade.Stop();
        sut.Timers.TimerInatividade.ChamadasStart.Should().BeGreaterThanOrEqualTo(1);

        var startAntes = sut.Timers.TimerInatividade.ChamadasStart;
        sut.Vm.NotificarAtividade();

        sut.Timers.TimerInatividade.ChamadasStart.Should().Be(startAntes + 1);
        sut.Timers.TimerInatividade.IsRunning.Should().BeTrue();
    }

    [Fact]
    public async Task TimerInatividade_AoDisparar_DeveTrancarSessaoELimparFlagsEDispararEvento()
    {
        var sut = await CriarSutAsync();
        sut.Vm.Inicializar();
        var item = await sut.Session.AddItemAsync("Site", "pass", "Cat");
        sut.Vm.CopiarSenhaCommand.Execute(item);
        sut.Vm.MostrarInfoBanner("teste");
        bool trancado = false;
        sut.Vm.Trancado += () => trancado = true;

        sut.Timers.TimerInatividade.DispararTick();

        sut.Session.Unlocked.Should().BeFalse();
        sut.Vm.SenhaCopiada.Should().BeFalse();
        sut.Vm.InfoBannerVisivel.Should().BeFalse();
        trancado.Should().BeTrue();
        sut.Timers.TimerInatividade.IsRunning.Should().BeFalse();
        sut.Timers.TimerClipboard.IsRunning.Should().BeFalse();
        sut.Timers.TimerInfoBanner.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task PararTimers_DevePararTodosOsTimers()
    {
        var sut = await CriarSutAsync();
        sut.Vm.Inicializar();
        var item = await sut.Session.AddItemAsync("Site", "pass", "Cat");
        sut.Vm.CopiarSenhaCommand.Execute(item);
        sut.Vm.MostrarInfoBanner("banner");

        sut.Vm.PararTimers();

        sut.Timers.TimerClipboard.IsRunning.Should().BeFalse();
        sut.Timers.TimerInatividade.IsRunning.Should().BeFalse();
        sut.Timers.TimerInfoBanner.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task MostrarInfoBanner_DeveExibirPor4SegundosEFecharInfoBanner_DeveOcultar()
    {
        var sut = await CriarSutAsync();
        sut.Vm.Inicializar();

        sut.Vm.MostrarInfoBanner("mensagem");

        sut.Vm.InfoBannerVisivel.Should().BeTrue();
        sut.Vm.TextoInfoBanner.Should().Be("mensagem");
        sut.Timers.TimerInfoBanner.IsRunning.Should().BeTrue();
        sut.Timers.TimerInfoBanner.Interval.Should().Be(TimeSpan.FromSeconds(4));

        // Dispara tick de 4s
        sut.Timers.TimerInfoBanner.DispararTick();
        sut.Vm.InfoBannerVisivel.Should().BeFalse();

        // Mostra de novo e fecha manualmente
        sut.Vm.MostrarInfoBanner("outra");
        sut.Vm.FecharInfoBannerCommand.Execute(null);
        sut.Vm.InfoBannerVisivel.Should().BeFalse();
        sut.Timers.TimerInfoBanner.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task NotificarExportacaoSucesso_EImportacaoSucesso_DevemMostrarBannerCorreto()
    {
        var sut = await CriarSutAsync();
        sut.Vm.Inicializar();

        sut.Vm.NotificarExportacaoSucesso();
        sut.Vm.TextoInfoBanner.Should().Be("Cofre exportado com sucesso");
        sut.Vm.InfoBannerVisivel.Should().BeTrue();

        sut.Vm.NotificarImportacaoSucesso();
        sut.Vm.TextoInfoBanner.Should().Be("Cofre importado com sucesso");
    }

    [Fact]
    public async Task TrocarSenhaMestraAsync_DeveDelegarParaSessionService()
    {
        var sut = await CriarSutAsync();
        sut.Vm.Inicializar();

        await sut.Vm.TrocarSenhaMestraAsync("senha-mestra-123", "nova-senha-456");

        // Após troca, a sessão continua desbloqueada e a nova senha funciona após Lock+Unlock
        sut.Session.Lock();
        await sut.Session.UnlockAsync("nova-senha-456");
        sut.Session.Unlocked.Should().BeTrue();
    }

    [Fact]
    public async Task RemoverItemCommand_ComItemNulo_NaoDeveFalhar()
    {
        var sut = await CriarSutAsync();
        sut.Vm.Inicializar();
        await sut.Session.AddItemAsync("A", "p", "C");
        sut.Vm.ReloadFolders();

        Func<Task> act = () => sut.Vm.RemoverItemCommand.ExecuteAsync(null);
        await act.Should().NotThrowAsync();
        sut.Vm.DisplayedItems.Should().HaveCount(1);
    }

    [Fact]
    public async Task RemoverItemCommand_ComItemValido_DeveRemoverELimparSelecao()
    {
        var sut = await CriarSutAsync();
        sut.Vm.Inicializar();
        var item = await sut.Session.AddItemAsync("A", "p", "C");
        sut.Vm.ReloadFolders();
        sut.Vm.ItemSelecionado = sut.Vm.DisplayedItems.First();

        await sut.Vm.RemoverItemCommand.ExecuteAsync(item);

        sut.Session.CurrentVault.Items.Should().BeEmpty();
        sut.Vm.DisplayedItems.Should().BeEmpty();
        sut.Vm.ItemSelecionado.Should().BeNull();
    }

    [Fact]
    public async Task ExportarAsync_EImportarAsync_DevemDelegarERecarregarPastas()
    {
        var sut = await CriarSutAsync();
        sut.Vm.Inicializar();
        var bytes = await sut.Vm.ExportarAsync("nova-senha");
        bytes.Should().NotBeEmpty();

        // Import substitui/mescla e recarrega pastas
        await sut.Vm.ImportarAsync(new byte[] { 0x01 }, "nova-senha", true);
        sut.Vm.FolderOptions.Should().NotBeEmpty();
    }
}
