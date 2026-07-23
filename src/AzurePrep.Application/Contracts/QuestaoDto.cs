using AzurePrep.Domain.Enums;

namespace AzurePrep.Application.Contracts;

/// <summary>Uma opção de resposta como exibida ao candidato (sem revelar se é correta).</summary>
public sealed record OpcaoDeQuestaoDto(Guid Id, string Text, int OrderIndex);

/// <summary>
/// Questão renderizável durante a prova, já com a seleção atual do candidato e o estado
/// de "marcada para revisão". Nunca carrega a informação de qual opção é a correta.
/// </summary>
public sealed record QuestaoDto(
    Guid Id,
    int Number,
    string Text,
    TipoDeQuestao Type,
    IReadOnlyList<OpcaoDeQuestaoDto> Options,
    IReadOnlyList<Guid> SelectedOptionIds,
    bool IsFlaggedForReview,
    int TotalQuestions);
