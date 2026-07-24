using AzurePrep.Domain.Enums;

namespace AzurePrep.Application.Contracts;

/// <summary>Placar de um domínio de habilidade na tela de resultado (score report).</summary>
public sealed record ResultadoPorAreaDto(
    string Name,
    decimal WeightPercent,
    int TotalQuestions,
    int CorrectAnswers,
    decimal ScorePercent);

/// <summary>Opção na revisão pós-prova — aqui sim expomos correta/selecionada.</summary>
public sealed record RevisaoDeOpcaoDto(string Text, bool IsCorrect, bool WasSelected);

/// <summary>Uma questão na revisão questão a questão, com explicação e gabarito.</summary>
public sealed record RevisaoDeQuestaoDto(
    int Number,
    string Text,
    TipoDeQuestao Type,
    bool WasCorrect,
    string Explanation,
    IReadOnlyList<RevisaoDeOpcaoDto> Options);

/// <summary>
/// Resultado completo de uma tentativa finalizada: score, aprovação, breakdown por skill area
/// e a revisão questão a questão.
/// </summary>
/// <remarks>
/// O score report fiel exibe apenas <see cref="ScaledScore"/> (escala 1–1000, corte em
/// <see cref="ScaledPassingScore"/>) — o percentual e a revisão questão a questão existem só
/// para o modo de estudo, que na prova real não é oferecido.
/// </remarks>
public sealed record ResultadoDaProvaDto(
    Guid AttemptId,
    string ExamCode,
    string ExamName,
    decimal ScorePercent,
    bool Passed,
    int PassingScorePercent,
    int TotalQuestions,
    int CorrectAnswers,
    DateTime StartedAt,
    DateTime FinishedAt,
    IReadOnlyList<ResultadoPorAreaDto> SkillAreas,
    IReadOnlyList<RevisaoDeQuestaoDto> Questions,
    int ScaledScore,
    int ScaledPassingScore);
