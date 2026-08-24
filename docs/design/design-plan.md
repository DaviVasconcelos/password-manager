# Plano de Implementação — Redesign da UI (Figma → WinUI 3)

> **Status**: EM IMPLEMENTAÇÃO. Design aprovado e snapshot salvo.
> Fonte: arquivo `PasswordManager` no Figma (fileKey `IfOF27YvqWa67OoDvhcrWD`).

## Decisões tomadas (2026-08-24)

1. **Barra lateral da VaultPage**: `NavigationView` nativo. Pastas/Configurações abrem diálogos e a seleção volta para "Itens"; Trancar fica no rodapé do pane.
2. **Renomear pasta**: mini-diálogo com TextBox (aberto pelo ícone de lápis na linha).
3. **Força de senha "Média"**: laranja `#F8A800` (claro) / `#FFB900` (escuro) — token `PMWarningBrush` adicionado ao design system.
4. **Toast "senha copiada"**: `InfoBar` nativo (Severity=Success) em vez de banner custom.
5. **Excluir item/pasta** ganha diálogo de confirmação (ações inline facilitam clique acidental).
6. **Tema**: tokens via `ThemeDictionaries` seguem o tema do SO; toggle manual continua adiado (roadmap item 4).
7. **Modo criar/desbloquear na UnlockPage**: o subtítulo fixo "Seu cofre seguro" é mantido, e o modo (`TituloModo`) aparece como rótulo acima dos campos — preserva a função existente sem quebrar o visual.

## Como atualizar o snapshot quando o design mudar

O snapshot regenerável em `docs/design/figma-snapshot.md` é a fonte de verdade do design
para implementação. Se o design mudar no Figma:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/fetch-figma-design.ps1
```

O script lê o token da variável de ambiente `FIGMA_PERSONAL_ACCESS_TOKEN` (nunca está no
repositório). Depois de regenerar, comparar com o `git diff` do `figma-snapshot.md` para
identificar o que mudou.

## Design tokens → ResourceDictionary

Criar `src/PasswordManager.UI/Themes/DesignTokens.xaml` com `ResourceDictionary` (light +
dark) e mesclar no `App.xaml`. Nomes dos tokens seguem o frame `tokens` do Figma.

| Token | Claro | Escuro |
|---|---|---|
| `PMAccentBrush` | `#0078D4` | `#60CDFF` |
| `PMSurfaceBrush` | `#FFFFFF` | `#202020` |
| `PMSurfaceAltBrush` | `#F3F3F3` | `#2D2D2D` |
| `PMTextPrimaryBrush` | `#1A1A1A` | `#FFFFFF` |
| `PMTextSecondaryBrush` | `#616161` | `#9E9E9E` |
| `PMTextOnAccentBrush` | `#FFFFFF` | `#000000` |
| `PMBorderSubtleBrush` | `#E0E0E0` | `#3D3D3D` |
| `PMDangerBrush` | `#D13438` | `#FF6767` |
| `PMSuccessBrush` | `#0F7B0F` | `#6CCB5F` |
| `PMBackgroundBrush` | `#F3F3F3` | `#1C1C1C` |

Tipografia (frame `tipografia`): Titulo 28px Semibold · Subtitulo 20px Semibold ·
Corpo 14px Regular · Caption 12px Regular. Usar `Segoe UI Variable`.

Espaçamento (frame `espacamentos`): múltiplos de 4 (4/8/12/16/24).

## Componentes reutilizáveis

Criar `src/PasswordManager.UI/Styles/`:
- `FluentButton.xaml` — estilos `PMButtonPrimary` e `PMButtonSecondary` (14px Semibold).
- `FluentTextBoxStyle` / `FluentPasswordBoxStyle` — campos com fundo `SurfaceAlt`,
  placeholder `TextSecondary`, raio 4px.
- `CategoryBadge` (novo) — pill `#E5E5E5` com texto 11px Regular.
- `FluentToggleStyle` — ToggleSwitch Fluent 2.

## Mapeamento tela por tela

### 1. UnlockPage (`Views/UnlockPage.xaml` + `ViewModels/UnlockViewModel.cs`)
- Card centralizado (~400px) com fundo `Surface`, borda `BorderSubtle`, raio 8px.
- Logo shield (accent) + título `PasswordManager` (Titulo) + subtítulo "Seu cofre seguro"
  (Subtitulo, `TextSecondary`).
- 2 campos senha (`SenhaMestraBox`, `ConfirmacaoBox`) — confirmar só no modo criar.
- Mensagem de erro com ícone alert-circle, `Danger`.
- Botões: primário "Criar cofre"/"Desbloquear"; secundário "Importar backup...".
- `ProgressRing` centralizado quando ocupado.

### 2. VaultPage (`Views/VaultPage.xaml` + `ViewModels/VaultViewModel.cs`)
- **NavigationView lateral** (novo): Itens, Pastas, Configurações, Trancar (bottom).
  Itens com ícone + label 14px; item ativo com pill accent.
  - "Pastas" e "Configurações" passam a ser navegação (abrem os mesmos diálogos atuais).
  - "Trancar" mantém `LockCommand`.
- **Top controls**: busca (placeholder "Buscar por título, usuário, URL, notas ou
  categoria..."), ComboBox de pasta ("Todas as pastas"), botão primário "+ Novo item".
- **Header actions**: Exportar, Importar, Trocar senha mestra (botões secundários).
- **Tabela**: header `Título | Usuário | Categoria` (14px Semibold, fundo `SurfaceAlt`);
  linhas com:
  - col Título (14px Semibold + ícone user-lock),
  - col Usuário (`TextSecondary`),
  - col Categoria = **badge pill** (novo, ex.: "Email", "Finanças"),
  - ações inline por linha (novo): editar, copiar, excluir (ícones).
- **Toast de sucesso** (novo): substituir o texto inline atual por banner verde
  `#D4EDDA` com texto `Success` ("Senha copiada! Área de transferência limpa em 30 s"),
  com ícone check-circle e botão fechar.
- Substituir `ListView` por `ItemsRepeater`/`ListView` com template novo (colunas + badge).

### 3. ItemEditorContent (`Views/ItemEditorContent.xaml`)
- Diálogo `ContentDialog` (fundo `Surface`, header 20px Semibold "Novo item").
- Campos com label 12px Medium `TextSecondary`: Título, Usuário, Senha (com botão
  "Gerar" + eye), URL, Notas.
- Linha Categoria + Pasta (ComboBox).
- Separador.
- **Gerador de senha** dentro do diálogo: slider Tamanho ("16 caracteres"), indicador de
  força ("Força da senha: Forte" com barra colorida), 4 toggles (Maiúsculas A-Z,
  Minúsculas a-z, Números 0-9, Símbolos @#$).
- Footer: Cancelar (secundário) + Salvar (primário).

### 4. GerenciarPastasContent (`Views/GerenciarPastasContent.xaml`)
- Diálogo header "Gerenciar pastas".
- Lista de pastas com ícone pasta + nome; ações editar/excluir por linha (ícones).
- Rodapé do diálogo: input "Nova pasta" + botão "Adicionar" + botão "Fechar".

### 5. SettingsContent (`Views/SettingsContent.xaml`)
- Diálogo header "Configurações".
- Seção **Segurança**: timeout de bloqueio automático (ComboBox, ex. "15 min"), tempo
  para limpar área de transferência (ComboBox, ex. "30 s").
- Separador.
- Seção **Gerador de senhas (padrões)**: slider "Tamanho padrão" ("16 caracteres"),
  4 toggles "Incluir maiúsculas/minúsculas/números/símbolos".
- Footer: Cancelar + Salvar.

## Ordem de implementação sugerida

1. `Themes/DesignTokens.xaml` + merge no `App.xaml` (base para tudo).
2. `Styles/` (botões, campos, badge, toggle).
3. `UnlockPage` (card + campos + erro).
4. `VaultPage` (NavigationView + tabela + badge + ações inline + toast).
5. `ItemEditorContent` (gerador + força + toggles).
6. `GerenciarPastasContent` e `SettingsContent`.

## Novidades vs. UI atual (resumo)

| Mudança | Arquivo afetado |
|---|---|
| Tema claro/escuro via tokens | `App.xaml`, todas as views |
| NavigationView lateral | `VaultPage.xaml` |
| Badge de categoria na tabela | `VaultPage.xaml` (novo template) |
| Ações inline por linha (editar/copiar/excluir) | `VaultPage.xaml` |
| Toast de "senha copiada" | `VaultPage.xaml` |
| Gerador de senha com força + toggles no diálogo | `ItemEditorContent.xaml` |
| ComboBox de timeout no Settings | `SettingsContent.xaml` |

## Acesso ao Figma (sem segredo no repo)

- Token: variável de ambiente `FIGMA_PERSONAL_ACCESS_TOKEN` (configurada no Windows,
  fora do git). Header correto da REST API: `X-Figma-Token` (não `Authorization: Bearer`).
- FileKey: `IfOF27YvqWa67OoDvhcrWD`.
- Link: `https://www.figma.com/design/IfOF27YvqWa67OoDvhcrWD/PasswordManager`