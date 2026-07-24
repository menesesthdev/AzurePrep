using AzurePrep.Application.Contracts;
using AzurePrep.Application.Sessoes;
using AzurePrep.Application.Tests.Fakes;
using AzurePrep.Domain.Entidades;
using AzurePrep.Domain.Enums;

namespace AzurePrep.Application.Tests;

public class SessaoDeProvaServiceTests
{
    private static readonly DateTime Iniciar = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    // Exame de 4 questões single-choice em 1 skill area; corte em 70%.
    private static Exame BuildExam()
    {
        var exam = new Exame("AZ-900", "Azure Fundamentals", timeLimitMinutes: 45, passingScorePercent: 70, totalQuestions: 4);
        var area = exam.AdicionarAreaDeHabilidade("Conceitos de nuvem", 100m);

        foreach (var i in Enumerable.Range(0, 4))
        {
            var q = exam.AdicionarQuestao(area.Id, $"Questão {i}", TipoDeQuestao.EscolhaUnica, "Explicação.");
            q.AdicionarOpcao("Correta", true, 0);
            q.AdicionarOpcao("Errada", false, 1);
        }

        return exam;
    }

    private static (SessaoDeProvaService service, Exame exam, FixedClock clock) BuildService()
    {
        var (service, exam, clock, _) = BuildServiceComUsuario();
        return (service, exam, clock);
    }

    private static (SessaoDeProvaService service, Exame exam, FixedClock clock, FakeUsuarioAtual usuario) BuildServiceComUsuario()
    {
        var exam = BuildExam();
        var clock = new FixedClock(Iniciar);
        var usuario = new FakeUsuarioAtual();
        var service = new SessaoDeProvaService(
            new InMemoryExamRepository(exam),
            new InMemoryExamAttemptRepository(),
            new FakeUnitOfWork(),
            clock,
            usuario);
        return (service, exam, clock, usuario);
    }

    private static Guid CorrectOption(QuestaoDto q) => q.Options.First().Id; // OrderIndex 0 = correta neste seed de teste

    [Fact]
    public async Task StartAttempt_CreatesAttemptWithFreshState()
    {
        var (service, exam, _) = BuildService();

        var attemptId = await service.IniciarTentativaAsync(exam.Id);
        var state = await service.ObterEstadoAsync(attemptId);

        Assert.NotNull(state);
        Assert.Equal("AZ-900", state!.ExamCode);
        Assert.Equal(4, state.Questions.Count);
        Assert.All(state.Questions, q => Assert.False(q.IsAnswered));
        Assert.Equal(45 * 60, state.RemainingSeconds);
    }

    [Fact]
    public async Task GetQuestion_OutOfRange_ReturnsNull()
    {
        var (service, exam, _) = BuildService();
        var attemptId = await service.IniciarTentativaAsync(exam.Id);

        Assert.Null(await service.ObterQuestaoAsync(attemptId, 0));
        Assert.Null(await service.ObterQuestaoAsync(attemptId, 5));
        Assert.NotNull(await service.ObterQuestaoAsync(attemptId, 1));
    }

    [Fact]
    public async Task SaveAnswer_MarksQuestionAnsweredInState()
    {
        var (service, exam, _) = BuildService();
        var attemptId = await service.IniciarTentativaAsync(exam.Id);
        var q1 = await service.ObterQuestaoAsync(attemptId, 1);

        await service.SalvarRespostaAsync(new SalvarRespostaRequest(
            attemptId, q1!.Id, new[] { CorrectOption(q1) }, IsFlaggedForReview: true, TimeSpentSeconds: 30));

        var state = await service.ObterEstadoAsync(attemptId);
        var status = state!.Questions.Single(s => s.QuestionId == q1.Id);
        Assert.True(status.IsAnswered);
        Assert.True(status.IsFlaggedForReview);
    }

    [Fact]
    public async Task RemainingSeconds_DecreasesAsTimePasses()
    {
        var (service, exam, clock) = BuildService();
        var attemptId = await service.IniciarTentativaAsync(exam.Id);

        clock.Advance(TimeSpan.FromMinutes(10));

        var state = await service.ObterEstadoAsync(attemptId);
        Assert.Equal(35 * 60, state!.RemainingSeconds);
    }

    [Fact]
    public async Task FinishAttempt_AllCorrect_PassesAndBuildsReview()
    {
        var (service, exam, _) = BuildService();
        var attemptId = await service.IniciarTentativaAsync(exam.Id);

        for (var n = 1; n <= 4; n++)
        {
            var q = await service.ObterQuestaoAsync(attemptId, n);
            await service.SalvarRespostaAsync(new SalvarRespostaRequest(
                attemptId, q!.Id, new[] { CorrectOption(q) }, false, 10));
        }

        var result = await service.FinalizarTentativaAsync(attemptId);

        Assert.NotNull(result);
        Assert.Equal(100m, result!.ScorePercent);
        Assert.True(result.Passed);
        Assert.Equal(4, result.CorrectAnswers);
        Assert.Equal(4, result.Questions.Count);
        Assert.All(result.Questions, q => Assert.True(q.WasCorrect));
        Assert.Single(result.SkillAreas);
    }

    [Fact]
    public async Task FinishAttempt_HalfCorrect_Fails()
    {
        var (service, exam, _) = BuildService();
        var attemptId = await service.IniciarTentativaAsync(exam.Id);

        for (var n = 1; n <= 4; n++)
        {
            var q = await service.ObterQuestaoAsync(attemptId, n);
            // Alterna correta/errada: 2 certas, 2 erradas => 50%.
            var optionId = n % 2 == 1 ? q!.Options.First().Id : q!.Options.Last().Id;
            await service.SalvarRespostaAsync(new SalvarRespostaRequest(attemptId, q.Id, new[] { optionId }, false, 10));
        }

        var result = await service.FinalizarTentativaAsync(attemptId);

        Assert.Equal(50m, result!.ScorePercent);
        Assert.False(result.Passed);
    }

    [Fact]
    public async Task SaveAnswer_AfterFinish_IsIgnored()
    {
        var (service, exam, _) = BuildService();
        var attemptId = await service.IniciarTentativaAsync(exam.Id);
        var q1 = await service.ObterQuestaoAsync(attemptId, 1);

        await service.FinalizarTentativaAsync(attemptId);

        // Não deve lançar nem alterar nada após finalizado.
        await service.SalvarRespostaAsync(new SalvarRespostaRequest(
            attemptId, q1!.Id, new[] { CorrectOption(q1) }, false, 10));

        var result = await service.ObterResultadoAsync(attemptId);
        Assert.NotNull(result);
        Assert.Equal(0m, result!.ScorePercent); // nenhuma resposta contou
    }

    [Fact]
    public async Task GetResult_WhileInProgress_ReturnsNull()
    {
        var (service, exam, _) = BuildService();
        var attemptId = await service.IniciarTentativaAsync(exam.Id);

        Assert.Null(await service.ObterResultadoAsync(attemptId));
    }

    [Fact]
    public async Task FinishAttempt_IsIdempotent()
    {
        var (service, exam, clock) = BuildService();
        var attemptId = await service.IniciarTentativaAsync(exam.Id);

        var first = await service.FinalizarTentativaAsync(attemptId);
        var finishedAt = first!.FinishedAt;

        clock.Advance(TimeSpan.FromMinutes(5));
        var second = await service.FinalizarTentativaAsync(attemptId);

        // Segundo finish não deve re-corrigir nem mudar o horário de finalização.
        Assert.Equal(finishedAt, second!.FinishedAt);
    }

    // ---- Isolamento entre usuários --------------------------------------
    // Ids de tentativa aparecem na URL. Sem a checagem de posse, trocar o Guid daria
    // acesso à prova de outra pessoa — inclusive para alterar respostas dela.

    [Fact]
    public async Task Attempt_DeOutroUsuario_NaoEhVisivel()
    {
        var (service, exam, _, usuario) = BuildServiceComUsuario();
        var attemptId = await service.IniciarTentativaAsync(exam.Id);

        usuario.Id = Guid.NewGuid(); // outra pessoa, mesma aplicação

        Assert.Null(await service.ObterEstadoAsync(attemptId));
        Assert.Null(await service.ObterQuestaoAsync(attemptId, 1));
        Assert.Null(await service.ObterResultadoAsync(attemptId));
    }

    [Fact]
    public async Task SalvarResposta_EmAttemptDeOutroUsuario_Falha()
    {
        var (service, exam, _, usuario) = BuildServiceComUsuario();
        var attemptId = await service.IniciarTentativaAsync(exam.Id);
        var question = await service.ObterQuestaoAsync(attemptId, 1);

        usuario.Id = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SalvarRespostaAsync(new SalvarRespostaRequest(
                attemptId,
                question!.Id,
                new[] { CorrectOption(question) },
                false,
                10)));
    }

    [Fact]
    public async Task IniciarTentativa_SemUsuarioLogado_Falha()
    {
        var (service, exam, _, usuario) = BuildServiceComUsuario();
        usuario.Id = null;

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.IniciarTentativaAsync(exam.Id));
    }
}
