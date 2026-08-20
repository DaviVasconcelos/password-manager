# 0007 - Internacionalização da UI com Resources.resw

## Status
Aceito

## Contexto
A UI estava 100% com textos hardcoded em `pt-BR` (`Views/*.xaml` com `Text="..."`/`Content="..."`/`PlaceholderText="..."` e `ViewModels`/`code-behind` com literais). Nenhum arquivo `.resw`, nenhum `x:Uid`, nenhum `ResourceLoader` existia. O `Package.appxmanifest` declarava apenas `<Resource Language="pt-br"/>` e o projeto não tinha `DefaultLanguage`.

O roadmap (Fase B, item 7) previa `Recursos/i18n` como pendente, sem decisão de implementação. Era necessário escolher a estratégia de localização nativa do WinUI 3 antes do redesign da UI, para evitar retrabalho.

Requisitos acordados:
- Idiomas iniciais: `pt-BR` e `en-US`.
- Regra de fallback dependente do SO: se o sistema estiver em `pt-BR` usa `pt-BR`, caso contrário `en-US`.
- Escopo apenas UI (Views + ViewModels + diálogos em code-behind). Mensagens de Domain/Application/Infrastructure continuam em `pt-BR` conforme convenção vigente (`AGENTS.md`).
- ViewModels devem permanecer testáveis (não acoplar diretamente a `ResourceLoader`).

## Decisão

### Formato e ferramenta
- Usar **PRI + `Strings/<lang>/Resources.resw`** (padrão WinUI 3 empacotado), com `x:Uid` no XAML.
  - `Strings/pt-BR/Resources.resw` como baseline (português existente).
  - `Strings/en-US/Resources.resw` espelhado e traduzido.
  - ~120 chaves no baseline, cobrindo `UnlockPage`, `VaultPage`, `ItemEditorContent`, `SettingsContent`, `GerenciarPastasContent` e `MainWindow`.
- `Package.appxmanifest` com `<Resource Language="x-generate"/>` (gera o PRI a partir das pastas `Strings/` automaticamente).
- `<DefaultLanguage>en-US</DefaultLanguage>` em `PasswordManager.UI.csproj` para que o fallback padrão seja `en-US` (qualquer idioma diferente de `pt-BR` cai em `en-US`).

### Convenção de chaves
- **XAML (`x:Uid`):** `{Pagina}_{Elemento}.{Propriedade}` — ex.: `UnlockPage_SenhaMestraBox.PlaceholderText`, `VaultPage_Header.Text`, `Settings_AutoLock.Text`, `GerenciarPastas_NomePasta.Header`.
- **Code-behind / ViewModels:** chaves planas quando não há propriedade XAML — ex.: `UnlockViewModel_TituloModo_Criar`, `VaultViewModel_TodasPastas`, `VaultPage_Erro_SenhaAtualIncorreta`, `VaultPage_Erro_FalhaExportar` (com placeholder `{0}` para `string.Format`).

### Abstração para testabilidade
- `ILocalizationService` (UI) + `LocalizationService` (implementação via `ResourceLoader.GetForViewIndependentUse("Resources")`).
  - `GetString(string key)` e `GetString(string key, params object[] args)` (formatação com `string.Format`).
  - Compatibilidade com separador `.` / `/`: tenta `key` e, se vazio, tenta `key.Replace('.', '/')` (x:Uid usa `.`, mapa de recursos usa `/`).
- Registrado como `Singleton` no DI (`App.ConfigureServices`) e injetado em `UnlockViewModel`, `ItemEditorViewModel`, `VaultViewModel` e nos code-behinds `UnlockPage`/`VaultPage` (via `App.Services.GetRequiredService<ILocalizationService>()`).
- Armazenar apenas chaves no código; interpoladas via placeholder (`Falha ao exportar: {0}` → `Failed to export: {0}`).

### Regra de idioma dependente do SO
- Em `App` (construtor), `AplicarIdiomaPreferencial()` lê `Windows.Globalization.ApplicationLanguages.Languages[0]` e, se não for `pt-BR` (case-insensitive), define `ApplicationLanguages.PrimaryLanguageOverride = "en-US"`.
- Isso garante: SO `pt-BR` → `pt-BR`; SO com qualquer outro idioma (`en-US`, `en-GB`, `fr-FR`, etc.) → `en-US`. `DefaultLanguage=en-US` já garante o fallback PRI correto para idiomas não suportados.

### XAML
- Todos os literais `Text`/`Content`/`PlaceholderText`/`Header` foram substituídos por `x:Uid`. Ex.: `<PasswordBox x:Uid="UnlockPage_SenhaMestraBox" />` consome `UnlockPage_SenhaMestraBox.PlaceholderText` do PRI.
- `TextBlock` com binding de ViewModel (`TituloModo`, `ForcaSenhaTexto`, `OpcoesPasta`) continuam via binding, mas o ViewModel resolve a string via `ILocalizationService`.

## Consequências

- **Positivas:**
  - UI 100% localizável sem recompilar lógica; adicionar novo idioma = nova pasta `Strings/<lang>/Resources.resw`.
  - ViewModels testáveis (mock de `ILocalizationService`).
  - Fallback determinístico (`pt-BR` só em SO `pt-BR`, resto `en-US`) sem depender de heurística do PRI.
  - Sem impacto em Domain/Application (mensagens continuam `pt-BR`).
- **Negativas / pontos de atenção:**
  - Novas strings na UI exigem entrada em ambos os `.resw` (pt-BR e en-US); esquecer uma chave resulta no fallback da chave literal (comportamento do `LocalizationService`).
  - `App.Services` usado nos code-behinds para resolver `ILocalizationService` (Page é criada via `Frame.Navigate`, não via DI).
  - `App.xaml.cs` usa `ResourceLoader` com fallback `try/catch` para a mensagem de provedor não inicializado (PRI pode não estar carregado em testes).
