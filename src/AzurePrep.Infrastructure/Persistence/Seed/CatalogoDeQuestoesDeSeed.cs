using System.Reflection;
using System.Text.Json;
using AzurePrep.Domain.Enums;

namespace AzurePrep.Infrastructure.Persistence.Seed;

/// <summary>
/// Lê e valida os arquivos de questões embutidos no assembly.
///
/// Adicionar questão ao projeto é adicionar um arquivo JSON aqui — nenhum código muda. A
/// validação é dura de propósito: um banco de questões com gabarito ausente, duplicata de
/// enunciado ou tipo inconsistente estraga a prova de forma silenciosa, e o teste que roda esta
/// validação é a única barreira antes disso chegar no usuário.
/// </summary>
public static class CatalogoDeQuestoesDeSeed
{
    private const string PastaDeRecursos = ".Persistence.Seed.Questoes.";

    private static readonly JsonSerializerOptions Opcoes = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>Todos os lotes embutidos, já desserializados. Não valida — use <see cref="Validar"/>.</summary>
    public static IReadOnlyList<ArquivoDeQuestoes> Carregar()
    {
        var assembly = typeof(CatalogoDeQuestoesDeSeed).Assembly;

        return assembly.GetManifestResourceNames()
            .Where(nome => nome.Contains(PastaDeRecursos, StringComparison.Ordinal)
                           && nome.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(nome => nome, StringComparer.Ordinal)
            .Select(nome => Desserializar(assembly, nome))
            .ToList();
    }

    private static ArquivoDeQuestoes Desserializar(Assembly assembly, string nomeDoRecurso)
    {
        using var stream = assembly.GetManifestResourceStream(nomeDoRecurso)
                           ?? throw new InvalidOperationException($"Recurso {nomeDoRecurso} não pôde ser aberto.");

        var arquivo = JsonSerializer.Deserialize<ArquivoDeQuestoes>(stream, Opcoes)
                      ?? throw new InvalidOperationException($"Recurso {nomeDoRecurso} não contém um lote de questões válido.");

        return arquivo with { Origem = NomeCurto(nomeDoRecurso) };
    }

    private static string NomeCurto(string nomeDoRecurso)
    {
        var indice = nomeDoRecurso.LastIndexOf(PastaDeRecursos, StringComparison.Ordinal);
        return indice < 0 ? nomeDoRecurso : nomeDoRecurso[(indice + PastaDeRecursos.Length)..];
    }

    /// <summary>
    /// Verifica tudo que o formato exige e devolve a lista de problemas — vazia quando o banco
    /// está íntegro. Devolver em vez de lançar deixa o teste mostrar todos os erros de uma vez,
    /// em vez de um por execução.
    /// </summary>
    public static IReadOnlyList<string> Validar(
        IReadOnlyList<ArquivoDeQuestoes> arquivos,
        IReadOnlyCollection<string> areasConhecidas)
    {
        var problemas = new List<string>();
        var idsVistos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var enunciadosVistos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var arquivo in arquivos)
        {
            if (!areasConhecidas.Contains(arquivo.Area))
            {
                problemas.Add($"{arquivo.Origem}: área '{arquivo.Area}' não existe no exame " +
                              $"(conhecidas: {string.Join(", ", areasConhecidas)}).");
            }

            if (arquivo.Questoes.Count == 0)
            {
                problemas.Add($"{arquivo.Origem}: nenhum item em 'questoes'.");
            }

            foreach (var questao in arquivo.Questoes)
            {
                var rotulo = $"{arquivo.Origem}[{questao.Id}]";

                if (string.IsNullOrWhiteSpace(questao.Id))
                {
                    problemas.Add($"{arquivo.Origem}: questão sem 'id'.");
                    continue;
                }

                if (idsVistos.TryGetValue(questao.Id, out var origemAnterior))
                {
                    problemas.Add($"{rotulo}: id repetido (já usado em {origemAnterior}).");
                }
                else
                {
                    idsVistos[questao.Id] = arquivo.Origem;
                }

                ValidarConteudo(questao, rotulo, problemas);
                ValidarOpcoes(questao, rotulo, problemas);

                var chaveDeTexto = NormalizarTexto(questao.Enunciado);
                if (chaveDeTexto.Length > 0)
                {
                    if (enunciadosVistos.TryGetValue(chaveDeTexto, out var duplicata))
                    {
                        problemas.Add($"{rotulo}: enunciado duplicado de {duplicata}.");
                    }
                    else
                    {
                        enunciadosVistos[chaveDeTexto] = rotulo;
                    }
                }
            }
        }

        return problemas;
    }

    private static void ValidarConteudo(QuestaoDeSeed questao, string rotulo, List<string> problemas)
    {
        if (string.IsNullOrWhiteSpace(questao.Enunciado))
        {
            problemas.Add($"{rotulo}: enunciado vazio.");
        }

        if (string.IsNullOrWhiteSpace(questao.Explicacao))
        {
            problemas.Add($"{rotulo}: explicação vazia.");
        }
        else if (questao.Explicacao.Trim().Length < 120)
        {
            // Explicação curta demais não tem como justificar a correta E cada distrator — que é
            // o que o formato exige, e o que faz o simulado ensinar em vez de só pontuar.
            problemas.Add($"{rotulo}: explicação curta demais ({questao.Explicacao.Trim().Length} caracteres) " +
                          "para justificar a resposta certa e cada distrator.");
        }

        if (!TentarConverterTipo(questao.Tipo, out _))
        {
            problemas.Add($"{rotulo}: tipo '{questao.Tipo}' desconhecido " +
                          $"(esperado: {string.Join(", ", Enum.GetNames<TipoDeQuestao>())}).");
        }
    }

    private static void ValidarOpcoes(QuestaoDeSeed questao, string rotulo, List<string> problemas)
    {
        var corretas = questao.Opcoes.Count(o => o.Correta);

        if (questao.Opcoes.Any(o => string.IsNullOrWhiteSpace(o.Texto)))
        {
            problemas.Add($"{rotulo}: alternativa com texto vazio.");
        }

        var textosRepetidos = questao.Opcoes
            .GroupBy(o => NormalizarTexto(o.Texto))
            .Where(g => g.Key.Length > 0 && g.Count() > 1)
            .Select(g => g.First().Texto);

        foreach (var texto in textosRepetidos)
        {
            problemas.Add($"{rotulo}: alternativa repetida ('{texto}').");
        }

        if (corretas == 0)
        {
            problemas.Add($"{rotulo}: nenhuma alternativa marcada como correta.");
        }

        if (!TentarConverterTipo(questao.Tipo, out var tipo))
        {
            return;
        }

        switch (tipo)
        {
            case TipoDeQuestao.EscolhaUnica when questao.Opcoes.Count != 4:
                problemas.Add($"{rotulo}: EscolhaUnica exige 4 alternativas (tem {questao.Opcoes.Count}).");
                break;

            case TipoDeQuestao.EscolhaUnica when corretas != 1:
                problemas.Add($"{rotulo}: EscolhaUnica exige exatamente 1 correta (tem {corretas}).");
                break;

            case TipoDeQuestao.EscolhaMultipla when questao.Opcoes.Count < 4:
                problemas.Add($"{rotulo}: EscolhaMultipla exige ao menos 4 alternativas (tem {questao.Opcoes.Count}).");
                break;

            case TipoDeQuestao.EscolhaMultipla when corretas < 2:
                problemas.Add($"{rotulo}: EscolhaMultipla exige ao menos 2 corretas (tem {corretas}).");
                break;

            case TipoDeQuestao.EscolhaMultipla when corretas >= questao.Opcoes.Count:
                problemas.Add($"{rotulo}: EscolhaMultipla com todas as alternativas corretas.");
                break;

            // O enunciado precisa dizer quantas marcar — a UI imprime "Escolha duas." a partir da
            // contagem, mas quem lê a questão tem de encontrar a instrução no texto também.
            case TipoDeQuestao.EscolhaMultipla when !MencionaQuantidade(questao.Enunciado):
                problemas.Add($"{rotulo}: EscolhaMultipla sem instrução de quantas alternativas selecionar.");
                break;

            case TipoDeQuestao.SimNao when questao.Opcoes.Count != 2:
                problemas.Add($"{rotulo}: SimNao exige exatamente 2 alternativas (tem {questao.Opcoes.Count}).");
                break;

            case TipoDeQuestao.SimNao when corretas != 1:
                problemas.Add($"{rotulo}: SimNao exige exatamente 1 correta (tem {corretas}).");
                break;
        }
    }

    private static bool MencionaQuantidade(string enunciado)
    {
        var texto = enunciado.ToLowerInvariant();
        return texto.Contains("duas") || texto.Contains("três") || texto.Contains("tres")
               || texto.Contains(" dois") || texto.Contains("selecione");
    }

    public static bool TentarConverterTipo(string tipo, out TipoDeQuestao resultado)
        => Enum.TryParse(tipo, ignoreCase: true, out resultado) && Enum.IsDefined(resultado);

    private static string NormalizarTexto(string? texto)
        => string.IsNullOrWhiteSpace(texto)
            ? string.Empty
            : string.Join(' ', texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
}
