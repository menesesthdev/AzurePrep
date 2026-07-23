using AzurePrep.Domain.Entidades;

namespace AzurePrep.Application.Abstractions;

/// <summary>
/// Porta de acesso aos exames e seu conteúdo. Implementada na Infrastructure (EF Core).
/// </summary>
public interface IExameRepository
{
    Task<IReadOnlyList<Exame>> ObterTodosAsync(CancellationToken cancellationToken = default);

    Task<Exame?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Exame com skill areas, questões e opções carregadas — para montar/corrigir a prova.</summary>
    Task<Exame?> ObterComConteudoAsync(Guid id, CancellationToken cancellationToken = default);
}
