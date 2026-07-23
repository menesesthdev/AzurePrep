namespace AzurePrep.Application.Contracts;

/// <summary>Status de uma questão no painel de navegação lateral.</summary>
public sealed record StatusDaQuestaoDto(
    Guid QuestionId,
    int Number,
    bool IsAnswered,
    bool IsFlaggedForReview);

/// <summary>
/// Estado geral de uma tentativa em andamento: dados do exame, tempo restante e o status
/// de cada questão — tudo que o header fixo e o painel lateral precisam para renderizar.
/// </summary>
public sealed record EstadoDaTentativaDto(
    Guid AttemptId,
    string ExamCode,
    string ExamName,
    int TimeLimitMinutes,
    DateTime StartedAt,
    int RemainingSeconds,
    bool IsFinished,
    IReadOnlyList<StatusDaQuestaoDto> Questions);
