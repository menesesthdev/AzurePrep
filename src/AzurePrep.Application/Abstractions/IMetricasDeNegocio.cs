using AzurePrep.Domain.Enums;

namespace AzurePrep.Application.Abstractions;

/// <summary>
/// Porta pela qual os casos de uso registram o que aconteceu no produto — conta criada, login,
/// prova encerrada. Quem coleta de verdade é a Infrastructure; a Application só declara o evento.
/// </summary>
/// <remarks>
/// <para>
/// A instrumentação vive AQUI, e não nos controllers, pelo mesmo motivo da checagem de posse da
/// tentativa: o controller não tem como esquecer de contar o que não é ele quem conta. Um login
/// que passasse por outro caminho (um comando de linha, um teste de carga, um endpoint novo)
/// continuaria aparecendo no gráfico.
/// </para>
/// <para>
/// ⚠️ Nada que chegue aqui pode identificar uma pessoa. Métrica vira série temporal indexada por
/// rótulo: e-mail, nome ou id de usuário como rótulo criariam uma série por pessoa — o que estoura
/// a memória do Prometheus (alta cardinalidade) e transforma o painel num cadastro exposto. Por
/// isso os parâmetros são enums e código de exame, todos de domínio pequeno e fechado.
/// </para>
/// </remarks>
public interface IMetricasDeNegocio
{
    /// <summary>Cadastro concluído — conta local ou primeiro login por provedor externo.</summary>
    void ContaCriada(ProvedorDeLogin provedor);

    /// <summary>Cadastro que não virou conta (e-mail repetido, senha fora da política).</summary>
    void CadastroRecusado(MotivoDeRecusaDeCadastro motivo);

    /// <summary>
    /// Uma tentativa de login, tenha ela dado certo ou não. É o par
    /// (provedor, resultado) que permite ver força bruta acontecendo — a tela continua
    /// respondendo a mesma coisa para todos os casos de falha; a diferença fica só aqui.
    /// </summary>
    void LoginRegistrado(ProvedorDeLogin provedor, ResultadoDeLogin resultado);

    /// <summary>Um passo do fluxo de "esqueci minha senha".</summary>
    void RedefinicaoDeSenha(EtapaDeRedefinicao etapa);

    /// <summary>Simulado iniciado.</summary>
    void ProvaIniciada(string codigoDoExame);

    /// <summary>
    /// Simulado encerrado, com a nota na escala 1–1000 e quanto tempo levou. O percentual
    /// interno não sai daqui: o que o produto promete é a escala, e é ela que faz sentido no
    /// painel ao lado do corte em 700.
    /// </summary>
    void ProvaConcluida(
        string codigoDoExame,
        bool aprovado,
        int notaEscalada,
        TimeSpan duracao,
        MotivoDeEncerramento motivo);
}

/// <summary>
/// Desfecho de uma tentativa de login. Distingue casos que a TELA nunca distingue de propósito
/// (ver <c>FalhaDeAutenticacao.CredenciaisInvalidas</c>): o painel é interno e não responde a
/// ninguém de fora, então aqui a diferença é justamente o que se quer enxergar.
/// </summary>
public enum ResultadoDeLogin
{
    Sucesso = 1,

    /// <summary>Não há conta local com esse e-mail — inclui o e-mail que só existe num provedor social.</summary>
    ContaInexistente = 2,

    SenhaIncorreta = 3,

    /// <summary>Conta sob bloqueio temporário por falhas consecutivas.</summary>
    ContaBloqueada = 4,

    /// <summary>Pedido malformado: campo vazio ou senha acima do teto da política.</summary>
    PedidoInvalido = 5
}

public enum MotivoDeRecusaDeCadastro
{
    EmailJaCadastrado = 1,
    SenhaInaceitavel = 2,
    DadosIncompletos = 3
}

public enum EtapaDeRedefinicao
{
    /// <summary>Alguém pediu o link, exista ou não conta com aquele e-mail.</summary>
    Solicitada = 1,

    /// <summary>Existia conta local e o link foi de fato emitido.</summary>
    LinkEmitido = 2,

    /// <summary>Senha trocada com sucesso.</summary>
    Concluida = 3
}

public enum MotivoDeEncerramento
{
    /// <summary>O candidato clicou em "Encerrar prova".</summary>
    Manual = 1,

    /// <summary>O prazo venceu e o servidor fechou a tentativa.</summary>
    TempoEsgotado = 2
}
