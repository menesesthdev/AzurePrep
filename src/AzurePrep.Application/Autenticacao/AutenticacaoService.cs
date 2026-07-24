using AzurePrep.Application.Abstractions;
using AzurePrep.Application.Contracts;
using AzurePrep.Domain.Entidades;

namespace AzurePrep.Application.Autenticacao;

public sealed class AutenticacaoService : IAutenticacaoService
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public AutenticacaoService(IUsuarioRepository usuarios, IUnitOfWork unitOfWork, IClock clock)
    {
        _usuarios = usuarios;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<UsuarioDto> ObterOuCriarAsync(
        LoginExternoRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = _clock.UtcNow;
        var usuario = await _usuarios.ObterPorProvedorAsync(request.Provider, request.ProviderKey, cancellationToken);

        if (usuario is null)
        {
            // Primeiro login: o nome pode vir vazio (GitHub sem nome público) — o handle do
            // provedor já foi resolvido pelo Web, mas garantimos um rótulo utilizável aqui.
            var name = string.IsNullOrWhiteSpace(request.Name) ? "Candidato" : request.Name;

            usuario = new Usuario(
                request.Provider,
                request.ProviderKey,
                name,
                request.Email,
                request.AvatarUrl,
                now);

            await _usuarios.AdicionarAsync(usuario, cancellationToken);
        }
        else
        {
            usuario.RegistrarLogin(request.Name, request.Email, request.AvatarUrl, now);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Mapear(usuario);
    }

    public async Task<UsuarioDto?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var usuario = await _usuarios.ObterPorIdAsync(id, cancellationToken);
        return usuario is null ? null : Mapear(usuario);
    }

    private static UsuarioDto Mapear(Usuario usuario)
        => new(usuario.Id, usuario.Name, usuario.Email, usuario.AvatarUrl, usuario.Provider);
}
