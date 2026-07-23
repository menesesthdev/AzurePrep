using AzurePrep.Application.Abstractions;
using AzurePrep.Application.Contracts;

namespace AzurePrep.Application.Exames;

public sealed class CatalogoDeExamesService : ICatalogoDeExamesService
{
    private readonly IExameRepository _examRepository;

    public CatalogoDeExamesService(IExameRepository examRepository)
    {
        _examRepository = examRepository;
    }

    public async Task<IReadOnlyList<ResumoDeExameDto>> ObterExamesDisponiveisAsync(CancellationToken cancellationToken = default)
    {
        var exams = await _examRepository.ObterTodosAsync(cancellationToken);

        return exams
            .OrderBy(e => e.Code)
            .Select(e => new ResumoDeExameDto(
                e.Id,
                e.Code,
                e.Name,
                e.TimeLimitMinutes,
                e.TotalQuestions,
                e.PassingScorePercent))
            .ToList();
    }
}
