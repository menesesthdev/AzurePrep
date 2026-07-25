using AzurePrep.Domain.Entidades;

namespace AzurePrep.Application.Abstractions;

/// <summary>
/// Porta de acesso às tentativas de prova. Implementada na Infrastructure (EF Core).
/// </summary>
public interface ITentativaDeProvaRepository
{
    Task AdicionarAsync(TentativaDeProva attempt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca uma resposta recém-criada para inserção. Necessário porque as entidades geram o Id
    /// no domínio (Guid): com a chave já preenchida, o EF trataria uma resposta nova como
    /// existente (UPDATE) ao adicioná-la pela coleção do agregado. Este Add explícito garante INSERT.
    /// </summary>
    Task AdicionarRespostaAsync(RespostaDaTentativa answer, CancellationToken cancellationToken = default);

    /// <summary>Tentativa com suas respostas carregadas.</summary>
    Task<TentativaDeProva?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Todas as tentativas de um usuário, mais recentes primeiro, com as respostas carregadas
    /// (o histórico mostra o progresso das que ficaram em andamento).
    /// </summary>
    /// <remarks>
    /// Filtrar por <paramref name="userId"/> aqui, e não depois em memória, é o que mantém a
    /// regra de posse no mesmo lugar das outras consultas: quem chama não tem como pedir
    /// "todas as tentativas" por engano.
    /// </remarks>
    Task<IReadOnlyList<TentativaDeProva>> ObterDoUsuarioAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
