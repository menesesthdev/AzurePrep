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
            // Exame em construção existe no banco e recebe questões, mas não é oferecido. Filtrar
            // aqui, e não no repositório, mantém o repositório como acesso a dado: o seed e os
            // testes continuam enxergando tudo, e quem decide o que é oferecido é o caso de uso.
            .Where(e => e.IsPublished)
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
