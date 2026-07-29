namespace AzurePrep.Infrastructure.Persistence.Seed;

/// <summary>
/// Um lote de questões vindo de um arquivo JSON embutido. O formato é o descrito em
/// <c>docs/formato-questoes.md</c> — este tipo é só a forma desserializada dele.
/// </summary>
public sealed record ArquivoDeQuestoes
{
    /// <summary>Nome do recurso de origem, usado nas mensagens de validação.</summary>
    public string Origem { get; init; } = string.Empty;

    public string ExameCode { get; init; } = string.Empty;

    /// <summary>Slug da área de habilidade (ex.: "conceitos-de-nuvem").</summary>
    public string Area { get; init; } = string.Empty;

    public IReadOnlyList<QuestaoDeSeed> Questoes { get; init; } = Array.Empty<QuestaoDeSeed>();
}

public sealed record QuestaoDeSeed
{
    /// <summary>Chave estável da questão — vira <c>ExternalId</c> e origem do Guid.</summary>
    public string Id { get; init; } = string.Empty;

    public string? Topico { get; init; }

    /// <summary>"EscolhaUnica", "EscolhaMultipla" ou "SimNao".</summary>
    public string Tipo { get; init; } = string.Empty;

    public string Enunciado { get; init; } = string.Empty;

    public string Explicacao { get; init; } = string.Empty;

    public IReadOnlyList<OpcaoDeSeed> Opcoes { get; init; } = Array.Empty<OpcaoDeSeed>();
}

public sealed record OpcaoDeSeed
{
    public string Texto { get; init; } = string.Empty;

    public bool Correta { get; init; }
}
