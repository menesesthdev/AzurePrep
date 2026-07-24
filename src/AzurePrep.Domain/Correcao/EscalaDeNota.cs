namespace AzurePrep.Domain.Correcao;

/// <summary>
/// Converte o percentual de acertos na nota em escala 1–1000 usada pelos exames Microsoft,
/// onde a aprovação é sempre 700 — independentemente de quantos acertos isso representa.
/// </summary>
/// <remarks>
/// A escala real da Microsoft é derivada de Teoria de Resposta ao Item: cada questão tem peso
/// próprio, calibrado estatisticamente, e o mapeamento nunca é divulgado. Aqui usamos uma
/// aproximação linear por partes, ancorada no ponto que importa: o percentual de corte do exame
/// vira exatamente 700. Assim a nota exibida e o veredito aprovado/reprovado nunca se contradizem.
/// </remarks>
public static class EscalaDeNota
{
    /// <summary>Nota mínima de aprovação na escala Microsoft.</summary>
    public const int NotaDeCorte = 700;

    /// <summary>Menor nota possível na escala (a escala não começa em zero).</summary>
    public const int NotaMinima = 1;

    /// <summary>Maior nota possível na escala.</summary>
    public const int NotaMaxima = 1000;

    /// <summary>
    /// Converte <paramref name="scorePercent"/> (0–100) para a escala 1–1000, ancorando
    /// <paramref name="passingScorePercent"/> em <see cref="NotaDeCorte"/>.
    /// </summary>
    public static int Converter(decimal scorePercent, int passingScorePercent)
    {
        var percent = Math.Clamp(scorePercent, 0m, 100m);
        var passing = Math.Clamp(passingScorePercent, 0, 100);

        decimal scaled;

        if (passing <= 0)
        {
            // Sem corte: qualquer acerto já aprova, então toda a faixa vive acima de 700.
            scaled = NotaDeCorte + (percent / 100m * (NotaMaxima - NotaDeCorte));
        }
        else if (passing >= 100)
        {
            // Corte em 100%: só a prova perfeita atinge 700; o resto é reprovado.
            scaled = percent >= 100m
                ? NotaMaxima
                : NotaMinima + (percent / 100m * (NotaDeCorte - 1 - NotaMinima));
        }
        else if (percent >= passing)
        {
            // Faixa aprovada: 700 no corte, 1000 na prova perfeita.
            var above = (percent - passing) / (100m - passing);
            scaled = NotaDeCorte + (above * (NotaMaxima - NotaDeCorte));
        }
        else
        {
            // Faixa reprovada: 1 com zero acertos, 699 imediatamente abaixo do corte.
            var below = percent / passing;
            scaled = NotaMinima + (below * (NotaDeCorte - 1 - NotaMinima));
        }

        return Math.Clamp((int)Math.Round(scaled, MidpointRounding.AwayFromZero), NotaMinima, NotaMaxima);
    }
}
