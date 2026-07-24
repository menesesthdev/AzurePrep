using AzurePrep.Application.Contracts;
using AzurePrep.Application.Sessoes;
using AzurePrep.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace AzurePrep.Web.Controllers;

[Route("exam")]
public class ExameController : Controller
{
    private readonly ISessaoDeProvaService _session;

    public ExameController(ISessaoDeProvaService session)
    {
        _session = session;
    }

    // Inicia uma nova tentativa e leva para a tela de prova.
    [HttpPost("start")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Iniciar(Guid examId, CancellationToken cancellationToken)
    {
        var attemptId = await _session.IniciarTentativaAsync(examId, cancellationToken);
        return RedirectToAction(nameof(Realizar), new { attemptId });
    }

    // Shell da prova: header fixo, painel de navegação e a primeira questão.
    [HttpGet("{attemptId:guid}")]
    public async Task<IActionResult> Realizar(Guid attemptId, CancellationToken cancellationToken)
    {
        var state = await _session.ObterEstadoAsync(attemptId, cancellationToken);
        if (state is null)
        {
            return NotFound();
        }

        if (state.IsFinished)
        {
            return RedirectToAction(nameof(Resultado), new { attemptId });
        }

        var firstQuestion = await _session.ObterQuestaoAsync(attemptId, 1, cancellationToken);
        if (firstQuestion is null)
        {
            return NotFound();
        }

        return View(new RealizarProvaViewModel(state, firstQuestion));
    }

    // Partial de uma questão (navegação sem recarregar a página — fidelidade estilo SPA).
    [HttpGet("{attemptId:guid}/question/{number:int}")]
    public async Task<IActionResult> Questao(Guid attemptId, int number, CancellationToken cancellationToken)
    {
        var question = await _session.ObterQuestaoAsync(attemptId, number, cancellationToken);
        if (question is null)
        {
            return NotFound();
        }

        return PartialView("_Questao", question);
    }

    // Estado atual (painel lateral + tempo restante) em JSON para o front-end sincronizar.
    [HttpGet("{attemptId:guid}/state")]
    public async Task<IActionResult> State(Guid attemptId, CancellationToken cancellationToken)
    {
        var state = await _session.ObterEstadoAsync(attemptId, cancellationToken);
        return state is null ? NotFound() : Json(state);
    }

    // Grava/atualiza a resposta de uma questão (AJAX).
    // NOTA: sem antiforgery por ora — não há autenticação no escopo atual. Endurecer ao evoluir.
    [HttpPost("{attemptId:guid}/answer")]
    public async Task<IActionResult> SalvarResposta(Guid attemptId, [FromBody] SalvarRespostaInput input, CancellationToken cancellationToken)
    {
        if (input is null)
        {
            return BadRequest();
        }

        await _session.SalvarRespostaAsync(
            new SalvarRespostaRequest(
                attemptId,
                input.QuestionId,
                input.SelectedOptionIds,
                input.IsFlaggedForReview,
                input.TimeSpentSeconds),
            cancellationToken);

        return Ok();
    }

    // Finaliza e corrige. Retorna a URL do resultado para o front-end navegar
    // (funciona tanto no "Finalizar prova" manual quanto na submissão automática por tempo).
    [HttpPost("{attemptId:guid}/finish")]
    public async Task<IActionResult> Finalizar(Guid attemptId, CancellationToken cancellationToken)
    {
        await _session.FinalizarTentativaAsync(attemptId, cancellationToken);
        return Json(new { redirectUrl = Url.Action(nameof(Resultado), new { attemptId }) });
    }

    // Score report: nota na escala 1–1000 e desempenho por domínio — o que a prova real entrega.
    [HttpGet("{attemptId:guid}/result")]
    public async Task<IActionResult> Resultado(Guid attemptId, CancellationToken cancellationToken)
    {
        var result = await _session.ObterResultadoAsync(attemptId, cancellationToken);
        if (result is null)
        {
            return RedirectToAction(nameof(Realizar), new { attemptId });
        }

        return View(result);
    }

    // Modo de estudo: revisão questão a questão com gabarito e explicação. Fica fora do score
    // report de propósito — a prova real não revela quais itens o candidato errou.
    [HttpGet("{attemptId:guid}/review")]
    public async Task<IActionResult> Revisao(Guid attemptId, CancellationToken cancellationToken)
    {
        var result = await _session.ObterResultadoAsync(attemptId, cancellationToken);
        if (result is null)
        {
            return RedirectToAction(nameof(Realizar), new { attemptId });
        }

        return View(result);
    }
}
