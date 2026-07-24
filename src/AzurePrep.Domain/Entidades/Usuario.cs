using AzurePrep.Domain.Common;
using AzurePrep.Domain.Enums;

namespace AzurePrep.Domain.Entidades;

/// <summary>
/// Pessoa que usa o simulado. A identidade vem inteiramente de um provedor externo
/// (Google, LinkedIn, GitHub) — o projeto não guarda senha nem hash de senha.
/// </summary>
/// <remarks>
/// A chave natural é o par <see cref="Provider"/> + <see cref="ProviderKey"/>, não o e-mail:
/// a mesma pessoa pode ter o mesmo e-mail em dois provedores, e o GitHub pode nem devolver
/// e-mail (o usuário escolhe mantê-lo privado). Vincular contas de provedores diferentes é
/// um recurso à parte — hoje cada par gera um usuário.
/// </remarks>
public class Usuario : Entity
{
    // Construtor exigido pelo EF Core (materialização).
    private Usuario()
    {
    }

    public Usuario(
        ProvedorDeLogin provider,
        string providerKey,
        string name,
        string? email,
        string? avatarUrl,
        DateTime createdAt,
        Guid? id = null)
        : base(id ?? Guid.NewGuid())
    {
        Provider = provider;
        ProviderKey = Guard.NotNullOrWhiteSpace(providerKey, nameof(providerKey));
        Name = Guard.NotNullOrWhiteSpace(name, nameof(name));
        Email = email;
        AvatarUrl = avatarUrl;
        CreatedAt = createdAt;
        LastLoginAt = createdAt;
    }

    public ProvedorDeLogin Provider { get; private set; }

    /// <summary>Identificador da pessoa dentro do provedor (claim NameIdentifier).</summary>
    public string ProviderKey { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    /// <summary>Opcional: o GitHub só devolve e-mail se a pessoa o tornar público.</summary>
    public string? Email { get; private set; }

    public string? AvatarUrl { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime LastLoginAt { get; private set; }

    /// <summary>
    /// Reaplica o perfil vindo do provedor a cada login — nome, foto e e-mail mudam lá fora
    /// e a nossa cópia é só um cache. Campos vazios não sobrescrevem o que já temos.
    /// </summary>
    public void RegistrarLogin(string? name, string? email, string? avatarUrl, DateTime at)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            Name = name;
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            Email = email;
        }

        if (!string.IsNullOrWhiteSpace(avatarUrl))
        {
            AvatarUrl = avatarUrl;
        }

        LastLoginAt = at;
    }
}
