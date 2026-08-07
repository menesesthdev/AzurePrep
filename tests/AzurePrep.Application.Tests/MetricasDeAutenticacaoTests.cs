using AzurePrep.Application.Abstractions;
using AzurePrep.Application.Contracts;
using AzurePrep.Application.Tests.Fakes;
using AzurePrep.Domain.Autenticacao;
using AzurePrep.Domain.Enums;

namespace AzurePrep.Application.Tests;

/// <summary>
/// O que a autenticação publica nas métricas.
/// </summary>
/// <remarks>
/// <para>
/// A instrumentação vive no caso de uso, e não no controller, justamente para não depender de
/// alguém lembrar de chamá-la em cada endpoint novo — o que só vale se houver teste garantindo
/// que ela continua sendo chamada. Métrica é o tipo de código que apodrece calado: quando para de
/// registrar, nada quebra e nada aparece no log; o painel só fica plano, que é exatamente o que
/// se espera ver num dia sem movimento.
/// </para>
/// <para>
/// O ponto mais delicado é o desfecho do login. A TELA responde a mesma coisa para e-mail
/// inexistente, senha errada e conta bloqueada, de propósito — distinguir ali viraria oráculo
/// para descobrir quem tem conta. Aqui dentro a distinção existe e é o que torna um ataque
/// visível; se ela se perder, o painel continua desenhando uma linha só de "falhas" e ninguém
/// nota a diferença.
/// </para>
/// </remarks>
public class MetricasDeAutenticacaoTests
{
    private const string Email = "ana@example.com";
    private const string Senha = "senha-que-serve";

    private static async Task<(AutenticacaoServiceHarness harness, FakeMetricasDeNegocio metricas)> ComContaLocalAsync()
    {
        var harness = AutenticacaoServiceHarness.Novo();
        await harness.Service.CadastrarComSenhaAsync(new CadastroLocalRequest("Ana", Email, Senha));
        harness.Metricas.Limpar();
        return (harness, harness.Metricas);
    }

    [Fact]
    public async Task Cadastro_RegistraContaCriadaComOProvedorLocal()
    {
        var harness = AutenticacaoServiceHarness.Novo();

        await harness.Service.CadastrarComSenhaAsync(new CadastroLocalRequest("Ana", Email, Senha));

        Assert.Equal([ProvedorDeLogin.Local], harness.Metricas.ContasCriadas);
        Assert.Empty(harness.Metricas.CadastrosRecusados);
    }

    [Fact]
    public async Task CadastroComEmailRepetido_RegistraRecusaENaoContaCriacao()
    {
        var (harness, metricas) = await ComContaLocalAsync();

        await harness.Service.CadastrarComSenhaAsync(new CadastroLocalRequest("Outra", Email, Senha));

        Assert.Equal([MotivoDeRecusaDeCadastro.EmailJaCadastrado], metricas.CadastrosRecusados);
        Assert.Empty(metricas.ContasCriadas);
    }

    [Fact]
    public async Task CadastroComSenhaCurta_RegistraRecusaPorSenhaInaceitavel()
    {
        var harness = AutenticacaoServiceHarness.Novo();

        await harness.Service.CadastrarComSenhaAsync(new CadastroLocalRequest("Ana", Email, "abc"));

        Assert.Equal([MotivoDeRecusaDeCadastro.SenhaInaceitavel], harness.Metricas.CadastrosRecusados);
    }

    [Fact]
    public async Task LoginCorreto_RegistraSucesso()
    {
        var (harness, metricas) = await ComContaLocalAsync();

        await harness.Service.AutenticarComSenhaAsync(new LoginLocalRequest(Email, Senha));

        Assert.Equal([(ProvedorDeLogin.Local, ResultadoDeLogin.Sucesso)], metricas.Logins);
    }

    [Fact]
    public async Task SenhaErrada_RegistraSenhaIncorreta()
    {
        var (harness, metricas) = await ComContaLocalAsync();

        await harness.Service.AutenticarComSenhaAsync(new LoginLocalRequest(Email, "outra-coisa"));

        Assert.Equal([(ProvedorDeLogin.Local, ResultadoDeLogin.SenhaIncorreta)], metricas.Logins);
    }

    [Fact]
    public async Task EmailSemConta_RegistraContaInexistente()
    {
        var harness = AutenticacaoServiceHarness.Novo();

        await harness.Service.AutenticarComSenhaAsync(new LoginLocalRequest("ninguem@example.com", Senha));

        Assert.Equal([(ProvedorDeLogin.Local, ResultadoDeLogin.ContaInexistente)], harness.Metricas.Logins);
    }

    /// <summary>
    /// O bloqueio por falhas consecutivas é SILENCIOSO na tela — confirmar que a conta existe
    /// seria o próprio vazamento que ele tenta evitar. A métrica é o único lugar onde ele
    /// aparece, e é ela que permite ver o ataque que disparou o bloqueio.
    /// </summary>
    [Fact]
    public async Task ContaBloqueada_ApareceComoContaBloqueadaSoNasMetricas()
    {
        var (harness, metricas) = await ComContaLocalAsync();

        // Erra até estourar a política e a conta travar.
        for (var i = 0; i < PoliticaDeTentativasDeLogin.MaximoDeFalhas; i++)
        {
            await harness.Service.AutenticarComSenhaAsync(new LoginLocalRequest(Email, "errada"));
        }

        metricas.Limpar();

        // Agora nem a senha certa entra — e é este desfecho que precisa ser distinguível aqui.
        var resultado = await harness.Service.AutenticarComSenhaAsync(new LoginLocalRequest(Email, Senha));

        Assert.False(resultado.Autenticou);
        Assert.Equal([(ProvedorDeLogin.Local, ResultadoDeLogin.ContaBloqueada)], metricas.Logins);

        // E a recusa que chega ao usuário continua sendo a genérica, indistinguível de senha errada.
        Assert.Equal(FalhaDeAutenticacao.CredenciaisInvalidas, resultado.Falha);
    }

    [Fact]
    public async Task LoginExterno_RegistraContaCriadaSoNaPrimeiraVez()
    {
        var harness = AutenticacaoServiceHarness.Novo();
        var request = new LoginExternoRequest(ProvedorDeLogin.GitHub, "chave-externa", "Ana", null, null);

        await harness.Service.ObterOuCriarAsync(request);
        await harness.Service.ObterOuCriarAsync(request);

        Assert.Equal([ProvedorDeLogin.GitHub], harness.Metricas.ContasCriadas);
        Assert.Equal(
            [(ProvedorDeLogin.GitHub, ResultadoDeLogin.Sucesso), (ProvedorDeLogin.GitHub, ResultadoDeLogin.Sucesso)],
            harness.Metricas.Logins);
    }

    /// <summary>
    /// A diferença entre "pedido" e "link emitido" é o que mostra alguém varrendo e-mails à
    /// procura de quem tem cadastro — a tela responde igual nos dois casos.
    /// </summary>
    [Fact]
    public async Task EsqueciSenha_SeparaOPedidoDaEmissaoDoLink()
    {
        var (harness, metricas) = await ComContaLocalAsync();

        await harness.Service.SolicitarRedefinicaoDeSenhaAsync("ninguem@example.com");
        Assert.Equal([EtapaDeRedefinicao.Solicitada], metricas.Redefinicoes);

        metricas.Limpar();

        await harness.Service.SolicitarRedefinicaoDeSenhaAsync(Email);
        Assert.Equal([EtapaDeRedefinicao.Solicitada, EtapaDeRedefinicao.LinkEmitido], metricas.Redefinicoes);
    }

    [Fact]
    public async Task RedefinicaoConcluida_ERegistrada()
    {
        var (harness, metricas) = await ComContaLocalAsync();

        await harness.Service.SolicitarRedefinicaoDeSenhaAsync(Email);
        metricas.Limpar();

        var resultado = await harness.Service.RedefinirSenhaAsync(
            new RedefinicaoDeSenhaRequest(harness.Tokens.UltimoGerado, "outra-senha-boa"));

        Assert.True(resultado.Autenticou);
        Assert.Equal([EtapaDeRedefinicao.Concluida], metricas.Redefinicoes);
    }
}
