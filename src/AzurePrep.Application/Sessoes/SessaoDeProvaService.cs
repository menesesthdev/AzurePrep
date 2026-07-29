using AzurePrep.Application.Abstractions;
using AzurePrep.Application.Contracts;
using AzurePrep.Application.Sorteios;
using AzurePrep.Domain.Entidades;
using AzurePrep.Domain.Correcao;

namespace AzurePrep.Application.Sessoes;

public sealed class SessaoDeProvaService : ISessaoDeProvaService
{
    private readonly IExameRepository _examRepository;
    private readonly ITentativaDeProvaRepository _attemptRepository;
    private readonly ISorteadorDeQuestoes _sorteador;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IUsuarioAtual _usuarioAtual;

    public SessaoDeProvaService(
        IExameRepository examRepository,
        ITentativaDeProvaRepository attemptRepository,
        ISorteadorDeQuestoes sorteador,
        IUnitOfWork unitOfWork,
        IClock clock,
        IUsuarioAtual usuarioAtual)
    {
        _examRepository = examRepository;
        _attemptRepository = attemptRepository;
        _sorteador = sorteador;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _usuarioAtual = usuarioAtual;
    }

    public async Task<Guid> IniciarTentativaAsync(Guid examId, CancellationToken cancellationToken = default)
    {
        var userId = _usuarioAtual.Id
                     ?? throw new InvalidOperationException("É preciso estar autenticado para iniciar uma tentativa.");

        var exam = await _examRepository.ObterPorIdAsync(examId, cancellationToken)
                   ?? throw new InvalidOperationException($"Exame {examId} não encontrado.");

        // A composição da prova é decidida agora e gravada com a tentativa: é o que garante que
        // reabrir o simulado mostre exatamente os mesmos itens, e o que permite ao próximo
        // sorteio saber o que esta pessoa já viu.
        var questionIds = await _sorteador.SortearAsync(exam.Id, userId, cancellationToken);

        var attempt = new TentativaDeProva(exam.Id, userId, _clock.UtcNow);
        attempt.DefinirQuestoes(questionIds);

        await _attemptRepository.AdicionarAsync(attempt, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return attempt.Id;
    }

    /// <summary>
    /// Carrega a tentativa só se ela pertencer a quem está logado. Devolver <c>null</c> para
    /// tentativa de outro dono faz o Web responder 404 — não confirma que o id existe.
    /// </summary>
    private async Task<TentativaDeProva?> ObterTentativaDoUsuarioAsync(Guid attemptId, CancellationToken cancellationToken)
    {
        var attempt = await _attemptRepository.ObterPorIdAsync(attemptId, cancellationToken);
        if (attempt is null || attempt.UserId != _usuarioAtual.Id)
        {
            return null;
        }

        return attempt;
    }

    public async Task<EstadoDaTentativaDto?> ObterEstadoAsync(Guid attemptId, CancellationToken cancellationToken = default)
    {
        var contexto = await CarregarContextoAsync(attemptId, cancellationToken);
        if (contexto is null)
        {
            return null;
        }

        var (attempt, exam, questions) = contexto.Value;
        var answersByQuestion = attempt.Answers.ToDictionary(a => a.QuestionId);

        var statuses = new List<StatusDaQuestaoDto>(questions.Count);
        for (var i = 0; i < questions.Count; i++)
        {
            var question = questions[i];
            answersByQuestion.TryGetValue(question.Id, out var answer);
            statuses.Add(new StatusDaQuestaoDto(
                question.Id,
                i + 1,
                answer?.IsAnswered ?? false,
                answer?.IsFlaggedForReview ?? false,
                answer?.SelectedOptionIds.Count ?? 0,
                SelecoesExigidas(question)));
        }

        return new EstadoDaTentativaDto(
            attempt.Id,
            exam.Code,
            exam.Name,
            exam.TimeLimitMinutes,
            attempt.StartedAt,
            CalcularSegundosRestantes(exam, attempt),
            attempt.IsFinished,
            statuses);
    }

    public async Task<QuestaoDto?> ObterQuestaoAsync(Guid attemptId, int number, CancellationToken cancellationToken = default)
    {
        var contexto = await CarregarContextoAsync(attemptId, cancellationToken);
        if (contexto is null)
        {
            return null;
        }

        var (attempt, _, questions) = contexto.Value;
        if (number < 1 || number > questions.Count)
        {
            return null;
        }

        var question = questions[number - 1];
        var answer = attempt.Answers.FirstOrDefault(a => a.QuestionId == question.Id);

        var options = question.Options
            .Select(o => new OpcaoDeQuestaoDto(o.Id, o.Text, o.OrderIndex))
            .ToList();

        return new QuestaoDto(
            question.Id,
            number,
            question.Text,
            question.Type,
            options,
            answer?.SelectedOptionIds.ToList() ?? new List<Guid>(),
            answer?.IsFlaggedForReview ?? false,
            questions.Count,
            SelecoesExigidas(question));
    }

    public async Task SalvarRespostaAsync(SalvarRespostaRequest request, CancellationToken cancellationToken = default)
    {
        var attempt = await ObterTentativaDoUsuarioAsync(request.AttemptId, cancellationToken)
                      ?? throw new InvalidOperationException($"Tentativa {request.AttemptId} não encontrada.");

        if (attempt.IsFinished)
        {
            return;
        }

        // Detecta se é a primeira resposta desta questão ANTES de mutar o agregado — respostas
        // novas precisam de Add explícito (chave gerada no domínio); atualizações o EF já rastreia.
        var isNewAnswer = attempt.Answers.All(a => a.QuestionId != request.QuestionId);

        var answer = attempt.DefinirResposta(
            request.QuestionId,
            request.SelectedOptionIds,
            request.IsFlaggedForReview,
            request.TimeSpentSeconds);

        if (isNewAnswer)
        {
            await _attemptRepository.AdicionarRespostaAsync(answer, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<ResultadoDaProvaDto?> FinalizarTentativaAsync(Guid attemptId, CancellationToken cancellationToken = default)
    {
        var contexto = await CarregarContextoAsync(attemptId, cancellationToken);
        if (contexto is null)
        {
            return null;
        }

        var (attempt, exam, questions) = contexto.Value;

        if (!attempt.IsFinished)
        {
            var score = CorretorDeProva.Corrigir(exam, questions, attempt.Answers);
            attempt.Concluir(score.ScorePercent, score.Passed, _clock.UtcNow);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return MontarResultado(exam, questions, attempt);
    }

    public async Task<ResultadoDaProvaDto?> ObterResultadoAsync(Guid attemptId, CancellationToken cancellationToken = default)
    {
        var contexto = await CarregarContextoAsync(attemptId, cancellationToken);
        if (contexto is null || !contexto.Value.Attempt.IsFinished)
        {
            return null;
        }

        var (attempt, exam, questions) = contexto.Value;
        return MontarResultado(exam, questions, attempt);
    }

    /// <summary>
    /// Tudo que qualquer operação da sessão precisa: a tentativa (já validada como do usuário),
    /// o exame com seus domínios e as questões DAQUELA prova, na ordem sorteada.
    /// </summary>
    private async Task<(TentativaDeProva Attempt, Exame Exam, IReadOnlyList<Questao> Questions)?> CarregarContextoAsync(
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var attempt = await ObterTentativaDoUsuarioAsync(attemptId, cancellationToken);
        if (attempt is null)
        {
            return null;
        }

        var exam = await _examRepository.ObterComAreasAsync(attempt.ExamId, cancellationToken);
        if (exam is null)
        {
            return null;
        }

        return (attempt, exam, await CarregarQuestoesAsync(attempt, cancellationToken));
    }

    /// <summary>
    /// As questões da tentativa, na ordem em que foram sorteadas.
    /// </summary>
    /// <remarks>
    /// O fallback é rede de segurança para tentativa sem composição gravada (anterior ao sorteio,
    /// quando a prova era literalmente "todas as questões do exame"). A migration reconstrói a
    /// composição dessas tentativas antigas justamente para que este caminho não seja usado — ele
    /// só sabe recuperar o que foi respondido, então item deixado em branco se perderia. O que ele
    /// impede é o desfecho pior: corrigir uma prova antiga de 8 itens contra um banco de centenas.
    /// </remarks>
    private async Task<IReadOnlyList<Questao>> CarregarQuestoesAsync(
        TentativaDeProva attempt,
        CancellationToken cancellationToken)
    {
        var ids = attempt.Questions.Select(q => q.QuestionId).ToList();

        if (ids.Count == 0)
        {
            var respondidas = attempt.Answers.Select(a => a.QuestionId).ToHashSet();
            var exam = await _examRepository.ObterComConteudoAsync(attempt.ExamId, cancellationToken);
            return exam is null
                ? Array.Empty<Questao>()
                : exam.Questions.Where(q => respondidas.Contains(q.Id)).OrderBy(q => q.Id).ToList();
        }

        var questions = await _examRepository.ObterQuestoesAsync(ids, cancellationToken);
        var byId = questions.ToDictionary(q => q.Id);

        return ids
            .Where(byId.ContainsKey)
            .Select(id => byId[id])
            .ToList();
    }

    // Quantas alternativas o candidato precisa marcar. Corresponde ao "Escolha duas." impresso
    // no enunciado da prova real — informa a quantidade, nunca quais são.
    private static int SelecoesExigidas(Questao question)
        => Math.Max(1, question.CorrectOptionIds.Count);

    private int CalcularSegundosRestantes(Exame exam, TentativaDeProva attempt)
    {
        if (attempt.IsFinished)
        {
            return 0;
        }

        var deadline = attempt.StartedAt.AddMinutes(exam.TimeLimitMinutes);
        var remaining = (int)Math.Floor((deadline - _clock.UtcNow).TotalSeconds);
        return Math.Max(0, remaining);
    }

    private static ResultadoDaProvaDto MontarResultado(Exame exam, IReadOnlyList<Questao> questions, TentativaDeProva attempt)
    {
        var score = CorretorDeProva.Corrigir(exam, questions, attempt.Answers);
        var answersByQuestion = attempt.Answers.ToDictionary(a => a.QuestionId);

        var skillAreas = score.SkillAreas
            .OrderByDescending(s => s.WeightPercent)
            .Select(s => new ResultadoPorAreaDto(
                s.SkillAreaName,
                s.WeightPercent,
                s.TotalQuestions,
                s.CorrectAnswers,
                s.ScorePercent))
            .ToList();

        var reviews = new List<RevisaoDeQuestaoDto>(questions.Count);
        for (var i = 0; i < questions.Count; i++)
        {
            var question = questions[i];
            answersByQuestion.TryGetValue(question.Id, out var answer);
            var selected = answer?.SelectedOptionIds.ToHashSet() ?? new HashSet<Guid>();

            var options = question.Options
                .Select(o => new RevisaoDeOpcaoDto(o.Text, o.IsCorrect, selected.Contains(o.Id)))
                .ToList();

            reviews.Add(new RevisaoDeQuestaoDto(
                i + 1,
                question.Text,
                question.Type,
                question.RespondidaCorretamentePor(selected),
                question.Explanation,
                options));
        }

        return new ResultadoDaProvaDto(
            attempt.Id,
            exam.Code,
            exam.Name,
            attempt.ScorePercent ?? score.ScorePercent,
            attempt.Passed ?? score.Passed,
            exam.PassingScorePercent,
            score.TotalQuestions,
            score.CorrectAnswers,
            attempt.StartedAt,
            attempt.FinishedAt ?? attempt.StartedAt,
            skillAreas,
            reviews,
            score.ScaledScore,
            EscalaDeNota.NotaDeCorte);
    }
}
