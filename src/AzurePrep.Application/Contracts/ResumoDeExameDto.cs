namespace AzurePrep.Application.Contracts;

/// <summary>Resumo de um exame disponível para a tela inicial.</summary>
public sealed record ResumoDeExameDto(
    Guid Id,
    string Code,
    string Name,
    int TimeLimitMinutes,
    int TotalQuestions,
    int PassingScorePercent);
