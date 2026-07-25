using AzurePrep.Application.Abstractions;
using AzurePrep.Domain.Entidades;
using AzurePrep.Domain.Enums;

namespace AzurePrep.Application.Tests.Fakes;

/// <summary>Usuário logado controlável — trocar o Id simula outra pessoa na mesma sessão.</summary>
public sealed class FakeUsuarioAtual : IUsuarioAtual
{
    public FakeUsuarioAtual(Guid? id = null) => Id = id ?? Guid.NewGuid();
    public Guid? Id { get; set; }
}

public sealed class InMemoryUsuarioRepository : IUsuarioRepository
{
    private readonly List<Usuario> _usuarios = new();
    private readonly List<TokenDeRedefinicaoDeSenha> _tokens = new();

    public IReadOnlyList<Usuario> Todos => _usuarios;

    public IReadOnlyList<TokenDeRedefinicaoDeSenha> Tokens => _tokens;

    public Task<Usuario?> ObterPorProvedorAsync(
        ProvedorDeLogin provider,
        string providerKey,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_usuarios.FirstOrDefault(u => u.Provider == provider && u.ProviderKey == providerKey));

    public Task<Usuario?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_usuarios.FirstOrDefault(u => u.Id == id));

    // Em memória não existe diferença entre rastreado e não rastreado: a instância é a mesma.
    public Task<Usuario?> ObterPorIdParaAtualizacaoAsync(Guid id, CancellationToken cancellationToken = default)
        => ObterPorIdAsync(id, cancellationToken);

    public Task AdicionarAsync(Usuario usuario, CancellationToken cancellationToken = default)
    {
        _usuarios.Add(usuario);
        return Task.CompletedTask;
    }

    public Task AdicionarTokenDeRedefinicaoAsync(
        TokenDeRedefinicaoDeSenha token,
        CancellationToken cancellationToken = default)
    {
        _tokens.Add(token);
        return Task.CompletedTask;
    }

    public Task<TokenDeRedefinicaoDeSenha?> ObterTokenDeRedefinicaoAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_tokens.FirstOrDefault(t => t.TokenHash == tokenHash));

    public Task<IReadOnlyList<TokenDeRedefinicaoDeSenha>> ObterTokensAtivosDoUsuarioAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<TokenDeRedefinicaoDeSenha>>(
            _tokens.Where(t => t.UserId == userId && t.UsedAt is null).ToList());
}

/// <summary>
/// Gerador previsível: token sequencial e hash com prefixo. Determinístico para o teste poder
/// afirmar qual token foi emitido — a aleatoriedade de verdade é do
/// <c>GeradorDeTokenSeguro</c>, coberto na Infrastructure.
/// </summary>
public sealed class FakeGeradorDeTokenSeguro : IGeradorDeTokenSeguro
{
    private int _contador;

    public string UltimoGerado { get; private set; } = string.Empty;

    public string Gerar() => UltimoGerado = $"token-{++_contador}";

    public string Hash(string token) => $"hash:{token}";
}

/// <summary>
/// Hasher determinístico e barato. O PBKDF2 real custa centenas de milissegundos por chamada
/// de propósito — usá-lo aqui tornaria a suíte lenta sem testar nada da regra de cadastro.
/// A derivação de verdade é coberta em <c>HasherDeSenhaPbkdf2Tests</c>.
/// </summary>
public sealed class FakeHasherDeSenha : IHasherDeSenha
{
    public int ChamadasDeVerificacao { get; private set; }

    public string Hash(string senha) => $"fake:{senha}";

    public bool Verificar(string senha, string hash)
    {
        ChamadasDeVerificacao++;
        return hash == $"fake:{senha}";
    }
}

/// <summary>Relógio controlável para testar tempo restante e finalização.</summary>
public sealed class FixedClock : IClock
{
    public FixedClock(DateTime utcNow) => UtcNow = utcNow;
    public DateTime UtcNow { get; set; }
    public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
}

/// <summary>Unit of work no-op: as mutações já ocorrem nos objetos em memória.</summary>
public sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.FromResult(0);
    }
}

public sealed class InMemoryExamRepository : IExameRepository
{
    private readonly Dictionary<Guid, Exame> _exams;

    public InMemoryExamRepository(params Exame[] exams)
        => _exams = exams.ToDictionary(e => e.Id);

    public Task<IReadOnlyList<Exame>> ObterTodosAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Exame>>(_exams.Values.ToList());

    public Task<Exame?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_exams.GetValueOrDefault(id));

    // Em memória o objeto já traz skill areas, questões e opções carregadas.
    public Task<Exame?> ObterComConteudoAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_exams.GetValueOrDefault(id));
}

public sealed class InMemoryExamAttemptRepository : ITentativaDeProvaRepository
{
    private readonly Dictionary<Guid, TentativaDeProva> _attempts = new();

    public Task AdicionarAsync(TentativaDeProva attempt, CancellationToken cancellationToken = default)
    {
        _attempts[attempt.Id] = attempt;
        return Task.CompletedTask;
    }

    // Em memória a resposta já está no agregado; nada a fazer.
    public Task AdicionarRespostaAsync(RespostaDaTentativa answer, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<TentativaDeProva?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_attempts.GetValueOrDefault(id));
}
