using AzurePrep.Application.Abstractions;
using AzurePrep.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AzurePrep.Infrastructure.Persistence.Repositories;

public sealed class TentativaDeProvaRepository : ITentativaDeProvaRepository
{
    private readonly AzurePrepDbContext _db;

    public TentativaDeProvaRepository(AzurePrepDbContext db)
    {
        _db = db;
    }

    public async Task AdicionarAsync(TentativaDeProva attempt, CancellationToken cancellationToken = default)
        => await _db.ExamAttempts.AddAsync(attempt, cancellationToken);

    public async Task AdicionarRespostaAsync(RespostaDaTentativa answer, CancellationToken cancellationToken = default)
        => await _db.ExamAttemptAnswers.AddAsync(answer, cancellationToken);

    // Rastreado (sem AsNoTracking): SalvarResposta/Finalizar precisam alterar a tentativa carregada.
    public async Task<TentativaDeProva?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _db.ExamAttempts
            .Include(a => a.Answers)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    // AsNoTracking: o histórico é só leitura, nada aqui vai ser alterado.
    //
    // Ordenação por StartedAt e NÃO por nota: o SQLite não tem decimal nativo, então ordenar
    // por ScorePercent seria impreciso (é a dívida registrada no CLAUDE.md). Data é DateTime,
    // que o provider ordena corretamente — e é a ordem que o histórico quer de todo jeito.
    public async Task<IReadOnlyList<TentativaDeProva>> ObterDoUsuarioAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
        => await _db.ExamAttempts
            .AsNoTracking()
            .Include(a => a.Answers)
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.StartedAt)
            .ToListAsync(cancellationToken);
}
