using System.Text.Json;
using PasswordManager.Application.VaultRegistry;

namespace PasswordManager.Infrastructure.VaultRegistry;

/// <summary>
/// Implementação filesystem de <see cref="IVaultRegistry"/> usando
/// <c>vaults.json</c> + pasta <c>Vaults/</c> (ADR 0008, Opção B).
/// Todas as operações são serializadas via <see cref="SemaphoreSlim"/>
/// e a escrita é atômica (temp + Move).
/// </summary>
public sealed class FileSystemVaultRegistry : IVaultRegistry
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _vaultsJsonPath;
    private readonly string _vaultsDir;
    private readonly string _legacyVaultDbPath;
    private readonly SemaphoreSlim _semaforo = new(1, 1);

    private List<VaultDescriptor> _vaults = [];
    private Guid? _ativoId;
    private bool _inicializado;

    public FileSystemVaultRegistry(string vaultsJsonPath, string vaultsDir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vaultsJsonPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(vaultsDir);

        _vaultsJsonPath = vaultsJsonPath;
        _vaultsDir = vaultsDir;

        var parent = Path.GetDirectoryName(vaultsJsonPath)
                     ?? Path.GetDirectoryName(vaultsDir)
                     ?? vaultsDir;
        _legacyVaultDbPath = Path.Combine(parent, "vault.db");
    }

    public Guid? AtivoId => _ativoId;
    public VaultDescriptor? Ativo => _ativoId is null ? null : _vaults.FirstOrDefault(v => v.Id == _ativoId);

    public async Task InicializarAsync(CancellationToken ct = default)
    {
        await _semaforo.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_inicializado) return;

            Directory.CreateDirectory(_vaultsDir);
            Directory.CreateDirectory(Path.GetDirectoryName(_vaultsJsonPath) ?? _vaultsDir);

            // Carrega registry existente (tolerante a corrupção).
            await CarregarDoDiscoAsync(ct).ConfigureAwait(false);

            // Migração legada: vault.db -> Vaults/vault-1.db
            if (File.Exists(_legacyVaultDbPath))
            {
                // Se já há registro correspondente, não migrar de novo.
                var jaMigrado = _vaults.Count > 0;
                if (!jaMigrado)
                {
                    var nome = "vault-1";
                    var arquivo = ObterNomeArquivoUnico(nome);
                    var destino = Path.Combine(_vaultsDir, arquivo);

                    try
                    {
                        File.Move(_legacyVaultDbPath, destino);
                    }
                    catch (IOException)
                    {
                        // Se Move falhar (ex.: destino já existe), tenta próximo nome.
                        arquivo = ObterNomeArquivoUnico("vault-1");
                        destino = Path.Combine(_vaultsDir, arquivo);
                        File.Move(_legacyVaultDbPath, destino);
                    }

                    var id = Guid.NewGuid();
                    var agora = DateTime.UtcNow;
                    var descriptor = new VaultDescriptor(id, nome, arquivo, agora, agora);
                    _vaults.Add(descriptor);
                    _ativoId = id;
                    await SalvarNoDiscoAsync(ct).ConfigureAwait(false);
                }
            }
            else if (_vaults.Count == 0 && !File.Exists(_vaultsJsonPath))
            {
                // Registry vazio: persiste arquivo inicial.
                await SalvarNoDiscoAsync(ct).ConfigureAwait(false);
            }

            // Garante que arquivos órfãos não fiquem sem registro? Não — apenas registra.
            // Se houver arquivos em Vaults/ sem entrada no registry, eles são ignorados
            // até que o usuário importe manualmente. Isso evita surpresas.

            _inicializado = true;
        }
        finally
        {
            _semaforo.Release();
        }
    }

    public async Task<IReadOnlyList<VaultDescriptor>> ListarAsync(CancellationToken ct = default)
    {
        await GarantirInicializadoAsync(ct).ConfigureAwait(false);
        await _semaforo.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Retorna cópia ordenada por nome.
            return _vaults.OrderBy(v => v.Nome, StringComparer.OrdinalIgnoreCase).ToList();
        }
        finally
        {
            _semaforo.Release();
        }
    }

    public async Task<VaultDescriptor> CriarAsync(string? nome, CancellationToken ct = default)
    {
        await GarantirInicializadoAsync(ct).ConfigureAwait(false);
        await _semaforo.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var nomeEfetivo = string.IsNullOrWhiteSpace(nome) ? GerarNomePadrao() : nome.Trim();
            VaultNameValidator.Validar(nomeEfetivo);

            if (_vaults.Any(v => string.Equals(v.Nome, nomeEfetivo, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Já existe um cofre com o nome \"{nomeEfetivo}\".");

            var arquivo = ObterNomeArquivoUnico(nomeEfetivo);
            var id = Guid.NewGuid();
            var agora = DateTime.UtcNow;
            var descriptor = new VaultDescriptor(id, nomeEfetivo, arquivo, agora, agora);
            _vaults.Add(descriptor);
            _ativoId = id;

            await SalvarNoDiscoAsync(ct).ConfigureAwait(false);
            return descriptor;
        }
        finally
        {
            _semaforo.Release();
        }
    }

    public async Task RenomearAsync(Guid id, string novoNome, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(novoNome);
        await GarantirInicializadoAsync(ct).ConfigureAwait(false);
        await _semaforo.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var idx = _vaults.FindIndex(v => v.Id == id);
            if (idx < 0)
                throw new InvalidOperationException("Cofre não encontrado para renomear.");

            var trimmed = novoNome.Trim();
            VaultNameValidator.Validar(trimmed);

            if (_vaults.Any(v => v.Id != id && string.Equals(v.Nome, trimmed, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Já existe um cofre com o nome \"{trimmed}\".");

            var atual = _vaults[idx];
            if (string.Equals(atual.Nome, trimmed, StringComparison.Ordinal))
            {
                // Mesmo nome exato: nada a fazer.
                return;
            }

            var novoArquivo = ObterNomeArquivoUnico(trimmed, ignorarId: id);
            var caminhoAntigo = Path.Combine(_vaultsDir, atual.Arquivo);
            var caminhoNovo = Path.Combine(_vaultsDir, novoArquivo);

            if (File.Exists(caminhoAntigo))
            {
                // Garante que destino não existe (deveria ser único).
                if (File.Exists(caminhoNovo))
                    throw new InvalidOperationException($"Já existe um arquivo com o nome \"{novoArquivo}\".");

                File.Move(caminhoAntigo, caminhoNovo);
            }

            var atualizado = atual with
            {
                Nome = trimmed,
                Arquivo = novoArquivo,
                AtualizadoEm = DateTime.UtcNow
            };
            _vaults[idx] = atualizado;

            await SalvarNoDiscoAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _semaforo.Release();
        }
    }

    public async Task ExcluirAsync(Guid id, CancellationToken ct = default)
    {
        await GarantirInicializadoAsync(ct).ConfigureAwait(false);
        await _semaforo.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var idx = _vaults.FindIndex(v => v.Id == id);
            if (idx < 0)
                throw new InvalidOperationException("Cofre não encontrado para excluir.");

            var descriptor = _vaults[idx];
            var caminho = Path.Combine(_vaultsDir, descriptor.Arquivo);

            // Deleta arquivo físico se existir (ignora erro se já não existe).
            // Com Pooling=False o arquivo não deveria estar em uso, mas
            // por segurança limpa o pool e tenta novamente.
            if (File.Exists(caminho))
            {
                try
                {
                    File.Delete(caminho);
                }
                catch (IOException)
                {
                    try
                    {
                        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        if (File.Exists(caminho))
                            File.Delete(caminho);
                    }
                    catch (IOException)
                    {
                        throw new InvalidOperationException("O arquivo do cofre está em uso e não pode ser excluído no momento.");
                    }
                }

                // Remove arquivos auxiliares WAL/SHM se existirem.
                var wal = caminho + "-wal";
                var shm = caminho + "-shm";
                try { if (File.Exists(wal)) File.Delete(wal); } catch { }
                try { if (File.Exists(shm)) File.Delete(shm); } catch { }
            }

            _vaults.RemoveAt(idx);

            if (_ativoId == id)
            {
                _ativoId = _vaults.FirstOrDefault()?.Id;
            }

            await SalvarNoDiscoAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _semaforo.Release();
        }
    }

    public async Task DefinirAtivoAsync(Guid id, CancellationToken ct = default)
    {
        await GarantirInicializadoAsync(ct).ConfigureAwait(false);
        await _semaforo.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!_vaults.Any(v => v.Id == id))
                throw new InvalidOperationException("Cofre não encontrado para definir como ativo.");

            _ativoId = id;
            await SalvarNoDiscoAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _semaforo.Release();
        }
    }

    public string ObterCaminho(Guid id)
    {
        // Tenta resolver sem lock para uso síncrono; se não inicializado, carrega de forma síncrona.
        var descriptor = _vaults.FirstOrDefault(v => v.Id == id)
                         ?? throw new InvalidOperationException("Cofre não encontrado.");
        return Path.Combine(_vaultsDir, descriptor.Arquivo);
    }

    public string ObterCaminhoAtivo()
    {
        var id = _ativoId ?? throw new InvalidOperationException("Nenhum cofre ativo definido.");
        return ObterCaminho(id);
    }

    // --- Privados ---

    private async Task GarantirInicializadoAsync(CancellationToken ct)
    {
        if (_inicializado) return;
        await InicializarAsync(ct).ConfigureAwait(false);
    }

    private string GerarNomePadrao()
    {
        // vault-1, vault-2, ... próximo livre.
        var max = 0;
        foreach (var v in _vaults)
        {
            if (v.Nome.StartsWith("vault-", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(v.Nome.AsSpan(6), out var n)
                && n > max)
                max = n;
        }
        var candidato = $"vault-{max + 1}";
        // Garante unicidade caso usuário tenha criado "vault-1" manualmente.
        while (_vaults.Any(v => string.Equals(v.Nome, candidato, StringComparison.OrdinalIgnoreCase)))
        {
            max++;
            candidato = $"vault-{max + 1}";
        }
        return candidato;
    }

    private string ObterNomeArquivoUnico(string nomeBase, Guid? ignorarId = null)
    {
        var slug = VaultNameValidator.GerarSlug(nomeBase);
        var baseNome = $"{slug}.db";
        var arquivosExistentes = new HashSet<string>(
            _vaults.Where(v => ignorarId is null || v.Id != ignorarId.Value)
                   .Select(v => v.Arquivo),
            StringComparer.OrdinalIgnoreCase);

        if (!arquivosExistentes.Contains(baseNome) && !File.Exists(Path.Combine(_vaultsDir, baseNome)))
            return baseNome;

        var i = 2;
        while (true)
        {
            var candidato = $"{slug}-{i}.db";
            if (!arquivosExistentes.Contains(candidato) && !File.Exists(Path.Combine(_vaultsDir, candidato)))
                return candidato;
            i++;
            if (i > 1000) throw new InvalidOperationException("Não foi possível gerar um nome de arquivo único.");
        }
    }

    private async Task CarregarDoDiscoAsync(CancellationToken ct)
    {
        if (!File.Exists(_vaultsJsonPath))
        {
            _vaults = [];
            _ativoId = null;
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_vaultsJsonPath, ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
            {
                _vaults = [];
                _ativoId = null;
                return;
            }

            var dto = JsonSerializer.Deserialize<VaultRegistryDto>(json, JsonOptions);
            if (dto is null)
            {
                _vaults = [];
                _ativoId = null;
                return;
            }

            _vaults = dto.Vaults?.Select(e => new VaultDescriptor(e.Id, e.Nome, e.Arquivo, e.CriadoEm, e.AtualizadoEm)).ToList() ?? [];
            _ativoId = dto.AtivoId;

            // Valida ativoId: se não existe mais, limpa.
            if (_ativoId is not null && !_vaults.Any(v => v.Id == _ativoId))
                _ativoId = null;

            // Ordena internamente por nome não é necessário; ListarAsync ordena.
        }
        catch (JsonException)
        {
            _vaults = [];
            _ativoId = null;
        }
        catch (IOException)
        {
            _vaults = [];
            _ativoId = null;
        }
        catch (UnauthorizedAccessException)
        {
            _vaults = [];
            _ativoId = null;
        }
    }

    private async Task SalvarNoDiscoAsync(CancellationToken ct)
    {
        var dto = new VaultRegistryDto
        {
            Vaults = _vaults.Select(v => new VaultRegistryEntryDto
            {
                Id = v.Id,
                Nome = v.Nome,
                Arquivo = v.Arquivo,
                CriadoEm = v.CriadoEm,
                AtualizadoEm = v.AtualizadoEm
            }).ToList(),
            AtivoId = _ativoId
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);
        var dir = Path.GetDirectoryName(_vaultsJsonPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var tempPath = _vaultsJsonPath + ".tmp";
        await File.WriteAllTextAsync(tempPath, json, ct).ConfigureAwait(false);

        // Move atômico.
        File.Move(tempPath, _vaultsJsonPath, overwrite: true);
    }

    private sealed class VaultRegistryDto
    {
        public List<VaultRegistryEntryDto>? Vaults { get; set; }
        public Guid? AtivoId { get; set; }
    }

    private sealed class VaultRegistryEntryDto
    {
        public Guid Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Arquivo { get; set; } = string.Empty;
        public DateTime CriadoEm { get; set; }
        public DateTime AtualizadoEm { get; set; }
    }
}
