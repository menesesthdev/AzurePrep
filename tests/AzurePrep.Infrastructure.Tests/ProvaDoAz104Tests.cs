using AzurePrep.Application.Abstractions;
using AzurePrep.Application.Contracts;
using AzurePrep.Application.Observabilidade;
using AzurePrep.Application.Sessoes;
using AzurePrep.Application.Sorteios;
using AzurePrep.Domain.Correcao;
using AzurePrep.Domain.Entidades;
using AzurePrep.Domain.Enums;
using AzurePrep.Infrastructure.Persistence;
using AzurePrep.Infrastructure.Persistence.Repositories;
using AzurePrep.Infrastructure.Time;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AzurePrep.Infrastructure.Tests;

/// <summary>
/// Exercita o banco do AZ-104 no pipeline de verdade: sorteio, apresentação, gravação de resposta,
/// correção e score report — contra SQLite real, com uma request por operação.
/// </summary>
/// <remarks>
/// Existe porque o AZ-104 está em construção e por isso <b>recusa tentativa em produção</b>: sem
/// este teste, as 108 questões teriam sido escritas, validadas quanto à forma e nunca respondidas.
/// Validação de catálogo confere estrutura (contagem de alternativas, gabarito presente, tipo
/// consistente); ela não diz se o conteúdo atravessa o sorteio, a correção e o placar por domínio.
/// A diferença aparece justamente nos tipos que o formato expande — uma questão de arrastar e
/// soltar vira 20 alternativas no banco, e nada além de responder uma de verdade prova que o
/// conjunto correto é reconhecido como acerto.
///
/// O exame é publicado <b>apenas dentro do banco deste teste</b>, o que é o ponto: dá para validar
/// o conteúdo antes de decidir publicá-lo para os usuários.
/// </remarks>
public sealed class ProvaDoAz104Tests : IDisposable
{
    private const string Codigo = "AZ-104";

    private readonly SqliteConnection _connection;
    private readonly IClock _clock = new SystemClock();
    private static readonly Guid _usuarioId = Guid.NewGuid();

    public ProvaDoAz104Tests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();
        AzurePrepDbSeeder.SemearAsync(ctx).GetAwaiter().GetResult();

        // Publica só neste banco em memória. A definição no seeder segue Publicado: false.
        var exame = ctx.Exams.Single(e => e.Code == Codigo);
        exame.AtualizarDefinicao(
            exame.Name, exame.TimeLimitMinutes, exame.PassingScorePercent, exame.TotalQuestions,
            isPublished: true);

        ctx.Users.Add(new Usuario(
            ProvedorDeLogin.Google, "provider-key-az104", "Candidato AZ-104",
            "az104@example.com", null, _clock.UtcNow, _usuarioId));
        ctx.SaveChanges();
    }

    public void Dispose() => _connection.Dispose();

    private AzurePrepDbContext CreateContext()
        => new(new DbContextOptionsBuilder<AzurePrepDbContext>().UseSqlite(_connection).Options);

    private (SessaoDeProvaService service, AzurePrepDbContext ctx) NewRequest()
    {
        var ctx = CreateContext();
        var exames = new ExameRepository(ctx);
        var tentativas = new TentativaDeProvaRepository(ctx);
        return (new SessaoDeProvaService(
            exames, tentativas,
            new SorteadorDeQuestoesService(exames, tentativas, new SementeFixa()),
            ctx, _clock, new FixedUsuarioAtual(_usuarioId),
            MetricasDeNegocioSilenciosas.Instancia), ctx);
    }

    private sealed class FixedUsuarioAtual(Guid id) : IUsuarioAtual
    {
        public Guid? Id { get; } = id;
    }

    private sealed class SementeFixa : IGeradorDeAleatoriedade
    {
        public Random Criar() => new(20260825);
    }

    private async Task<Exame> ObterExameAsync()
    {
        using var ctx = CreateContext();
        return await ctx.Exams.Include(e => e.SkillAreas).SingleAsync(e => e.Code == Codigo);
    }

    private async Task<Guid> IniciarAsync()
    {
        var exame = await ObterExameAsync();
        var (service, ctx) = NewRequest();
        using (ctx) return await service.IniciarTentativaAsync(exame.Id);
    }

    // ------------------------------------------------------------------ composição da prova

    [Fact]
    public async Task Prova_TemExatamenteOTamanhoDeclarado()
    {
        var exame = await ObterExameAsync();
        var attemptId = await IniciarAsync();

        using var ctx = CreateContext();
        var sorteadas = await ctx.ExamAttemptQuestions
            .Where(q => q.ExamAttemptId == attemptId)
            .ToListAsync();

        Assert.Equal(exame.TotalQuestions, sorteadas.Count);
        Assert.Equal(sorteadas.Count, sorteadas.Select(q => q.QuestionId).Distinct().Count());
    }

    /// <summary>
    /// A repartição por domínio segue o peso do Skills Measured, não o tamanho do pool.
    /// </summary>
    /// <remarks>
    /// A tolerância de ±1 item existe porque a repartição é pelo método do maior resto e os pesos
    /// somam 92,5 (pontos médios das faixas oficiais), então a cota exata quase nunca é inteira.
    /// O que o teste garante é o que importa: nenhum domínio some, nenhum domina, e a distribuição
    /// acompanha o blueprint mesmo com os cinco domínios tendo pools de tamanho parecido — que é
    /// exatamente a situação em que um sorteio ingênuo, proporcional ao pool, passaria despercebido.
    /// </remarks>
    [Fact]
    public async Task Prova_RepartePorDominioSegundoOPesoDoBlueprint()
    {
        var exame = await ObterExameAsync();
        var attemptId = await IniciarAsync();

        using var ctx = CreateContext();
        var porArea = await ctx.ExamAttemptQuestions
            .Where(q => q.ExamAttemptId == attemptId)
            .Join(ctx.Questions, a => a.QuestionId, q => q.Id, (_, q) => q.SkillAreaId)
            .GroupBy(id => id)
            .Select(g => new { AreaId = g.Key, Total = g.Count() })
            .ToListAsync();

        var pesoTotal = exame.SkillAreas.Sum(a => a.WeightPercent);

        Assert.Equal(exame.SkillAreas.Count, porArea.Count);

        foreach (var area in exame.SkillAreas)
        {
            var esperado = area.WeightPercent / pesoTotal * exame.TotalQuestions;
            var obtido = porArea.Single(p => p.AreaId == area.Id).Total;

            Assert.True(
                Math.Abs(obtido - esperado) <= 1m,
                $"{area.Key}: {obtido} itens, esperado ~{esperado:0.0}");
        }
    }

    /// <summary>
    /// Os quatro formatos chegam à prova — em especial o arrastar e soltar.
    /// </summary>
    /// <remarks>
    /// É o formato com maior superfície para falhar em silêncio: ele não existe como linha própria
    /// no banco, e sim como todas as combinações de alvo com item. Se a expansão quebrasse, a
    /// questão continuaria sendo sorteada e apresentada — só nunca poderia ser acertada.
    /// </remarks>
    [Fact]
    public async Task Prova_ContemOsQuatroFormatos()
    {
        var attemptId = await IniciarAsync();

        using var ctx = CreateContext();
        var tipos = await ctx.ExamAttemptQuestions
            .Where(q => q.ExamAttemptId == attemptId)
            .Join(ctx.Questions, a => a.QuestionId, q => q.Id, (_, q) => q.Type)
            .Distinct()
            .ToListAsync();

        Assert.Contains(TipoDeQuestao.EscolhaUnica, tipos);
        Assert.Contains(TipoDeQuestao.EscolhaMultipla, tipos);
        Assert.Contains(TipoDeQuestao.SimNao, tipos);
        Assert.Contains(TipoDeQuestao.Associacao, tipos);
    }

    // ------------------------------------------------------------------------- fluxo completo

    private async Task<Dictionary<Guid, List<Guid>>> GabaritoAsync(bool corretas)
    {
        using var ctx = CreateContext();
        var questoes = await ctx.Questions
            .Include(q => q.Options)
            .Where(q => ctx.Exams.Any(e => e.Id == q.ExamId && e.Code == Codigo))
            .ToListAsync();

        return questoes.ToDictionary(
            q => q.Id,
            q => q.Options.Where(o => o.IsCorrect == corretas).Select(o => o.Id).ToList());
    }

    private async Task<ResultadoDaProvaDto> ResponderTudoAsync(bool corretamente)
    {
        var attemptId = await IniciarAsync();
        var gabarito = await GabaritoAsync(corretas: true);

        int total;
        {
            var (service, ctx) = NewRequest();
            using (ctx) total = (await service.ObterEstadoAsync(attemptId))!.Questions.Count;
        }

        for (var n = 1; n <= total; n++)
        {
            var (service, ctx) = NewRequest();
            using (ctx)
            {
                var q = await service.ObterQuestaoAsync(attemptId, n);
                var corretas = gabarito[q!.Id];

                // Para errar, marca uma alternativa que não está no gabarito. Vale para todos os
                // tipos: acertar exige o conjunto EXATO, então qualquer conjunto diferente erra.
                var selecao = corretamente
                    ? corretas
                    : q.Options.Select(o => o.Id).Where(id => !corretas.Contains(id)).Take(1).ToList();

                await service.SalvarRespostaAsync(
                    new SalvarRespostaRequest(attemptId, q.Id, selecao, false, 12));
            }
        }

        var (svc, c) = NewRequest();
        using (c) return (await svc.FinalizarTentativaAsync(attemptId))!;
    }

    [Fact]
    public async Task ProvaInteira_RespondidaCorretamente_Pontua100EAprova()
    {
        var resultado = await ResponderTudoAsync(corretamente: true);

        Assert.Equal(Codigo, resultado.ExamCode);
        Assert.Equal(100m, resultado.ScorePercent);
        Assert.True(resultado.Passed);
        Assert.Equal(resultado.TotalQuestions, resultado.CorrectAnswers);
        Assert.Equal(EscalaDeNota.NotaMaxima, resultado.ScaledScore);
        Assert.Equal(EscalaDeNota.NotaDeCorte, resultado.ScaledPassingScore);
    }

    [Fact]
    public async Task ProvaInteira_RespondidaErrada_ZeraEReprova()
    {
        var resultado = await ResponderTudoAsync(corretamente: false);

        Assert.Equal(0m, resultado.ScorePercent);
        Assert.False(resultado.Passed);
        Assert.Equal(0, resultado.CorrectAnswers);
        Assert.True(resultado.ScaledScore < EscalaDeNota.NotaDeCorte);
    }

    /// <summary>
    /// O score report traz os cinco domínios, e a soma dos itens por domínio fecha com a prova.
    /// </summary>
    [Fact]
    public async Task ScoreReport_CobreOsCincoDominios()
    {
        var exame = await ObterExameAsync();
        var resultado = await ResponderTudoAsync(corretamente: true);

        Assert.Equal(exame.SkillAreas.Count, resultado.SkillAreas.Count);
        Assert.Equal(resultado.TotalQuestions, resultado.SkillAreas.Sum(a => a.TotalQuestions));
        Assert.All(resultado.SkillAreas, a => Assert.Equal(100m, a.ScorePercent));
        Assert.All(resultado.SkillAreas, a => Assert.True(a.TotalQuestions > 0, $"{a.Name} sem itens"));
    }

    /// <summary>
    /// A revisão de estudo traz explicação em toda questão, inclusive nas de arrastar e soltar.
    /// </summary>
    /// <remarks>
    /// É a tela que justifica o simulado ensinar em vez de só pontuar. Explicação vazia passaria
    /// pela validação de catálogo apenas se tivesse ao menos 120 caracteres — o que este teste
    /// confirma é que ela sobrevive ao caminho até o DTO, e que o gabarito exibido não vem vazio.
    /// </remarks>
    [Fact]
    public async Task RevisaoDeEstudo_TrazExplicacaoEGabaritoEmTodaQuestao()
    {
        var resultado = await ResponderTudoAsync(corretamente: true);

        Assert.Equal(resultado.TotalQuestions, resultado.Questions.Count);
        Assert.All(resultado.Questions, q =>
        {
            Assert.False(string.IsNullOrWhiteSpace(q.Explanation), $"questão {q.Number} sem explicação");
            Assert.Contains(q.Options, o => o.IsCorrect);
            Assert.True(q.WasCorrect, $"questão {q.Number} marcada como errada numa prova 100% correta");
        });

        // Nas de arrastar, a revisão mostra o alvo de cada par — sem isso o gabarito fica ilegível.
        var arrastar = resultado.Questions.Where(q => q.Type == TipoDeQuestao.Associacao).ToList();
        Assert.NotEmpty(arrastar);
        Assert.All(arrastar, q => Assert.All(q.Options, o =>
            Assert.False(string.IsNullOrWhiteSpace(o.TargetText))));
    }
}
