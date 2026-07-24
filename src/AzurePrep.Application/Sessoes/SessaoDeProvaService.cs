using AzurePrep.Application.Abstractions;
using AzurePrep.Application.Contracts;
using AzurePrep.Domain.Entidades;
using AzurePrep.Domain.Correcao;

namespace AzurePrep.Application.Sessoes;

public sealed class SessaoDeProvaService : ISessaoDeProvaService
{
    private readonly IExameRepository _examRepository;
    private readonly ITentativaDeProvaRepository _attemptRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IUsuarioAtual _usuarioAtual;

    public SessaoDeProvaService(
        IExameRepository examRepository,
        ITentativaDeProvaRepository attemptRepository,
        IUnitOfWork unitOfWork,
        IClock clock,
        IUsuarioAtual usuarioAtual)
    {
        _examRepository = examRepository;
        _attemptRepository = attemptRepository;
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

        var attempt = new TentativaDeProva(exam.Id, userId, _clock.UtcNow);
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
        var attempt = await ObterTentativaDoUsuarioAsync(attemptId, cancellationToken);
        if (attempt is null)
        {
            return null;
        }

        var exam = await _examRepository.ObterComConteudoAsync(attempt.ExamId, cancellationToken);
        if (exam is null)
        {
            return null;
        }

        var questions = QuestoesOrdenadas(exam);
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
        var attempt = await ObterTentativaDoUsuarioAsync(attemptId, cancellationToken);
        if (attempt is null)
        {
            return null;
        }

        var exam = await _examRepository.ObterComConteudoAsync(attempt.ExamId, cancellationToken);
        if (exam is null)
        {
            return null;
        }

        var questions = QuestoesOrdenadas(exam);
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
        var attempt = await ObterTentativaDoUsuarioAsync(attemptId, cancellationToken);
        if (attempt is null)
        {
            return null;
        }

        var exam = await _examRepository.ObterComConteudoAsync(attempt.ExamId, cancellationToken);
        if (exam is null)
        {
            return null;
        }

        var questions = QuestoesOrdenadas(exam);

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
        var attempt = await ObterTentativaDoUsuarioAsync(attemptId, cancellationToken);
        if (attempt is null || !attempt.IsFinished)
        {
            return null;
        }

        var exam = await _examRepository.ObterComConteudoAsync(attempt.ExamId, cancellationToken);
        if (exam is null)
        {
            return null;
        }

        return MontarResultado(exam, QuestoesOrdenadas(exam), attempt);
    }

    // Ordem estável das questões dentro de uma tentativa. Por ora usa todas as questões do
    // exame, ordenadas por Id (determinístico com o seed). Quando o banco passar a ter mais
    // questões que TotalQuestions, aqui entra a seleção/embaralhamento por tentativa.
    private static IReadOnlyList<Questao> QuestoesOrdenadas(Exame exam)
        => exam.Questions.OrderBy(q => q.Id).ToList();

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
