using AzurePrep.Application.Autenticacao;
using AzurePrep.Application.Contracts;
using AzurePrep.Application.Tests.Fakes;
using AzurePrep.Domain.Enums;

namespace AzurePrep.Application.Tests;

public class AutenticacaoServiceTests
{
    private static readonly DateTime Agora = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    private static (AutenticacaoService service, InMemoryUsuarioRepository repo, FixedClock clock) Build()
    {
        var repo = new InMemoryUsuarioRepository();
        var clock = new FixedClock(Agora);
        return (new AutenticacaoService(repo, new FakeUnitOfWork(), clock), repo, clock);
    }

    private static LoginExternoRequest Login(
        ProvedorDeLogin provider = ProvedorDeLogin.Google,
        string key = "123",
        string name = "Ana",
        string? email = "ana@example.com",
        string? avatar = "https://cdn/ana.png")
        => new(provider, key, name, email, avatar);

    [Fact]
    public async Task PrimeiroLogin_CriaUsuario()
    {
        var (service, repo, _) = Build();

        var dto = await service.ObterOuCriarAsync(Login());

        Assert.Single(repo.Todos);
        Assert.Equal("Ana", dto.Name);
        Assert.Equal("ana@example.com", dto.Email);
        Assert.Equal(ProvedorDeLogin.Google, dto.Provider);
    }

    [Fact]
    public async Task SegundoLogin_ReaproveitaOMesmoUsuario()
    {
        var (service, repo, _) = Build();

        var primeiro = await service.ObterOuCriarAsync(Login());
        var segundo = await service.ObterOuCriarAsync(Login());

        Assert.Single(repo.Todos);
        Assert.Equal(primeiro.Id, segundo.Id);
    }

    // A identidade é o par (provedor, chave): o mesmo e-mail em provedores diferentes
    // são pessoas diferentes para nós, porque vincular contas é outro recurso.
    [Fact]
    public async Task MesmoEmailEmProvedoresDiferentes_GeraUsuariosDistintos()
    {
        var (service, repo, _) = Build();

        var google = await service.ObterOuCriarAsync(Login(ProvedorDeLogin.Google, key: "g-1"));
        var github = await service.ObterOuCriarAsync(Login(ProvedorDeLogin.GitHub, key: "gh-1"));

        Assert.Equal(2, repo.Todos.Count);
        Assert.NotEqual(google.Id, github.Id);
    }

    [Fact]
    public async Task LoginPosterior_AtualizaPerfilEUltimoAcesso()
    {
        var (service, repo, clock) = Build();
        await service.ObterOuCriarAsync(Login(name: "Ana", avatar: "https://cdn/antigo.png"));

        clock.Advance(TimeSpan.FromDays(3));
        var dto = await service.ObterOuCriarAsync(Login(name: "Ana Silva", avatar: "https://cdn/novo.png"));

        Assert.Equal("Ana Silva", dto.Name);
        Assert.Equal("https://cdn/novo.png", dto.AvatarUrl);
        Assert.Equal(Agora.AddDays(3), repo.Todos[0].LastLoginAt);
        Assert.Equal(Agora, repo.Todos[0].CreatedAt);
    }

    // O GitHub pode não devolver e-mail nem foto; um login desses não deve apagar
    // o que já sabíamos da pessoa.
    [Fact]
    public async Task CamposVazios_NaoSobrescrevemOPerfilExistente()
    {
        var (service, repo, _) = Build();
        await service.ObterOuCriarAsync(Login(name: "Ana", email: "ana@example.com", avatar: "https://cdn/ana.png"));

        var dto = await service.ObterOuCriarAsync(Login(name: "", email: null, avatar: null));

        Assert.Equal("Ana", dto.Name);
        Assert.Equal("ana@example.com", dto.Email);
        Assert.Equal("https://cdn/ana.png", dto.AvatarUrl);
    }

    [Fact]
    public async Task SemNomeNoPrimeiroLogin_UsaRotuloPadrao()
    {
        var (service, _, _) = Build();

        var dto = await service.ObterOuCriarAsync(Login(name: "  ", email: null, avatar: null));

        Assert.Equal("Candidato", dto.Name);
    }

    [Fact]
    public async Task ObterPorId_DevolveNullQuandoNaoExiste()
    {
        var (service, _, _) = Build();

        Assert.Null(await service.ObterPorIdAsync(Guid.NewGuid()));
    }
}
