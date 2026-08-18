# PasswordManager

Gerenciador de senhas local desenvolvido em C#/.NET 8 utilizando Clean Architecture.

## Objetivo

Projeto para demonstrar conhecimentos em:

- Clean Architecture
- Criptografia aplicada
- EF Core
- WinUI 3
- Testes automatizados
- CI/CD
- Boas práticas de engenharia

## Stack

- .NET 8
- WinUI 3
- SQLite
- EF Core
- AES-256-GCM
- Argon2id
- xUnit
- FluentAssertions
- Serilog

## Estrutura

src/
tests/

## Roadmap

Concluído:

- [x] Domain
- [x] Testes
- [x] Infrastructure
- [x] UI
- [x] Export/Import (arquivo `.vault` criptografado; sem CSV nesta etapa)
- [x] CI/CD (GitHub Actions: build + testes em push/PR — ADR 0006)

Planejado (não implementado — ordem sugerida A → D):

**Fase A — Robustez/UX do núcleo**

- [x] Configurações (JSON local simples) + auto-lock por inatividade (padrão 2 min)
- [x] Trocar senha mestra na UI (com verificação da senha atual)
- [ ] Tema claro/escuro/sistema (adiado)

**Fase B — Engenharia**

- [ ] Migrations EF Core (no lugar de `EnsureCreated`)
- [ ] Testes de ViewModels (desacoplar de WinUI)
- [ ] Recursos/i18n

**Fase C — Features de produto**

- [ ] Import CSV (Bitwarden/LastPass/1Password) — adiado
- [ ] TOTP/2FA
- [ ] Favoritos / tags / health check de senhas

**Fase D — Distribuição**

- [ ] Empacotamento MSIX + assinatura + build da UI no CI
- [ ] Auto-update / backup automático / lembretes de backup

## Licença

MIT