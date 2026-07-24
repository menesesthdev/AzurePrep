using AzurePrep.Application.Abstractions;
using AzurePrep.Domain.Entidades;
using AzurePrep.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AzurePrep.Infrastructure.Persistence.Repositories;

public sealed class UsuarioRepository : IUsuarioRepository
{
    private readonly AzurePrepDbContext _db;

    public UsuarioRepository(AzurePrepDbContext db)
    {
        _db = db;
    }

    // Rastreado: o login atualiza nome/foto/último acesso na instância carregada.
    public async Task<Usuario?> ObterPorProvedorAsync(
        ProvedorDeLogin provider,
        string providerKey,
        CancellationToken cancellationToken = default)
        => await _db.Users
            .FirstOrDefaultAsync(u => u.Provider == provider && u.ProviderKey == providerKey, cancellationToken);

    public async Task<Usuario?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task AdicionarAsync(Usuario usuario, CancellationToken cancellationToken = default)
        => await _db.Users.AddAsync(usuario, cancellationToken);
}
