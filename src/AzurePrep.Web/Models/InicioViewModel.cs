using AzurePrep.Application.Contracts;

namespace AzurePrep.Web.Models;

/// <summary>
/// Tela inicial: catálogo de exames mais as tentativas que ficaram abertas.
/// </summary>
/// <remarks>
/// As tentativas em andamento aparecem aqui, e não só no histórico, porque é nesta tela que
/// existe o botão "Iniciar simulado" — sem o aviso, quem fechou o navegador no meio de uma prova
/// começaria outra e deixaria a primeira órfã, sem nunca ser corrigida.
/// </remarks>
public sealed record InicioViewModel(
    IReadOnlyList<ResumoDeExameDto> Exames,
    IReadOnlyList<ResumoDeTentativaDto> EmAndamento);
