using AzurePrep.Domain.Common;

namespace AzurePrep.Domain.Entidades;

/// <summary>
/// Domínio de habilidades do exame, com seu peso (ex.: "Descrever conceitos de nuvem — 25-30%").
/// Os pesos vêm do Skills Measured outline oficial da Microsoft — não são inventados.
/// </summary>
public class AreaDeHabilidade : Entity
{
    private readonly List<Questao> _questions = new();

    // Construtor exigido pelo EF Core.
    private AreaDeHabilidade()
    {
    }

    public AreaDeHabilidade(Guid examId, string key, string name, decimal weightPercent, Guid? id = null)
        : base(id ?? Guid.NewGuid())
    {
        ExamId = examId;
        Key = Guard.NotNullOrWhiteSpace(key, nameof(key));
        Name = Guard.NotNullOrWhiteSpace(name, nameof(name));
        WeightPercent = Guard.InRange(weightPercent, 0m, 100m, nameof(weightPercent));
    }

    public Guid ExamId { get; private set; }

    /// <summary>
    /// Slug estável da área (ex.: "conceitos-de-nuvem"). É por ele que os arquivos de seed
    /// referenciam o domínio — o <see cref="Name"/> é texto de UI e pode ser reescrito sem
    /// quebrar nada, o Key não.
    /// </summary>
    public string Key { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    /// <summary>Peso do domínio no exame, em pontos percentuais (ex.: 27.5).</summary>
    public decimal WeightPercent { get; private set; }

    public IReadOnlyCollection<Questao> Questions => _questions;
}
