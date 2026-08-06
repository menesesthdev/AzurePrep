using AzurePrep.Domain.Enums;

namespace AzurePrep.Application.Contracts;

/// <summary>
/// Uma opção de resposta como exibida ao candidato (sem revelar se é correta).
/// </summary>
/// <remarks>
/// <paramref name="OrderIndex"/> é a posição NESTA tentativa, não a posição no banco de questões:
/// a ordem das alternativas é embaralhada por tentativa (ver <c>OrdemDasOpcoes</c>). Expor o índice
/// do arquivo aqui entregaria o gabarito, já que o seed escreve a alternativa correta primeiro.
/// </remarks>
public sealed record OpcaoDeQuestaoDto(Guid Id, string Text, int OrderIndex);

/// <summary>
/// Questão renderizável durante a prova, já com a seleção atual do candidato e o estado
/// de "marcada para revisão". Nunca carrega a informação de qual opção é a correta.
/// </summary>
/// <remarks>
/// <paramref name="RequiredSelections"/> é a quantidade de alternativas que o candidato deve
/// marcar. Não revela o gabarito — é a mesma informação que a prova real imprime no enunciado
/// ("Escolha duas."), e é o que permite classificar a questão como incompleta na revisão.
/// </remarks>
public sealed record QuestaoDto(
    Guid Id,
    int Number,
    string Text,
    TipoDeQuestao Type,
    IReadOnlyList<OpcaoDeQuestaoDto> Options,
    IReadOnlyList<Guid> SelectedOptionIds,
    bool IsFlaggedForReview,
    int TotalQuestions,
    int RequiredSelections);
