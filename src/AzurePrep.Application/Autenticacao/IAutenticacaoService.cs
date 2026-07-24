using AzurePrep.Application.Contracts;

namespace AzurePrep.Application.Autenticacao;

public interface IAutenticacaoService
{
    /// <summary>
    /// Resolve o usuário local a partir do perfil externo: cria no primeiro login e,
    /// nos seguintes, atualiza o perfil e carimba a data de acesso.
    /// </summary>
    Task<UsuarioDto> ObterOuCriarAsync(LoginExternoRequest request, CancellationToken cancellationToken = default);

    Task<UsuarioDto?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
}
