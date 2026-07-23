using AzurePrep.Domain.Common;

namespace AzurePrep.Domain.Entidades;

/// <summary>
/// Uma alternativa de resposta de uma questão. <see cref="IsCorrect"/> nunca é exposto
/// ao candidato durante a prova — só na tela de revisão pós-resultado.
/// </summary>
public class OpcaoDeResposta : Entity
{
    // Construtor exigido pelo EF Core.
    private OpcaoDeResposta()
    {
    }

    public OpcaoDeResposta(Guid questionId, string text, bool isCorrect, int orderIndex, Guid? id = null)
        : base(id ?? Guid.NewGuid())
    {
        QuestionId = questionId;
        Text = Guard.NotNullOrWhiteSpace(text, nameof(text));
        IsCorrect = isCorrect;
        OrderIndex = orderIndex;
    }

    public Guid QuestionId { get; private set; }

    public string Text { get; private set; } = string.Empty;

    public bool IsCorrect { get; private set; }

    /// <summary>Ordem de exibição estável — evita depender da ordem de inserção no banco.</summary>
    public int OrderIndex { get; private set; }
}
