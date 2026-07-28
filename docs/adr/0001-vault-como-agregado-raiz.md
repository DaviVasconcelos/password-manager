# ADR 0001: Vault como agregado raiz, VaultItem sem repositório próprio

## Status
Aceito

## Contexto
O cofre de senhas precisa garantir que todo `VaultItem` pertence a
exatamente um `Vault`, e que a coleção de itens não pode ser manipulada
por fora das regras de negócio (ex: adicionar item com dados inválidos,
remover item inexistente sem erro, ter itens "órfãos" sem vault).

Sendo um projeto solo, de portfólio, também é importante que a camada de
Domain fique simples de entender e testar isoladamente, sem depender de
Infrastructure ou Application.

## Decisão
`Vault` é modelado como o agregado raiz (padrão DDD). `VaultItem` só pode
ser criado, alterado ou removido através de métodos expostos pelo próprio
`Vault` (`AddItem`, `RemoveItem`), nunca diretamente.

Não existe `IVaultItemRepository`. Apenas `IVaultRepository`, que trabalha
com o agregado `Vault` completo (carrega e salva o cofre inteiro, não itens
individuais).

A coleção interna de itens (`_items`) é privada e exposta externamente como
`IReadOnlyCollection<VaultItem>`, impedindo modificação direta pela UI ou
por qualquer camada externa.

## Consequências

**Positivas:**
- Impossível existir `VaultItem` órfão ou lista de itens em estado
  inconsistente.
- Toda regra de negócio sobre itens fica centralizada no `Vault`, facilitando
  testes de domínio isolados.
- Reduz a superfície de interfaces na Application (uma interface de
  persistência em vez de duas).

**Negativas / trade-offs aceitos:**
- Para persistir a alteração de um único item, é necessário carregar e
  salvar o `Vault` inteiro. Aceitável dado o escopo do MVP (poucos itens
  esperados por usuário, sem requisito de performance para grandes volumes).
- Caso o projeto evolua para suportar cofres muito grandes ou sincronização
  parcial no futuro, essa decisão precisaria ser revisitada (fora do escopo
  atual, ver seção "Fora do escopo" no README).

## Alternativas consideradas
- **VaultItem com repositório próprio (`IVaultItemRepository`)**: rejeitada
  por permitir manipulação de itens sem passar pelas invariantes do `Vault`,
  aumentando o risco de estado inconsistente.