using AzurePrep.Infrastructure.Persistence.Seed;
using Xunit;

namespace AzurePrep.Infrastructure.Tests;

/// <summary>
/// A validação do catálogo é a última barreira antes de um gabarito errado chegar ao usuário — e
/// só era exercitada de lado, pelo seed que lança quando ela reclama. Isso deixava passar a
/// regressão que importa: uma <c>Validar</c> que parasse de reclamar não quebraria teste nenhum.
/// Aqui cada regra é verificada pelo que ela precisa REJEITAR.
/// </summary>
public class CatalogoDeQuestoesDeSeedTests
{
    private const string AreaConhecida = "conceitos-de-nuvem";

    private static readonly string[] Areas = { AreaConhecida };

    // O validador exige explicação longa o bastante para justificar a correta e cada distrator.
    private const string ExplicacaoValida =
        "A alternativa correta é a primeira porque descreve exatamente o comportamento do serviço no " +
        "cenário apresentado. A segunda confunde com um recurso parecido, a terceira resolve um " +
        "problema diferente e a quarta contraria a restrição enunciada.";

    // ------------------------------------------------------------------- catálogo de verdade

    [Fact]
    public void Carregar_LeTodosOsLotesEmbutidosNoAssembly()
    {
        var arquivos = CatalogoDeQuestoesDeSeed.Carregar();

        Assert.NotEmpty(arquivos);
        Assert.All(arquivos, a => Assert.False(string.IsNullOrWhiteSpace(a.ExameCode)));
        Assert.All(arquivos, a => Assert.False(string.IsNullOrWhiteSpace(a.Area)));
        Assert.All(arquivos, a => Assert.NotEmpty(a.Questoes));
    }

    [Fact]
    public void Validar_LoteBemFormado_NaoAcusaNada()
    {
        var problemas = CatalogoDeQuestoesDeSeed.Validar(new[] { Lote(QuestaoValida()) }, Areas);

        Assert.Empty(problemas);
    }

    // ------------------------------------------------------------------------- lote e chaves

    [Fact]
    public void Validar_AreaInexistenteNoExame_Acusa()
    {
        var lote = Lote(QuestaoValida()) with { Area = "dominio-que-nao-existe" };

        Assert.Contains(CatalogoDeQuestoesDeSeed.Validar(new[] { lote }, Areas), p => p.Contains("não existe no exame"));
    }

    [Fact]
    public void Validar_LoteSemQuestoes_Acusa()
    {
        var lote = Lote() with { Questoes = Array.Empty<QuestaoDeSeed>() };

        Assert.Contains(CatalogoDeQuestoesDeSeed.Validar(new[] { lote }, Areas), p => p.Contains("nenhum item"));
    }

    [Fact]
    public void Validar_MesmoIdEmDoisArquivos_Acusa()
    {
        // O Guid da questão é derivado do id: chave repetida faria dois enunciados disputarem a
        // mesma linha do banco, e o último a ser importado sobrescreveria o outro.
        var primeiro = Lote(QuestaoValida() with { Id = "az900-repetida" }) with { Origem = "lote-a.json" };
        var segundo = Lote(QuestaoValida() with { Id = "az900-repetida", Enunciado = "Outro enunciado?" })
            with { Origem = "lote-b.json" };

        Assert.Contains(
            CatalogoDeQuestoesDeSeed.Validar(new[] { primeiro, segundo }, Areas),
            p => p.Contains("id repetido"));
    }

    [Fact]
    public void Validar_EnunciadoDuplicadoEntreArquivos_Acusa()
    {
        var primeiro = Lote(QuestaoValida() with { Id = "az900-a" });
        var segundo = Lote(QuestaoValida() with { Id = "az900-b" });

        Assert.Contains(
            CatalogoDeQuestoesDeSeed.Validar(new[] { primeiro, segundo }, Areas),
            p => p.Contains("enunciado duplicado"));
    }

    // ----------------------------------------------------------------------------- conteúdo

    [Fact]
    public void Validar_EnunciadoVazio_Acusa()
    {
        var lote = Lote(QuestaoValida() with { Enunciado = "   " });

        Assert.Contains(CatalogoDeQuestoesDeSeed.Validar(new[] { lote }, Areas), p => p.Contains("enunciado vazio"));
    }

    [Fact]
    public void Validar_ExplicacaoVazia_Acusa()
    {
        var lote = Lote(QuestaoValida() with { Explicacao = "" });

        Assert.Contains(CatalogoDeQuestoesDeSeed.Validar(new[] { lote }, Areas), p => p.Contains("explicação vazia"));
    }

    [Fact]
    public void Validar_ExplicacaoQueSoJustificaOGabarito_Acusa()
    {
        // Explicação por distrator é o que faz o simulado ensinar em vez de só pontuar.
        var lote = Lote(QuestaoValida() with { Explicacao = "A primeira está correta." });

        Assert.Contains(CatalogoDeQuestoesDeSeed.Validar(new[] { lote }, Areas), p => p.Contains("curta demais"));
    }

    [Fact]
    public void Validar_TipoDesconhecido_Acusa()
    {
        var lote = Lote(QuestaoValida() with { Tipo = "Dissertativa" });

        Assert.Contains(CatalogoDeQuestoesDeSeed.Validar(new[] { lote }, Areas), p => p.Contains("desconhecido"));
    }

    // -------------------------------------------------------------------------- alternativas

    [Fact]
    public void Validar_SemGabarito_Acusa()
    {
        var lote = Lote(QuestaoValida() with
        {
            Opcoes = Opcoes(("Primeira", false), ("Segunda", false), ("Terceira", false), ("Quarta", false))
        });

        Assert.Contains(
            CatalogoDeQuestoesDeSeed.Validar(new[] { lote }, Areas),
            p => p.Contains("nenhuma alternativa marcada como correta"));
    }

    [Fact]
    public void Validar_AlternativaComTextoVazio_Acusa()
    {
        var lote = Lote(QuestaoValida() with
        {
            Opcoes = Opcoes(("Primeira", true), ("", false), ("Terceira", false), ("Quarta", false))
        });

        Assert.Contains(CatalogoDeQuestoesDeSeed.Validar(new[] { lote }, Areas), p => p.Contains("texto vazio"));
    }

    [Fact]
    public void Validar_AlternativaRepetida_Acusa()
    {
        var lote = Lote(QuestaoValida() with
        {
            Opcoes = Opcoes(("Primeira", true), ("Segunda", false), ("segunda", false), ("Quarta", false))
        });

        Assert.Contains(CatalogoDeQuestoesDeSeed.Validar(new[] { lote }, Areas), p => p.Contains("alternativa repetida"));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    public void Validar_EscolhaUnicaSemQuatroAlternativas_Acusa(int quantidade)
    {
        var opcoes = Enumerable.Range(0, quantidade)
            .Select(i => new OpcaoDeSeed { Texto = $"Alternativa {i}", Correta = i == 0 })
            .ToList();

        var lote = Lote(QuestaoValida() with { Opcoes = opcoes });

        Assert.Contains(CatalogoDeQuestoesDeSeed.Validar(new[] { lote }, Areas), p => p.Contains("exige 4 alternativas"));
    }

    [Fact]
    public void Validar_EscolhaUnicaComDuasCorretas_Acusa()
    {
        var lote = Lote(QuestaoValida() with
        {
            Opcoes = Opcoes(("Primeira", true), ("Segunda", true), ("Terceira", false), ("Quarta", false))
        });

        Assert.Contains(
            CatalogoDeQuestoesDeSeed.Validar(new[] { lote }, Areas),
            p => p.Contains("exatamente 1 correta"));
    }

    [Fact]
    public void Validar_EscolhaMultiplaComUmaSoCorreta_Acusa()
    {
        var lote = Lote(QuestaoMultipla() with
        {
            Opcoes = Opcoes(("Primeira", true), ("Segunda", false), ("Terceira", false), ("Quarta", false))
        });

        Assert.Contains(CatalogoDeQuestoesDeSeed.Validar(new[] { lote }, Areas), p => p.Contains("ao menos 2 corretas"));
    }

    [Fact]
    public void Validar_EscolhaMultiplaComTodasCorretas_Acusa()
    {
        var lote = Lote(QuestaoMultipla() with
        {
            Opcoes = Opcoes(("Primeira", true), ("Segunda", true), ("Terceira", true), ("Quarta", true))
        });

        Assert.Contains(
            CatalogoDeQuestoesDeSeed.Validar(new[] { lote }, Areas),
            p => p.Contains("todas as alternativas corretas"));
    }

    [Fact]
    public void Validar_EscolhaMultiplaSemDizerQuantasMarcar_Acusa()
    {
        // A UI imprime "Escolha duas." a partir da contagem, mas quem lê o enunciado tem de
        // encontrar a instrução no texto também — é assim na prova real.
        var lote = Lote(QuestaoMultipla() with { Enunciado = "Quais servicos atendem ao requisito?" });

        Assert.Contains(
            CatalogoDeQuestoesDeSeed.Validar(new[] { lote }, Areas),
            p => p.Contains("sem instrução de quantas alternativas"));
    }

    [Fact]
    public void Validar_SimNaoSemDuasAlternativas_Acusa()
    {
        var lote = Lote(QuestaoValida() with { Tipo = "SimNao" });

        Assert.Contains(
            CatalogoDeQuestoesDeSeed.Validar(new[] { lote }, Areas),
            p => p.Contains("SimNao exige exatamente 2 alternativas"));
    }

    [Fact]
    public void Validar_AcumulaTodosOsProblemasEmVezDePararNoPrimeiro()
    {
        // Devolver a lista inteira é o que deixa corrigir um lote de uma vez, em vez de descobrir
        // um erro por execução.
        var lote = Lote(QuestaoValida() with { Enunciado = "", Explicacao = "", Tipo = "Dissertativa" });

        Assert.True(CatalogoDeQuestoesDeSeed.Validar(new[] { lote }, Areas).Count >= 3);
    }

    // --------------------------------------------------------------------------- auxiliares

    private static ArquivoDeQuestoes Lote(params QuestaoDeSeed[] questoes) => new()
    {
        Origem = "lote-de-teste.json",
        ExameCode = "AZ-900",
        Area = AreaConhecida,
        Questoes = questoes
    };

    private static QuestaoDeSeed QuestaoValida() => new()
    {
        Id = "az900-teste-01",
        Topico = "Modelos de serviço",
        Tipo = "EscolhaUnica",
        Enunciado = "Uma empresa precisa de X restrição. Qual abordagem atende ao requisito?",
        Explicacao = ExplicacaoValida,
        Opcoes = Opcoes(("Primeira", true), ("Segunda", false), ("Terceira", false), ("Quarta", false))
    };

    private static QuestaoDeSeed QuestaoMultipla() => QuestaoValida() with
    {
        Id = "az900-teste-multipla-01",
        Tipo = "EscolhaMultipla",
        Enunciado = "Uma equipe precisa de X restrição. Selecione duas opções que atendem ao requisito.",
        Opcoes = Opcoes(("Primeira", true), ("Segunda", true), ("Terceira", false), ("Quarta", false))
    };

    private static IReadOnlyList<OpcaoDeSeed> Opcoes(params (string Texto, bool Correta)[] opcoes)
        => opcoes.Select(o => new OpcaoDeSeed { Texto = o.Texto, Correta = o.Correta }).ToList();
}
