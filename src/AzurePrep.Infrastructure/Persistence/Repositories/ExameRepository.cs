using AzurePrep.Application.Abstractions;
using AzurePrep.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AzurePrep.Infrastructure.Persistence.Repositories;

public sealed class ExameRepository : IExameRepository
{
    private readonly AzurePrepDbContext _db;

    public ExameRepository(AzurePrepDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Exame>> ObterTodosAsync(CancellationToken cancellationToken = default)
        => await _db.Exams.AsNoTracking().ToListAsync(cancellationToken);

    public async Task<Exame?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _db.Exams.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<Exame?> ObterComConteudoAsync(Guid id, CancellationToken cancellationToken = default)
        => await _db.Exams
            .AsNoTracking()
            .Include(e => e.SkillAreas)
            .Include(e => e.Questions)
                .ThenInclude(q => q.Options)
            .AsSplitQuery()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
}
