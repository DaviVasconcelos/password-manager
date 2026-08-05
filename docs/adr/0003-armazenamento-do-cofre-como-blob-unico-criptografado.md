# 0003 - Armazenamento do cofre como blob único criptografado

## Status
Aceito

## Contexto
O cofre precisa ser persistido localmente via SQLite/EF Core. Duas
abordagens foram consideradas: (a) schema relacional, com uma tabela
por VaultItem/VaultFolder, criptografando campos sensíveis individualmente;
(b) serializar o Vault inteiro e armazenar como um único blob criptografado.

## Decisão
Optamos pela abordagem (b): o Vault é serializado (JSON) e criptografado
como um único blob (AES-256-GCM), armazenado em um registro único no
SQLite junto com o salt de derivação de chave e a versão do schema.

## Consequências
- Nenhum metadado (títulos, URLs, nomes de pasta) fica legível sem a
  senha mestra, incluindo metadados que numa abordagem relacional
  tendem a ficar em claro para permitir indexação/busca.
- O mesmo par Encrypt/Decrypt do ICryptoService é reutilizado tanto
  para persistência local quanto para export/import do arquivo .vault.
- A tag de autenticação (GCM) cobre o cofre inteiro, simplificando
  garantias de integridade.
- Abre mão de queries SQL diretas sobre itens/pastas — busca e filtro
  acontecem em memória, após descriptografar o cofre inteiro. Aceitável
  dado o volume esperado (cofre pessoal, não multi-usuário).
- EF Core/Migrations seguem úteis para versionamento de schema do
  registro (SchemaVersion) e colunas não sensíveis, mesmo com pouca
  normalização.