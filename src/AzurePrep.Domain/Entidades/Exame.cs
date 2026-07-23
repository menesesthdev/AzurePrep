using AzurePrep.Domain.Common;

namespace AzurePrep.Domain.Entidades;

/// <summary>
/// Representa uma certificação/exame (ex.: AZ-900). O modelo é genérico de propósito:
/// nada aqui é hardcoded para o AZ-900, para suportar futuros exames (AZ-104, AI-900...).
/// </summary>
public class Exame : Entity
{
    private readonly List<AreaDeHabilidade> _skillAreas = new();
    private readonly List<Questao> _questions = new();

    // Construtor exigido pelo EF Core (materialização).
    private Exame()
    {
    }

    public Exame(
        string code,
        string name,
        int timeLimitMinutes,
        int passingScorePercent,
        int totalQuestions,
        Guid? id = null)
        : base(id ?? Guid.NewGuid())
    {
        Code = Guard.NotNullOrWhiteSpace(code, nameof(code));
        Name = Guard.NotNullOrWhiteSpace(name, nameof(name));
        TimeLimitMinutes = Guard.Positive(timeLimitMinutes, nameof(timeLimitMinutes));
        PassingScorePercent = Guard.InRange(passingScorePercent, 0, 100, nameof(passingScorePercent));
        TotalQuestions = Guard.Positive(totalQuestions, nameof(totalQuestions));
    }

    /// <summary>Código oficial do exame (ex.: "AZ-900").</summary>
    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public int TimeLimitMinutes { get; private set; }

    /// <summary>Percentual mínimo para aprovação (ex.: 70).</summary>
    public int PassingScorePercent { get; private set; }

    /// <summary>Quantidade de questões que compõem uma tentativa deste exame.</summary>
    public int TotalQuestions { get; private set; }

    public IReadOnlyCollection<AreaDeHabilidade> SkillAreas => _skillAreas;

    public IReadOnlyCollection<Questao> Questions => _questions;

    public AreaDeHabilidade AdicionarAreaDeHabilidade(string name, decimal weightPercent, Guid? id = null)
    {
        var skillArea = new AreaDeHabilidade(Id, name, weightPercent, id);
        _skillAreas.Add(skillArea);
        return skillArea;
    }

    /// <summary>
    /// Adiciona uma questão ao exame. As questões fazem parte do agregado Exame — é aqui que a
    /// coleção <see cref="Questions"/> é a fonte da verdade (o EF a repovoa via Include ao ler).
    /// </summary>
    public Questao AdicionarQuestao(
        Guid skillAreaId,
        string text,
        Enums.TipoDeQuestao type,
        string explanation,
        Guid? id = null)
    {
        var question = new Questao(Id, skillAreaId, text, type, explanation, id);
        _questions.Add(question);
        return question;
    }
}
