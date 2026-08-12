# ADR 0004: Criptografia — Argon2id para derivação de chave e AES-256-GCM para o blob

## Status
Aceito

## Contexto
O cofre é persistido localmente como um único blob criptografado (ADR 0003),
serializado em JSON e protegido com a senha mestra do usuário. A segurança do
produto depende de duas premissas:

1. A senha mestra deve ser transformada em uma chave simétrica forte, de modo
   que a senha fraca do usuário não possa ser recuperada por força bruta.
2. O blob criptografado deve garantir **confidencialidade e autenticidade**:
   qualquer adulteração do dado deve ser detectada, tanto acidental quanto
   por ataque ativo.

Os parâmetros escolhidos (memória, iterações, paralelismo, tamanhos de nonce
e tag, layout do pacote) afetam a compatibilidade com dados já gravados:
alterá-los quebra a leitura de cofres existentes.

## Decisão

### Derivação de chave (Argon2id)
- Algoritmo: **Argon2id** (biblioteca `Konscious.Security.Cryptography.Argon2`),
  versão recomendada por resistência a side-channels de tempo.
- Parâmetros em produção (padrão do `CryptoService`):
  - Memória: **64 MiB** (65536 KB).
  - Iterações: **3**.
  - Paralelismo: **4**.
- Saída: **32 bytes** (chave AES-256).
- Salt: **16 bytes**, gerado com `RandomNumberGenerator`, validado com mínimo
  de 8 bytes (requisito do Argon2). O salt é persistido junto ao blob (ADR 0003).
- Os parâmetros do Argon2id são **configuráveis via construtor** do
  `CryptoService` — os testes usam valores reduzidos (32 KB, 1 iteração,
  1 paralelo) para execução rápida, mas **valores fracos nunca devem ser
  usados em produção**.

### Criptografia (AES-256-GCM)
- Algoritmo: **AES-256-GCM** (`System.Security.Cryptography.AesGcm`), modo
  autenticado que combina cifra e MAC.
- Nonce: **12 bytes**; tag de autenticação: **16 bytes**.
- Layout do pacote serializado: `nonce(12) + tag(16) + ciphertext(N)`.
- O mesmo par `Encrypt`/`Decrypt` do `ICryptoService` é usado tanto na
  persistência local quanto no futuro export/import (ADR 0003).

### Falhas de autenticação
- Qualquer falha de tag (senha errada ou dado corrompido/adulterado) lança
  `CryptographicIntegrityException`. O código **não distingue as duas causas**
  de propósito, para não servir de oráculo a um atacante.
- Pacote com tamanho insuficiente (menor que nonce + tag) também é tratado
  como falha de integridade.

### Validações
- Chave deve ter exatamente 32 bytes (sempre chamada com resultado do
  `DeriveKey`, mas validada para defender o uso incorreto da `CryptoService`).
- Salt mínimo de 8 bytes; senha mestra não vazia.

## Consequências

**Positivas:**
- Autenticidade garantida: o blob inteiro é coberto pela tag do GCM.
- Reuso do par `Encrypt`/`Decrypt` entre persistência e export/import.
- Parâmetros de derivação testáveis (injeção de valores baratos) sem
  permitir enfraquecimento acidental de produção — o padrão de 64 MiB
  permanece no construtor.

**Negativas / trade-offs aceitos:**
- Desbloquear o cofre com 64 MiB / 3 iterações custa da ordem de centenas
  de ms a ~1 s por máquina. Trade-off consciente: latência aceitável no
  desbloqueio em troca de custo alto de brute-force sobre a senha.
- Alterar qualquer parâmetro do Argon2id ou o layout do pacote tornaria
  ilegíveis cofres já gravados — mudanças desse tipo exigem nova versão de
  `SchemaVersion` e migração (começada no ADR 0003).
- A implementação usa a plataforma para AES-GCM e uma biblioteca de terceiros
  (Konscious) para Argon2; novos bugs/síntese nessas dependências ficam fora
  do nosso controle (mitigado pelas validações de integridade do GCM).

## Alternativas consideradas
- **AES-CBC + HMAC-SHA256 separado (encrypt-then-MAC)**: rejeitada por exigir
  encadeamento manual cifra+MAC com mais superfície para erro; GCM já entrega
  autenticação ao custo de uma construção padrão da plataforma.
- **libsodium / envelopes prontos (ex: Sodium.SecretBox)**: rejeitada por
  introduzir dependência nativa e APIs menos explícitas; prefere-se AES-GCM do
  BCL + Argon2id explícito, coerente com o objetivo de demonstração de
  criptografia aplicada no projeto.
- **Argon2i ou Argon2d**: rejeitados; Argon2id combina a resistência a
  side-channels do Argon2i com a proteção contra ataques de GPU/memória do
  Argon2d, sendo a recomendação atual.