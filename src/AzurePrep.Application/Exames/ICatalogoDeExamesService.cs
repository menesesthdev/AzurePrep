using AzurePrep.Application.Contracts;

namespace AzurePrep.Application.Exames;

/// <summary>Consulta os exames disponíveis para a tela inicial.</summary>
public interface ICatalogoDeExamesService
{
    Task<IReadOnlyList<ResumoDeExameDto>> ObterExamesDisponiveisAsync(CancellationToken cancellationToken = default);
}
