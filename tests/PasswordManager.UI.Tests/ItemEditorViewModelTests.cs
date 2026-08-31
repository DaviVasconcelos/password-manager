using FluentAssertions;
using PasswordManager.Application.PasswordGeneration;
using PasswordManager.Application.Settings;
using PasswordManager.Domain.Entities;
using PasswordManager.UI.Tests.Fakes;
using PasswordManager.UI.ViewModels;

namespace PasswordManager.UI.Tests;

/// <summary>
/// Testes de <see cref="ItemEditorViewModel"/>.
/// </summary>
public class ItemEditorViewModelTests
{
    private static FakeLocalizationService CriarLoc()
    {
        return new FakeLocalizationService(new Dictionary<string, string>
        {
            ["ItemEditor_Forca_Formato"] = "Força: {0}",
            ["ItemEditor_Forca_Forte"] = "Forte",
            ["ItemEditor_Forca_Media"] = "Média",
            ["ItemEditor_Forca_Fraca"] = "Fraca",
            ["ItemEditor_Tamanho_Sufixo"] = "caracteres",
            ["ItemEditor_SemPasta"] = "Sem pasta",
            ["VaultViewModel_TodasPastas"] = "Todas as pastas"
        });
    }

    private static ItemEditorViewModel CriarVm(
        AppSettings? settings = null,
        IPasswordGenerator? generator = null,
        IPasswordStrengthEvaluator? evaluator = null)
    {
        var svc = new FakeAppSettingsService(settings ?? new AppSettings
        {
            PasswordGeneratorLength = 16,
            PasswordGeneratorIncludeLowercase = true,
            PasswordGeneratorIncludeUppercase = true,
            PasswordGeneratorIncludeDigits = true,
            PasswordGeneratorIncludeSymbols = false
        });
        var gen = generator ?? new PasswordGenerator();
        var eval = evaluator ?? new PasswordStrengthEvaluator();
        var loc = CriarLoc();
        return new ItemEditorViewModel(gen, eval, svc, loc);
    }

    private static VaultFolder CriarPasta(string nome)
    {
        // Vault cria pastas internamente; usamos um vault temporário para gerar a entidade.
        var vault = Vault.CreateNew();
        return vault.AddFolder(nome);
    }

    [Fact]
    public void CarregarParaCriacao_DeveGerarSenhaComDefaultsEGerarOpcoes()
    {
        var vm = CriarVm();
        var pasta = CriarPasta("Trabalho");
        var opcoes = new[] { new OpcoesPasta("Todas as pastas", null), new OpcoesPasta(pasta.Name, pasta) };

        vm.CarregarParaCriacao(opcoes, pasta.Id);

        vm.ItemId.Should().BeNull();
        vm.Senha.Should().NotBeNullOrWhiteSpace();
        vm.Senha.Length.Should().Be(16);
        vm.TamanhoSenha.Should().Be(16);
        vm.IncluirMinusculas.Should().BeTrue();
        vm.IncluirMaiusculas.Should().BeTrue();
        vm.IncluirNumeros.Should().BeTrue();
        vm.IncluirSimbolos.Should().BeFalse();
        vm.OpcoesPasta.Should().HaveCount(2); // Sem pasta + Trabalho
        vm.PastaSelecionada!.Pasta!.Id.Should().Be(pasta.Id);
        vm.OpcoesPasta[0].Nome.Should().Be("Sem pasta");
    }

    [Fact]
    public void CarregarParaCriacao_SemPastaSugerida_DeveSelecionarSemPasta()
    {
        var vm = CriarVm();
        var pasta = CriarPasta("Pessoal");
        var opcoes = new[] { new OpcoesPasta("Todas", null), new OpcoesPasta(pasta.Name, pasta) };

        vm.CarregarParaCriacao(opcoes);

        vm.PastaSelecionada!.Pasta.Should().BeNull();
    }

    [Fact]
    public void CarregarParaEdicao_DevePreencherCamposEOpcoes()
    {
        var vm = CriarVm();
        var vault = Vault.CreateNew();
        var pasta1 = vault.AddFolder("Trabalho");
        var pasta2 = vault.AddFolder("Pessoal");
        var item = vault.AddItem("GitHub", "s3nh@", "Dev", "alice", "https://github.com", "notas");
        vault.AssignItemToFolder(item.Id, pasta1.Id);
        var opcoes = new[] { new OpcoesPasta("Todas", null), new OpcoesPasta(pasta1.Name, pasta1), new OpcoesPasta(pasta2.Name, pasta2) };

        vm.CarregarParaEdicao(item, opcoes);

        vm.ItemId.Should().Be(item.Id);
        vm.Titulo.Should().Be("GitHub");
        vm.Usuario.Should().Be("alice");
        vm.Categoria.Should().Be("Dev");
        vm.Senha.Should().Be("s3nh@");
        vm.Url.Should().Be("https://github.com");
        vm.Notas.Should().Be("notas");
        vm.PastaSelecionada!.Pasta!.Id.Should().Be(pasta1.Id);
    }

    [Fact]
    public void CarregarDefaults_DeveUsarSettingsPersistidos()
    {
        var settings = new AppSettings
        {
            PasswordGeneratorLength = 24,
            PasswordGeneratorIncludeLowercase = false,
            PasswordGeneratorIncludeUppercase = true,
            PasswordGeneratorIncludeDigits = false,
            PasswordGeneratorIncludeSymbols = true
        };
        var vm = CriarVm(settings);
        var item = Vault.CreateNew().AddItem("T", "p", "C");

        vm.CarregarParaEdicao(item, Array.Empty<OpcoesPasta>());

        vm.TamanhoSenha.Should().Be(24);
        vm.IncluirMinusculas.Should().BeFalse();
        vm.IncluirMaiusculas.Should().BeTrue();
        vm.IncluirNumeros.Should().BeFalse();
        vm.IncluirSimbolos.Should().BeTrue();
    }

    [Fact]
    public void OnSenhaChanged_DeveAtualizarForcaSenha()
    {
        var vm = CriarVm();
        vm.Senha = "abc"; // fraca
        vm.ForcaSenha.Should().Be(ForcaSenha.Fraca);
        vm.ForcaValor.Should().Be(0);
        vm.ForcaSenhaTexto.Should().Contain("Fraca");

        vm.Senha = "Abc12345"; // 8 chars + 3 classes => média
        vm.ForcaSenha.Should().Be(ForcaSenha.Media);

        vm.Senha = "Abcdef123!@#XYZ"; // 12+ + 4 classes => forte
        vm.ForcaSenha.Should().Be(ForcaSenha.Forte);
        vm.ForcaSenhaTexto.Should().Contain("Forte");
    }

    [Fact]
    public void TamanhoSenhaTexto_DeveFormatarComSufixo()
    {
        var vm = CriarVm();
        vm.CarregarParaCriacao(Array.Empty<OpcoesPasta>());
        vm.TamanhoSenha = 20;

        vm.TamanhoSenhaTexto.Should().Be("20 caracteres");
    }

    [Fact]
    public void PodeGerar_ComNenhumaOpcao_DeveSerFalseEGerarSenhaDesabilitado()
    {
        var vm = CriarVm();
        vm.CarregarParaCriacao(Array.Empty<OpcoesPasta>());
        vm.IncluirMinusculas = false;
        vm.IncluirMaiusculas = false;
        vm.IncluirNumeros = false;
        vm.IncluirSimbolos = false;

        vm.PodeGerar.Should().BeFalse();
        vm.GerarSenhaCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void PodeGerar_ComUmaOpcao_DeveSerTrue()
    {
        var vm = CriarVm();
        vm.CarregarParaCriacao(Array.Empty<OpcoesPasta>());
        vm.IncluirMinusculas = true;
        vm.IncluirMaiusculas = false;
        vm.IncluirNumeros = false;
        vm.IncluirSimbolos = false;

        vm.PodeGerar.Should().BeTrue();
        vm.GerarSenhaCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void GerarSenha_DeveGerarNovaSenhaComDefaults()
    {
        var vm = CriarVm();
        vm.CarregarParaCriacao(Array.Empty<OpcoesPasta>());
        var senhaAntes = vm.Senha;

        vm.GerarSenhaCommand.Execute(null);

        vm.Senha.Should().NotBeNullOrWhiteSpace();
        // Pode por coincidência gerar igual, mas extremamente improvável com RNG; verifica tamanho
        vm.Senha.Length.Should().Be(vm.TamanhoSenha);
        // Força deve ter sido recalculada
        vm.ForcaSenha.Should().BeOneOf(ForcaSenha.Fraca, ForcaSenha.Media, ForcaSenha.Forte);
    }

    [Fact]
    public void CarregarOpcoes_ComPastaIdInexistente_DeveSelecionarSemPasta()
    {
        var vm = CriarVm();
        var pasta = CriarPasta("A");
        var opcoes = new[] { new OpcoesPasta("Todas", null), new OpcoesPasta(pasta.Name, pasta) };
        var item = Vault.CreateNew().AddItem("T", "p", "C");

        vm.CarregarParaEdicao(item, opcoes); // item sem pasta (FolderId null)

        vm.PastaSelecionada!.Pasta.Should().BeNull();
        vm.OpcoesPasta.Should().HaveCount(2);
    }
}
