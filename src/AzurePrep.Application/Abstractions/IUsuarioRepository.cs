using AzurePrep.Domain.Entidades;
using AzurePrep.Domain.Enums;

namespace AzurePrep.Application.Abstractions;

public interface IUsuarioRepository
{
    /// <summary>Busca pelo par (provedor, chave externa) — a chave natural da identidade.</summary>
    Task<Usuario?> ObterPorProvedorAsync(
        ProvedorDeLogin provider,
        string providerKey,
        CancellationToken cancellationToken = default);

    Task<Usuario?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AdicionarAsync(Usuario usuario, CancellationToken cancellationToken = default);
}
