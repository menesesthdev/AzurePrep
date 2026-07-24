using AzurePrep.Application.Abstractions;
using AzurePrep.Application.Contracts;
using AzurePrep.Application.Sessoes;
using AzurePrep.Domain.Entidades;
using AzurePrep.Domain.Enums;
using AzurePrep.Infrastructure.Persistence;
using AzurePrep.Infrastructure.Persistence.Repositories;
using AzurePrep.Infrastructure.Time;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AzurePrep.Infrastructure.Tests;

/// <summary>
/// Testes de integração contra SQLite real, usando um novo DbContext por operação para
/// reproduzir o ciclo request-por-request da web. É aqui que se pega o bug de gravação de
/// respostas (INSERT x UPDATE com chave gerada no domínio).
/// </summary>
public sealed class SessaoDeProvaPersistenceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IClock _clock = new SystemClock();

    public SessaoDeProvaPersistenceTests()
    {
        // Connection in-memory mantida aberta => o banco sobrevive entre múltiplos DbContexts.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();
        AzurePrepDbSeeder.SemearAsync(ctx).GetAwaiter().GetResult();

        // A tentativa tem FK obrigatória para Users, e o SQLite valida FK — o dono
        // precisa existir antes de qualquer tentativa ser gravada.
        ctx.Users.Add(new Usuario(
            ProvedorDeLogin.Google,
            providerKey: "provider-key-de-teste",
            name: "Candidato de Teste",
            email: "teste@example.com",
            avatarUrl: null,
            createdAt: _clock.UtcNow,
            id: _usuarioId));
        ctx.SaveChanges();
    }

    private AzurePrepDbContext CreateContext()
        => new(new DbContextOptionsBuilder<AzurePrepDbContext>().UseSqlite(_connection).Options);

    // Cada "request" recebe seu próprio contexto e serviço (como no ciclo scoped da web).
    // Todas as "requests" do teste representam a mesma pessoa logada.
    private static readonly Guid _usuarioId = Guid.NewGuid();
    private readonly IUsuarioAtual _usuario = new FixedUsuarioAtual(_usuarioId);

    private (SessaoDeProvaService service, AzurePrepDbContext ctx) NewRequest()
    {
        var ctx = CreateContext();
        return (new SessaoDeProvaService(
            new ExameRepository(ctx),
            new TentativaDeProvaRepository(ctx),
            ctx,
            _clock,
            _usuario), ctx);
    }

    private sealed class FixedUsuarioAtual(Guid id) : IUsuarioAtual
    {
        public Guid? Id { get; } = id;
    }

    private async Task<Guid> GetSeededExamIdAsync()
    {
        using var ctx = CreateContext();
        return (await ctx.Exams.FirstAsync()).Id;
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Seed_CreatesAz900WithEightQuestionsAndThreeSkillAreas()
    {
        using var ctx = CreateContext();

        var exam = await ctx.Exams
            .Include(e => e.SkillAreas)
            .Include(e => e.Questions).ThenInclude(q => q.Options)
            .SingleAsync();

        Assert.Equal("AZ-900", exam.Code);
        Assert.Equal(3, exam.SkillAreas.Count);
        Assert.Equal(8, exam.Questions.Count);
        Assert.All(exam.Questions, q => Assert.True(q.Options.Count >= 2));
        // Toda questão tem ao menos uma alternativa correta.
        Assert.All(exam.Questions, q => Assert.Contains(q.Options, o => o.IsCorrect));
    }

    [Fact]
    public async Task SaveAnswer_NewThenUpdate_PersistsSingleRowAcrossContexts()
    {
        var examId = await GetSeededExamIdAsync();

        // 1) Iniciar tentativa (contexto próprio).
        Guid attemptId;
        {
            var (service, ctx) = NewRequest();
            using (ctx) attemptId = await service.IniciarTentativaAsync(examId);
        }

        // 2) Carregar a questão 1 e capturar duas opções.
        Guid questionId, firstOption, secondOption;
        {
            var (service, ctx) = NewRequest();
            using (ctx)
            {
                var q = await service.ObterQuestaoAsync(attemptId, 1);
                Assert.NotNull(q);
                questionId = q!.Id;
                firstOption = q.Options[0].Id;
                secondOption = q.Options[1].Id;
            }
        }

        // 3) Primeira gravação (INSERT) — cenário que disparava o erro de concorrência.
        {
            var (service, ctx) = NewRequest();
            using (ctx)
            {
                await service.SalvarRespostaAsync(new SalvarRespostaRequest(
                    attemptId, questionId, new[] { firstOption }, IsFlaggedForReview: false, TimeSpentSeconds: 10));
            }
        }

        // 4) Segunda gravação da MESMA questão (UPDATE): muda seleção e marca para revisão.
        {
            var (service, ctx) = NewRequest();
            using (ctx)
            {
                await service.SalvarRespostaAsync(new SalvarRespostaRequest(
                    attemptId, questionId, new[] { secondOption }, IsFlaggedForReview: true, TimeSpentSeconds: 5));
            }
        }

        // 5) Verificar no banco: exatamente uma resposta, com a seleção final e a flag.
        using (var ctx = CreateContext())
        {
            var answers = await ctx.ExamAttemptAnswers
                .Where(a => a.ExamAttemptId == attemptId)
                .ToListAsync();

            var answer = Assert.Single(answers);
            Assert.Equal(new[] { secondOption }, answer.SelectedOptionIds);
            Assert.True(answer.IsFlaggedForReview);
            Assert.Equal(15, answer.TimeSpentSeconds); // 10 + 5 acumulados
        }
    }

    [Fact]
    public async Task FullAttempt_AnswerAllCorrectly_ScoresHundredAndPasses()
    {
        var examId = await GetSeededExamIdAsync();

        // Gabarito: opções corretas por questão (direto do banco).
        Dictionary<Guid, List<Guid>> correctByQuestion;
        using (var ctx = CreateContext())
        {
            correctByQuestion = (await ctx.Questions
                    .Include(q => q.Options)
                    .ToListAsync())
                .ToDictionary(q => q.Id, q => q.Options.Where(o => o.IsCorrect).Select(o => o.Id).ToList());
        }

        Guid attemptId;
        {
            var (service, ctx) = NewRequest();
            using (ctx) attemptId = await service.IniciarTentativaAsync(examId);
        }

        // Responde cada questão corretamente (uma request por questão).
        for (var n = 1; n <= correctByQuestion.Count; n++)
        {
            var (service, ctx) = NewRequest();
            using (ctx)
            {
                var q = await service.ObterQuestaoAsync(attemptId, n);
                await service.SalvarRespostaAsync(new SalvarRespostaRequest(
                    attemptId, q!.Id, correctByQuestion[q.Id], false, 8));
            }
        }

        ResultadoDaProvaDto? result;
        {
            var (service, ctx) = NewRequest();
            using (ctx) result = await service.FinalizarTentativaAsync(attemptId);
        }

        Assert.NotNull(result);
        Assert.Equal(100m, result!.ScorePercent);
        Assert.True(result.Passed);
        Assert.Equal(result.TotalQuestions, result.CorrectAnswers);
    }
}
