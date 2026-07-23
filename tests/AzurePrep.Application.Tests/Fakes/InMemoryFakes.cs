using AzurePrep.Application.Abstractions;
using AzurePrep.Domain.Entidades;

namespace AzurePrep.Application.Tests.Fakes;

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
