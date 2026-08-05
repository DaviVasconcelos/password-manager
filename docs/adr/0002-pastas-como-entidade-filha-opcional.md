# 0002. Pastas como entidade filha opcional do Vault

## Status
Aceito

## Contexto
O escopo do MVP define um único cofre por usuário (sem suporte a múltiplos
cofres, por decisão consciente documentada no README). Ainda assim, é
desejável permitir que o usuário organize seus `VaultItem` em grupos
nomeados, sem introduzir complexidade que dificulte uma futura evolução
para múltiplos cofres.

Duas abordagens foram consideradas:

1. Tratar pastas como um conceito de "container" separado do agregado
   `Vault`, com repositório próprio.
2. Tratar `VaultFolder` como mais uma entidade filha do agregado `Vault`,
   seguindo o mesmo padrão já usado para `VaultItem` (ADR 0001).

A opção 1 quebraria o invariante estabelecido no ADR 0001 (todo acesso
passa pelo agregado raiz `Vault`, sem repositórios para entidades filhas).
A opção 2 mantém consistência arquitetural e é suficiente para o escopo
atual (organização simples, sem hierarquia).

## Decisão
- `VaultFolder` é uma entidade filha do agregado `Vault`, criada e
  removida exclusivamente através de métodos do próprio `Vault`
  (`AddFolder`, `RemoveFolder`), assim como já ocorre com `VaultItem`.
- Não há hierarquia de pastas (uma pasta não pode conter outra pasta).
  Mantém o MVP pequeno, coerente com a decisão já tomada para o restante
  do escopo.
- `VaultItem` ganha a propriedade `FolderId` (nullable). Um item sem
  pasta é o padrão; a pasta é um recurso opcional de organização, nunca
  obrigatório.
- A associação de um item a uma pasta é feita via
  `Vault.AssignItemToFolder(itemId, folderId)`, que valida a existência
  de ambos antes de delegar a mudança de estado ao `VaultItem` através
  de um método `internal` (`AssignToFolder`). Isso impede que o
  `VaultItem` seja movido para uma pasta inexistente por fora do
  agregado.
- Remover uma pasta (`RemoveFolder`) **não apaga os itens** que estavam
  nela — eles voltam ao estado "sem pasta" (`FolderId = null`). Perder
  senhas por consequência indireta da exclusão de uma pasta seria um
  risco inaceitável para um gerenciador de senhas.

## Consequências
- **Positivas**: nenhuma mudança nas interfaces da Application definidas
  até aqui — `IVaultRepository` continua expondo `Get`/`Save` para o
  agregado inteiro, sem repositório dedicado a pastas. Se no futuro o
  projeto evoluir para múltiplos cofres, `VaultFolder` já nasce escopada
  a um `Vault` específico, sem necessidade de refatoração.
- **Negativas**: como não há hierarquia, o usuário não pode agrupar
  pastas dentro de pastas. Se essa necessidade surgir depois, será uma
  mudança de escopo deliberada, não uma limitação técnica desta decisão.
- Fica registrado que exclusão de pasta é nomeada explicitamente como
  operação não-destrutiva para itens, para evitar ambiguidade em revisões
  futuras de código.