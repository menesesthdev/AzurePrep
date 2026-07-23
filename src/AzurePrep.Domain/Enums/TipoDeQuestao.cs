namespace AzurePrep.Domain.Enums;

/// <summary>
/// Formatos de questão suportados — todos publicamente documentados pela Microsoft
/// como parte do formato de prova. Replicamos o formato, nunca o conteúdo real.
/// </summary>
public enum TipoDeQuestao
{
    /// <summary>Uma única resposta correta (radio button).</summary>
    EscolhaUnica = 0,

    /// <summary>Duas ou mais respostas corretas (checkbox, "selecione N").</summary>
    EscolhaMultipla = 1,

    /// <summary>Afirmação avaliada como Verdadeiro/Falso ou Sim/Não.</summary>
    SimNao = 2
}
