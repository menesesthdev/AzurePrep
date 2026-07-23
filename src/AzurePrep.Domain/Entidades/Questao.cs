using AzurePrep.Domain.Common;
using AzurePrep.Domain.Enums;

namespace AzurePrep.Domain.Entidades;

/// <summary>
/// Questão do banco. Sempre original, escrita a partir do Skills Measured outline público —
/// nunca reproduz conteúdo real de prova. A <see cref="Explanation"/> deve justificar por que
/// cada distrator está errado, não só o gabarito.
/// </summary>
public class Questao : Entity
{
    private readonly List<OpcaoDeResposta> _options = new();

    // Construtor exigido pelo EF Core.
    private Questao()
    {
    }

    public Questao(
        Guid examId,
        Guid skillAreaId,
        string text,
        TipoDeQuestao type,
        string explanation,
        Guid? id = null)
        : base(id ?? Guid.NewGuid())
    {
        ExamId = examId;
        SkillAreaId = skillAreaId;
        Text = Guard.NotNullOrWhiteSpace(text, nameof(text));
        Type = type;
        Explanation = Guard.NotNullOrWhiteSpace(explanation, nameof(explanation));
    }

    public Guid ExamId { get; private set; }

    public Guid SkillAreaId { get; private set; }

    public string Text { get; private set; } = string.Empty;

    public TipoDeQuestao Type { get; private set; }

    public string Explanation { get; private set; } = string.Empty;

    public IReadOnlyCollection<OpcaoDeResposta> Options => _options.OrderBy(o => o.OrderIndex).ToList();

    public OpcaoDeResposta AdicionarOpcao(string text, bool isCorrect, int orderIndex, Guid? id = null)
    {
        var option = new OpcaoDeResposta(Id, text, isCorrect, orderIndex, id);
        _options.Add(option);
        return option;
    }

    /// <summary>Ids das alternativas corretas desta questão.</summary>
    public IReadOnlyCollection<Guid> CorrectOptionIds
        => _options.Where(o => o.IsCorrect).Select(o => o.Id).ToList();

    /// <summary>
    /// Uma questão está correta quando o conjunto de alternativas selecionadas é
    /// exatamente igual ao conjunto de alternativas corretas — nem a mais, nem a menos.
    /// Vale para single choice, múltipla escolha e Sim/Não.
    /// </summary>
    public bool RespondidaCorretamentePor(IEnumerable<Guid> selectedOptionIds)
    {
        var selected = selectedOptionIds.Distinct().ToHashSet();
        var correct = CorrectOptionIds.ToHashSet();
        return selected.SetEquals(correct);
    }
}
