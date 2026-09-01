<h1 align="center"><img src="src/PasswordManager.UI/Assets/logo-password-manager.png" width="43" height="43" style="vertical-align: middle;" /> PasswordManager</h1>

<p align="center">
  <strong>Gerenciador de senhas local, offline-first, com criptografia de ponta a ponta.</strong><br/>
  C# / .NET 8 &middot; WinUI 3 &middot; Clean Architecture &middot; SQLite + EF Core &middot; AES-256-GCM + Argon2id
</p>

<p align="center">
  <a href="https://github.com/DaviVasconcelos/password-manager/actions/workflows/ci.yml"><img alt="CI" src="https://github.com/DaviVasconcelos/password-manager/actions/workflows/ci.yml/badge.svg" /></a>
  <img alt=".NET" src="https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet" />
  <img alt="WinUI 3" src="https://img.shields.io/badge/WinUI-3-0078D4?logo=windows" />
  <img alt="Platform" src="https://img.shields.io/badge/platform-Windows%2010%2F11-0078D4?logo=windows" />
  <img alt="Tests" src="https://img.shields.io/badge/tests-247%20passing-brightgreen" />
  <img alt="Distribuição" src="https://img.shields.io/badge/distribui%C3%A7%C3%A3o-MSI%20(WiX%205)-blue" />
  <img alt="License" src="https://img.shields.io/badge/license-MIT-green" />
</p>

> Projeto de portfólio focado em **Clean Architecture**, **criptografia aplicada**, **DDD** e **boas práticas de engenharia** — sem telemetria, sem nuvem, sem dependência de terceiros para seus segredos.

---

## Índice

- [Sobre](#sobre)
- [Funcionalidades](#funcionalidades)
- [Arquitetura](#arquitetura)
- [Segurança e Criptografia](#segurança-e-criptografia)
- [Stack](#stack)
- [Estrutura do Projeto](#estrutura-do-projeto)
- [Pré-requisitos](#pré-requisitos)
- [Instalação e Execução](#instalação-e-execução)
- [Distribuição (MSI)](#distribuição-msi)
- [Como Usar](#como-usar)
- [Configurações](#configurações)
- [Internacionalização (i18n)](#internacionalização-i18n)
- [Exportação e Importação (.vault)](#exportação-e-importação-vault)
- [Testes](#testes)
- [CI/CD](#cicd)
- [Decisões de Arquitetura (ADRs)](#decisões-de-arquitetura-adrs)
- [Roadmap](#roadmap)
- [Design da UI (Figma)](#design-da-ui-figma)
- [Convenções](#convenções)
- [Contribuindo](#contribuindo)
- [Licença](#licença)

---

## Sobre

O **PasswordManager** é um gerenciador de senhas **100% local** para Windows. O cofre inteiro é serializado e armazenado como **um único blob criptografado** em SQLite — nenhum título, URL ou nome de pasta fica legível sem a senha mestra. A aplicação roda offline, com auto-lock por inatividade, gerador de senhas sem viés, avaliação de força, tema claro/escuro/sistema, i18n (pt-BR/en-US) e backup criptografado no formato proprietário `.vault`. Distribuição via **MSI (WiX Toolset 5)** — app **unpackaged** (`WindowsPackageType=None`).

Objetivos de engenharia demonstrados no projeto:

- **Clean Architecture** com separação estrita Domain / Application / Infrastructure / Presentation / UI
- **DDD** — `Vault` como agregado raiz, invariantes centralizadas
- **Criptografia aplicada** — Argon2id + AES-256-GCM com parâmetros auditáveis
- **Persistência segura** — SQLite/EF Core com blob único (ADR 0003)
- **Testes automatizados** — 247 testes (xUnit + FluentAssertions), incluindo 58 de ViewModels desacoplados
- **CI/CD** — GitHub Actions em `windows-latest` com build, testes e geração de MSI

---

## Funcionalidades

| Área | Detalhe |
|------|---------|
| **Cofre** | Criar, desbloquear e trancar cofre com senha mestra |
| **Itens** | CRUD completo de credenciais (título, usuário, senha, URL, notas, categoria, pasta) |
| **Pastas** | Criar, renomear e remover pastas; remover pasta não apaga itens (limpa `FolderId`) |
| **Busca** | Filtro em memória por título, usuário, URL, notas e categoria |
| **Filtro por pasta** | Listagem filtrada com opção "Todas as pastas" |
| **Gerador de senhas** | `RandomNumberGenerator` sem viés de módulo, tamanho e conjuntos configuráveis |
| **Força da senha** | Avaliação em `ForcaSenha` (enum) exibida na UI |
| **Copiar senha** | Cópia para área de transferência com limpeza automática configurável |
| **Auto-lock** | Trancamento por inatividade via `ITimer`/`DispatcherQueueTimer` (padrão 2 min, configurável) |
| **Troca de senha mestra** | Exige senha atual (verificação em tempo constante), rotaciona salt + blob |
| **Backup .vault** | Exportar/importar arquivo autocontido criptografado, com opção substituir ou mesclar |
| **Tema** | Claro / escuro / sistema (`AppSettings.Tema`, `App.AplicarTema` + `DesignTokens.xaml`) |
| **Idioma** | pt-BR / en-US (PRI + `Resources.resw`), detecção automática do SO |
| **Configurações** | Persistidas em `settings.json` fora do cofre (sem segredos) |

> Pastas por nome case-insensitive no merge; itens deduplicados por título+usuário; novos GUIDs no merge para preservar invariantes do agregado.

---

## Arquitetura

### Clean Architecture

```
┌─────────────────────────────────────────────────┐
│  UI (WinUI 3 + CommunityToolkit.Mvvm + DI)      │  Views, Dialogs, Services reais
├─────────────────────────────────────────────────┤
│  Presentation (net8.0)                           │  ViewModels + Abstrações (IClipboardService,
│                                                  │  ITimer/ITimerFactory, IIdiomaProvider)
├─────────────────────────────────────────────────┤
│  Infrastructure                                  │  CryptoService, VaultRepository (EF Core),
│                                                  │  ExportImportService, AppSettingsService
├─────────────────────────────────────────────────┤
│  Application                                     │  IVaultSessionService, IVaultRepository,
│                                                  │  ICryptoService, PasswordGenerator,
│                                                  │  AppSettings, DTOs, Exceptions
├─────────────────────────────────────────────────┤
│  Domain (sem dependências)                       │  Vault (agregado raiz), VaultItem,
│                                                  │  VaultFolder, invariantes, Rehydrate
└─────────────────────────────────────────────────┘
```

**Princípios-chave:**

- **Agregado raiz** — `Vault` é o único ponto de mutação (`AddItem`, `RemoveItem`, `UpdateItem`, `AddFolder`, `RemoveFolder`, `RenameFolder`, `AssignItemToFolder`, `MergeFrom`). Não existe `IVaultItemRepository`. Coleções internas `_items`/`_folders` são privadas e expostas como `IReadOnlyCollection`. Ver `docs/adr/0001-vault-como-agregado-raiz.md`.
- **Blob único criptografado** — `VaultRepository` serializa o `Vault` inteiro para JSON, criptografa com AES-256-GCM e persiste em **uma única linha** no SQLite. Busca/filtro ocorrem em memória após descriptografar. Ver `docs/adr/0003-armazenamento-do-cofre-como-blob-unico-criptografado.md`.
- **Sessão centralizada** — `VaultSessionService` retém **apenas a chave derivada** em memória (nunca a senha), deriva via `ICryptoService` com o salt persistido e zera com `CryptographicOperations.ZeroMemory` no `Lock()`. Todo CRUD faz auto-save.
- **Desacoplamento Presentation/UI** — ViewModels em `PasswordManager.Presentation` (`net8.0`, sem WinUI) dependem de abstrações (`IClipboardService`, `ITimer`, `IIdiomaProvider`, `ILocalizationService`); `PasswordManager.UI` fornece as implementações reais (`ClipboardService`, `DispatcherQueueTimerAdapter`, `ApplicationLanguagesProvider`). Testes em `PasswordManager.UI.Tests` usam fakes.
- **Desacoplamento de I/O** — `IExportImportService` opera sobre `byte[]`; file pickers ficam na UI, mantendo Application/Infrastructure testáveis.

### Fluxo de Persistência

```
Criar/Desbloquear  →  DeriveKey(senha, salt)  →  Vault em memória (sessão)
Salvar (auto)      →  Serialize(Vault) → Encrypt(chave) → SQLite (1 linha)
Carregar           →  SQLite → Decrypt(chave) → Deserialize → Vault
Export .vault      →  Serialize → novo salt → Encrypt(senha re-digitada) → PMVT|ver|salt|pacote
```

### Diagrama de Dependências

```
Domain ──< Application ──< Infrastructure
              ^               |
              └─ Presentation └─ UI
```

---

## Segurança e Criptografia

> Leia `docs/adr/0004-criptografia-argon2id-e-aes-256-gcm.md` antes de alterar qualquer parâmetro.

| Camada | Algoritmo | Parâmetros |
|--------|-----------|------------|
| **Derivação de chave** | **Argon2id** (`Konscious.Security.Cryptography.Argon2`) | 64 MiB · 3 iterações · paralelismo 4 · salt 16 bytes · saída 32 bytes (AES-256) |
| **Cifra autenticada** | **AES-256-GCM** (`System.Security.Cryptography.AesGcm`) | nonce 12 bytes · tag 16 bytes · pacote `nonce \|\| tag \|\| ciphertext` |
| **Integridade** | Tag GCM cobre o cofre inteiro | Falha de tag lança `CryptographicIntegrityException` — senha errada e dado adulterado são **indistinguíveis por design** |
| **Higiene de memória** | `CryptographicOperations.ZeroMemory` no `Lock()` | Chave derivada zerada ao trancar |

**Notas importantes:**

- Parâmetros do Argon2id são **injetáveis via construtor** do `CryptoService` para testes rápidos (ex.: 32 KB / 1 iteração). **Nunca enfraquecer em produção.**
- `Guid.Empty` nunca é usado como chave persistida — `Microsoft.Data.Sqlite` faz bind de `Guid.Empty` como BLOB enquanto a coluna é TEXT, causando `WHERE` silenciosamente vazio. O repositório usa um `SingletonRecordId` fixo não-nulo.
- Alterar parâmetros ou layout do pacote quebra a leitura de cofres existentes — exige bump de `SchemaVersion` e migração.

---

## Stack

| Camada | Tecnologia | Versão / Obs |
|--------|------------|--------------|
| Linguagem / Runtime | C# / .NET | 8.0 (`net8.0`, SDK 10.0.302 via `global.json`, `rollForward: latestFeature`) |
| UI | WinUI 3 + Windows App SDK | 2.3.1 |
| MVVM | CommunityToolkit.Mvvm | 8.3.2 |
| DI | Microsoft.Extensions.DependencyInjection | 8.0.1 |
| Persistência | EF Core + SQLite | 8.0.30 (`dotnet-ef` 8.0.30 via `.config/dotnet-tools.json`) |
| Criptografia | Konscious.Security.Cryptography.Argon2 | 1.3.1 |
| Testes | xUnit + FluentAssertions | 247 testes |
| Logs | Serilog | — |
| Distribuição | WiX Toolset | 5.0.2 (MSI) |
| CI | GitHub Actions (`windows-latest`) | build + testes + MSI |

---

## Estrutura do Projeto

```
password-manager/
├── PasswordManager.slnx              # Solution principal (Domain + App + Infra + Presentation + UI + 4 testes)
├── src/PasswordManager.UI.slnx       # Solution só-UI (unpackaged, MSI — WindowsPackageType=None)
├── global.json                       # SDK 10.0.302, rollForward latestFeature (projetos em net8.0)
├── installer/
│   ├── Package.wxs                   # Definição MSI (WiX 5, UpgradeCode estável, MajorUpgrade)
│   └── generate-AppFiles.ps1         # Gera AppFiles.wxs a partir do publish self-contained
├── docs/
│   ├── adr/                          # 0001..0007 — decisões de arquitetura
│   ├── design/
│   │   ├── design-plan.md            # Plano do redesign Figma → WinUI 3
│   │   └── figma-snapshot.md         # Snapshot regenerável (fonte de verdade)
│   └── plans/06-testes-viewmodels.md # Plano Fase B — concluído
├── scripts/
│   └── fetch-figma-design.ps1        # Regenera o snapshot via Figma REST API
├── src/
│   ├── PasswordManager.Domain/       # Entidades, invariantes, Rehydrate (InternalsVisibleTo)
│   ├── PasswordManager.Application/  # Interfaces, VaultSessionService, PasswordGeneration, Settings
│   ├── PasswordManager.Infrastructure/ # CryptoService, VaultRepository, ExportImportService, AppSettingsService
│   ├── PasswordManager.Presentation/ # ViewModels + Abstrações (net8.0, sem WinUI — testável)
│   └── PasswordManager.UI/           # WinUI 3: Views, Services reais, DI, Localization, Strings/
│       ├── Views/                    # UnlockPage, VaultPage, ItemEditorContent, GerenciarPastasContent, SettingsContent
│       ├── ViewModels/               # (legado — ViewModels migrados para Presentation)
│       ├── Services/                 # ClipboardService, DispatcherQueueTimerAdapter, ApplicationLanguagesProvider
│       ├── Localization/             # ILocalizationService / LocalizationService
│       └── Strings/{pt-BR,en-US}/Resources.resw
└── tests/
    ├── PasswordManager.Domain.Tests/         # 57 testes
    ├── PasswordManager.Application.Tests/    # 66 testes (com fakes)
    ├── PasswordManager.Infrastructure.Tests/ # 66 testes (inclui VaultDbContextMigrationTests)
    └── PasswordManager.UI.Tests/             # 58 testes (Vault 23 + Settings 10 + ItemEditor 10 + Unlock 12 + 3 smoke)
```

**Persistência em disco (Windows):**

- Cofre: `%LocalAppData%\PasswordManager\vault.db` (via `VaultDatabaseMigrator.ApplyMigrations`: migration `InitialCreate` + baseline automático para bancos legados criados com `EnsureCreated`)
- Configurações: `%LocalAppData%\PasswordManager\settings.json`

---

## Pré-requisitos

- **Windows 10 17763+ / Windows 11** (exigido pelo WinUI 3)
- **.NET SDK 10.0.302** (instalado automaticamente pelo `setup-dotnet` lendo `global.json`; o SDK 10 compila projetos `net8.0` sem alteração de TFM)
- **Runtime .NET 8.0.x** (instalado no CI e necessário para executar os testes)
- **Visual Studio 2022 17.8+** ou **VS Code** com workload de desenvolvimento Windows (para build da UI com `Microsoft.WindowsAppSDK` e `Microsoft.Windows.SDK.BuildTools`)
- Para gerar MSI localmente: **WiX Toolset 5.0.2** (`dotnet tool install --global wix --version 5.0.2`)
- Para regenerar o snapshot do Figma: variável de ambiente `FIGMA_PERSONAL_ACCESS_TOKEN` (nunca commitar)

---

## Instalação e Execução

### Via código (desenvolvimento)

```powershell
# 1. Clonar
git clone https://github.com/DaviVasconcelos/password-manager.git
Set-Location password-manager

# 2. Compilar a solution principal (Domain + Application + Infrastructure + Presentation + UI + testes)
#    Requer -p:Platform=x64 por causa do PasswordManager.UI unpackaged
dotnet build PasswordManager.slnx -p:Platform=x64

# 3. Executar os testes (247 testes)
dotnet test PasswordManager.slnx -p:Platform=x64

# 4. Executar a UI — abrir src/PasswordManager.UI.slnx no Visual Studio
#    e pressionar F5 (x64). Não é executável headless via CLI.
```

> A solution é `.slnx` (formato novo), não `.sln`. O `global.json` fixa o SDK em `10.0.302` (`rollForward: latestFeature`) — não "corrija" o TFM para `net10.0`.

**Projeto de teste isolado:**

```powershell
dotnet test tests/PasswordManager.Domain.Tests/PasswordManager.Domain.Tests.csproj
dotnet test tests/PasswordManager.Application.Tests/PasswordManager.Application.Tests.csproj
dotnet test tests/PasswordManager.Infrastructure.Tests/PasswordManager.Infrastructure.Tests.csproj
dotnet test tests/PasswordManager.UI.Tests/PasswordManager.UI.Tests.csproj -p:Platform=x64
```

---

## Distribuição (MSI)

O app é **unpackaged** (`WindowsPackageType=None`, `EnableMsixTooling=false`, `PublishTrimmed=false` — EF Core não é trimming-safe). A distribuição é via **MSI next-next** com WiX Toolset 5.

```powershell
# Gerar MSI localmente (Release, self-contained win-x64)
dotnet publish src/PasswordManager.UI -c Release -p:Platform=x64 -r win-x64 --self-contained -o publish
powershell -ExecutionPolicy Bypass -File installer/generate-AppFiles.ps1 -PublishDir publish -Output installer/AppFiles.wxs
wix build -arch x64 -d PublishDir=publish -o PasswordManager-0.1.0-x64.msi installer/Package.wxs installer/AppFiles.wxs
```

- `installer/Package.wxs` — `Version="0.1.0"` (sincronize com git tag), `UpgradeCode` estável (não altere), `MajorUpgrade` para upgrades, atalhos no Menu Iniciar e Área de Trabalho.
- `installer/AppFiles.wxs` — gerado dinamicamente, `gitignored`.
- Saída no CI: `PasswordManager-0.1.0-x64.msi` publicado como artefato (30 dias, **unsigned**). Instalação silenciosa: `msiexec /i PasswordManager-0.1.0-x64.msi /qn`.

> Histórico: o projeto cogitou MSIX, mas a decisão final é **MSI puro** para manter o app unpackaged e simplificar distribuição/CI. Nunca use `WindowsPackageType=MSIX`.

---

## Como Usar

1. **Primeira execução** — informe a senha mestra e confirme para criar o cofre. Alternativamente, use **Importar backup...** para restaurar um `.vault`.
2. **Desbloquear** — informe a senha mestra. Falha de autenticação resulta em `CryptographicIntegrityException` (mensagem genérica por segurança).
3. **Vault** — crie pastas, adicione itens (título/usuário/senha/URL/notas/categoria/pasta), use a busca e o filtro por pasta.
4. **Copiar senha** — copia para a área de transferência e agenda limpeza automática (tempo configurável).
5. **Gerar senha** — no diálogo de item, ajuste tamanho e conjuntos (maiúsculas, minúsculas, números, símbolos) e veja a força em tempo real.
6. **Trancar** — manual pelo botão ou automático por inatividade.
7. **Trocar senha mestra** — exige senha atual + nova senha + confirmação; rotaciona salt e re-criptografa o blob.
8. **Tema e idioma** — em **Configurações** (tema claro/escuro/sistema, idioma pt-BR/en-US/auto); tema aplica ao vivo, idioma requer reinício.
9. **Exportar/Importar** — gere um `.vault` com a senha re-digitada; na importação escolha **Substituir** ou **Mesclar**. Import com sessão trancada só é permitido quando ainda não existe cofre local.

---

## Configurações

Acessíveis pelo diálogo **Configurações** na `VaultPage`:

| Configuração | Padrão | Descrição |
|--------------|--------|-----------|
| Timeout de auto-lock | 2 minutos | Tranca por inatividade (não por perda de foco). `ITimer` reiniciado a cada `Pointer`/`Key` |
| Tempo de limpeza do clipboard | 30 s | Zera a área de transferência após copiar senha |
| Tamanho padrão do gerador | 20 | Comprimento da senha gerada (8–64) |
| Conjuntos do gerador | todos ativos | Incluir maiúsculas / minúsculas / números / símbolos |
| Tema | sistema | `sistema` / `claro` / `escuro` — aplica via `App.AplicarTema` / `RequestedTheme` |
| Idioma | auto | `auto` (segue SO) / `pt-BR` / `en-US` — usa `ApplicationLanguages` + PRI |

Persistência via `IAppSettingsService` + `AppSettingsService` em `%LocalAppData%\PasswordManager\settings.json` (JSON simples, sem segredos). Validação em `AppSettings.Validar()`.

---

## Internacionalização (i18n)

Implementada conforme **ADR 0007**:

- **Formato:** PRI + `Strings/<lang>/Resources.resw` (`pt-BR` baseline, `en-US` espelhado) com `x:Uid` no XAML.
- **Fallback dependente do SO:** `DefaultLanguage=en-US` no csproj; `App.AplicarIdiomaPreferencial()` lê `ApplicationLanguages.Languages[0]` — se for `pt-BR` usa `pt-BR`, qualquer outro idioma cai em `en-US`.
- **Testabilidade:** `ILocalizationService` / `LocalizationService` (`GetString` com `string.Format`) injetado via DI nos ViewModels; `ResourceLoader.GetForViewIndependentUse("Resources")` com compatibilidade `.` ↔ `/`.
- **Escopo:** apenas UI. Mensagens de Domain/Application/Infrastructure permanecem em **pt-BR** por convenção.
- **Novo idioma:** criar `Strings/<lang>/Resources.resw` com as mesmas chaves (~120 chaves no baseline).

### Como adicionar um novo idioma

> Contribuidores não precisam alterar C# para adicionar um idioma — basta traduzir o `.resw`. A lista de idiomas é descoberta em runtime via `ManifestLanguages` (PRI).

1. **Crie a pasta** `src/PasswordManager.UI/Strings/<lang>/` onde `<lang>` é o BCP-47 (ex: `es-ES`, `fr-FR`, `de-DE`).
2. **Copie** `src/PasswordManager.UI/Strings/en-US/Resources.resw` para a nova pasta.
3. **Traduza** apenas o conteúdo de `<value>...</value>`, mantendo as chaves (`<data name="...">`) e os placeholders `{0}`, `{1}` intactos:
   ```xml
   <data name="VaultPage_BtnNovoItem.Content" xml:space="preserve"><value>+ Nuevo ítem</value></data>
   ```
4. **Não altere** `AppSettings` nem `SettingsViewModel` — a validação aceita qualquer `CultureInfo` válido e o `ComboBox` de idioma em **Configurações** lista automaticamente todos os idiomas do PRI.
5. **Teste** localmente:
   ```powershell
   dotnet build PasswordManager.slnx -p:Platform=x64
   # Rode a UI, vá em Configurações → Idioma → selecione o novo idioma → Salvar → Reiniciar agora
   # A opção "Automático (sistema)" usa o idioma do SO com fallback para en-US.
   ```
6. **Abra o PR** com o novo `.resw`. O CI compila o `resources.pri` e valida `dotnet build`/`dotnet test`.

Dicas:
- Use `pt-BR` como referência para termos técnicos e `en-US` para placeholders.
- Ferramentas como [ResX Resource Manager](https://github.com/dotnet/ResXResourceManager) ajudam a visualizar chaves faltantes.
- Se quiser que o nome do idioma apareça localizado no ComboBox, adicione `Settings_Idioma_Opcao_<Lang>` (ex: `Settings_Idioma_Opcao_EsES`) nos dois baselines; caso contrário o nome nativo do `CultureInfo` será usado.

---

## Exportação e Importação (.vault)

Formato autocontido definido no **ADR 0005**:

```
[magic "PMVT" (4 bytes)] [versão (1 byte)] [salt Argon2id (16 bytes)] [pacote AES-256-GCM]
                                     └─ nonce(12) + tag(16) + ciphertext
```

- Reusa `VaultDataMapper` + `ICryptoService` (mesma serialização da persistência local).
- Salt **novo a cada exportação** — arquivo independente do salt local.
- Chave derivada da **senha mestra re-digitada** na exportação (evita confusão de "senha de exportação").
- `IExportImportService` opera sobre `byte[]`; I/O de arquivo é responsabilidade da UI.
- **Import desbloqueado:** escolher **Substituir** (troca o cofre) ou **Mesclar** (`Vault.MergeFrom` — pastas por nome case-insensitive, itens deduplicados por título+usuário, novos GUIDs).
- **Import trancado:** permitido apenas quando **não existe cofre local** (restauração inicial).
- **Sem CSV nesta etapa** — deliberadamente adiado (texto plano expõe senhas).

---

## Testes

```powershell
# Todos os testes (247) — exige -p:Platform=x64 por causa da UI
dotnet build PasswordManager.slnx -p:Platform=x64 --configuration Debug
dotnet test PasswordManager.slnx -p:Platform=x64 --configuration Debug --no-build --logger trx --results-directory TestResults

# Cobertura por projeto
dotnet test tests/PasswordManager.Domain.Tests/PasswordManager.Domain.Tests.csproj
dotnet test tests/PasswordManager.Application.Tests/PasswordManager.Application.Tests.csproj
dotnet test tests/PasswordManager.Infrastructure.Tests/PasswordManager.Infrastructure.Tests.csproj
dotnet test tests/PasswordManager.UI.Tests/PasswordManager.UI.Tests.csproj -p:Platform=x64
```

| Projeto | Testes | Framework |
|---------|--------|-----------|
| `PasswordManager.Domain.Tests` | 57 | xUnit + FluentAssertions |
| `PasswordManager.Application.Tests` | 66 | xUnit + FluentAssertions + fakes |
| `PasswordManager.Infrastructure.Tests` | 66 | xUnit + FluentAssertions |
| `PasswordManager.UI.Tests` | 58 | xUnit + FluentAssertions + fakes (`FakeTimer`, `FakeClipboard`, etc.) |
| **Total** | **247** | Todos passando em `net8.0` (`-p:Platform=x64` no Windows) |

Convenção de nomes: `Metodo_Cenario_ResultadoEsperado` em pt-BR (ex.: `RemoveItem_ComIdInexistente_DeveLancarExcecao`). ViewModels testáveis via `PasswordManager.Presentation` (`net8.0` sem WinUI) — ver `docs/plans/06-testes-viewmodels.md`.

---

## CI/CD

Workflow em `.github/workflows/ci.yml` (ADR 0006) — **MSI, não MSIX**:

- **Runner:** `windows-latest` (exigido pela UI — WinUI 3 + `Microsoft.Windows.SDK.BuildTools` + mapeamento x86/x64/ARM64)
- **Gatilhos:** `push` em qualquer branch e `pull_request`
- **Jobs:**
  - `build-and-test`:
    1. `actions/setup-dotnet` lendo `global.json` (SDK 10.0.302)
    2. `actions/setup-dotnet` com `8.0.x` para runtime dos testes
    3. `dotnet tool restore` (instala `dotnet-ef` 8.0.30)
    4. `dotnet ef migrations has-pending-model-changes --project src/PasswordManager.Infrastructure`
    5. `dotnet build PasswordManager.slnx --configuration Debug -p:Platform=x64`
    6. `dotnet test PasswordManager.slnx --configuration Debug --no-build -p:Platform=x64 --logger trx` (247 testes)
    7. `actions/upload-artifact` com `TestResults/**/*.trx` (14 dias)
  - `build-msi` (depende de `build-and-test`):
    1. `dotnet publish src/PasswordManager.UI -c Release -p:Platform=x64 -r win-x64 --self-contained -o publish`
    2. `wix build -arch x64 -d PublishDir=publish -o PasswordManager-0.1.0-x64.msi installer/Package.wxs installer/AppFiles.wxs` (WiX 5.0.2, `generate-AppFiles.ps1`)
    3. `actions/upload-artifact` com `PasswordManager-*.msi` (30 dias, **unsigned / next-next**)
- **Fora do escopo atual:** assinatura de código/certificado, versionamento via git tag, auto-update e publicação em store/winget (Fase D)

---

## Decisões de Arquitetura (ADRs)

| ADR | Título | Status |
|-----|--------|--------|
| [0001](docs/adr/0001-vault-como-agregado-raiz.md) | Vault como agregado raiz, VaultItem sem repositório próprio | Aceito |
| [0002](docs/adr/0002-pastas-como-entidade-filha-opcional.md) | Pastas como entidade filha opcional | Aceito |
| [0003](docs/adr/0003-armazenamento-do-cofre-como-blob-unico-criptografado.md) | Armazenamento do cofre como blob único criptografado (SQLite) | Aceito |
| [0004](docs/adr/0004-criptografia-argon2id-e-aes-256-gcm.md) | Criptografia — Argon2id + AES-256-GCM | Aceito |
| [0005](docs/adr/0005-export-import-do-cofre-em-formato-vault.md) | Export/Import do cofre em formato `.vault` | Aceito |
| [0006](docs/adr/0006-integracao-continua-com-github-actions.md) | Integração contínua com GitHub Actions + MSI (WiX) | Aceito (MSI, não MSIX) |
| [0007](docs/adr/0007-internacionalizacao-com-resources-resw.md) | Internacionalização da UI com `Resources.resw` | Aceito |

> Leia `docs/adr/*` antes de tocar em Domain, persistência ou criptografia.

---

## Roadmap

> **Verificado no código em 2026-09-01** — Fase A e B 100% entregues (247 testes). Distribuição MSI já implementada no CI (sem assinatura).

### ✅ Concluído — Fase A (Robustez/UX) e Fase B (Engenharia)

Arquivado como histórico — não faz mais parte do roadmap ativo. Todos os itens abaixo estão implementados e cobertos por testes:

- **Fase A — Robustez/UX:** Configurações (`IAppSettingsService`/`AppSettingsService` + `settings.json` + `SettingsViewModel`/`SettingsContent`), Auto-lock por inatividade (2 min padrão, `ITimer`/`DispatcherQueueTimerAdapter`), Troca de senha mestra (exige senha atual, `CryptographicIntegrityException`), Tema claro/escuro/sistema (`AppSettings.Tema`, `App.AplicarTema`, `DesignTokens.xaml`)
- **Fase B — Engenharia:** Migrations EF Core (`dotnet-ef 8.0.30`, `VaultDatabaseMigrator.ApplyMigrations` + baseline legado, `has-pending-model-changes` no CI), Testes de ViewModels (58 UI tests desacoplados via `IClipboardService`/`ITimer`/`IIdiomaProvider`, `PasswordManager.Presentation` + `PasswordManager.UI.Tests`), i18n (ADR 0007 — PRI + `Strings/<lang>/Resources.resw`, `ILocalizationService`, fallback `pt-BR`/`en-US`)

Detalhes: `AGENTS.md` (seção Roadmap) + `docs/plans/06-testes-viewmodels.md`.

### 🔜 Em aberto

#### Fase C — Features de produto

- [ ] **Import CSV** (Bitwarden/LastPass/1Password) — **ADIADO**, ADR 0005 segue sem CSV (texto plano expõe senhas)
- [ ] **TOTP/2FA** — secret criptografado no item, geração de 6 dígitos na UI (QR futuro)
- [ ] **Favoritos / tags / health check** — força, reuso, expiração

#### Fase D — Distribuição

- [x] **Empacotamento MSI (WiX 5)** — `installer/Package.wxs` + `installer/generate-AppFiles.ps1`, publish self-contained `win-x64`, MSI `PasswordManager-0.1.0-x64.msi` gerado no CI (`build-msi`, `wix 5.0.2`) como artefato (30 dias, **sem assinatura**). App **unpackaged** (`WindowsPackageType=None`, `EnableMsixTooling=false`, `PublishTrimmed=false`). Próximos passos: assinatura/certificado e versionamento via git tag.
- [ ] **Auto-update / backup automático / lembretes de backup**

---

## Design da UI (Figma)

O redesign está documentado em `docs/design/design-plan.md` (tokens, componentes, mapeamento tela a tela). A fonte de verdade para implementação é `docs/design/figma-snapshot.md` (regenerável).

Para atualizar o snapshot após mudanças no Figma:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/fetch-figma-design.ps1
```

Requer `FIGMA_PERSONAL_ACCESS_TOKEN` no ambiente (header `X-Figma-Token`). O JSON cru (`figma-snapshot.json`, 10+ MB) é gitignored — apenas o markdown é versionado.

FileKey do Figma: `IfOF27YvqWa67OoDvhcrWD`.

---

## Convenções

- **Idioma:** comentários, mensagens de exceção, nomes de testes e docs em **pt-BR**.
- **Exceção de integridade:** `CryptographicIntegrityException` vive em `Application/Abstractions/CryptographicIntegrityException.cs` mas no namespace `PasswordManager.Application.Exceptions`.
- **Entidades:** construtor privado sem parâmetros + factories estáticas, chaves `Guid`, `DateTime.UtcNow`, setters privados, `Rehydrate` interno para persistência (`InternalsVisibleTo`).
- **Testes:** `xUnit` + `FluentAssertions`, padrão `Metodo_Cenario_ResultadoEsperado` em pt-BR.
- **Distribuição:** **MSI (WiX 5)** via `installer/Package.wxs` (nunca MSIX). `WindowsPackageType=None`, `EnableMsixTooling=false`, `PublishTrimmed=false`. `UpgradeCode` em `Package.wxs` é estável — não altere. Version em `Package/@Version` deve ser sincronizada com git tag.
- **Commits/PRs:** mensagens objetivas, sem segredos; CI deve permanecer verde (`-p:Platform=x64` obrigatório para UI).

---

## Contribuindo

1. Abra uma issue descrevendo a mudança (inclua ADR se afetar Domain/persistência/criptografia).
2. Crie um branch a partir de `main`.
3. Garanta `dotnet build PasswordManager.slnx -p:Platform=x64` e `dotnet test PasswordManager.slnx -p:Platform=x64` passando (247 testes).
4. Abra um PR — o CI roda build + testes + MSI em `windows-latest` automaticamente.

---

## Licença

[MIT](LICENSE) — Copyright (c) 2026 Davi Vasconcelos.

---

<p align="center">
  Feito com foco em segurança, simplicidade e código bem testado.
</p>
