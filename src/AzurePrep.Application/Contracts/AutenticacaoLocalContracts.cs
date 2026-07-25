namespace AzurePrep.Application.Contracts;

/// <summary>Dados do cadastro com e-mail e senha. A senha chega em texto e sai daqui como hash.</summary>
public sealed record CadastroLocalRequest(string Name, string Email, string Password);

/// <summary>Credenciais de login da conta local.</summary>
public sealed record LoginLocalRequest(string Email, string Password);

/// <summary>Nova senha, apresentada junto com o token que veio no link do e-mail.</summary>
public sealed record RedefinicaoDeSenhaRequest(string Token, string NovaSenha);

/// <summary>
/// Token recém-emitido, para o Web montar o link e enviar o e-mail. O valor em texto existe
/// só aqui e no e-mail — no banco fica apenas o hash.
/// </summary>
public sealed record TokenDeRedefinicaoDto(string Token, string Email, string Nome);

/// <summary>Motivo pelo qual cadastro ou login não passou.</summary>
public enum FalhaDeAutenticacao
{
    /// <summary>
    /// Login recusado. Deliberadamente não distingue "e-mail inexistente" de "senha errada"
    /// nem de "essa conta é social": qualquer diferença aí vira oráculo para descobrir quem
    /// tem conta no sistema.
    /// </summary>
    CredenciaisInvalidas = 1,

    /// <summary>Já existe conta local com esse e-mail.</summary>
    EmailJaCadastrado = 2,

    /// <summary>Senha fora da <see cref="Domain.Autenticacao.PoliticaDeSenha"/>.</summary>
    SenhaInaceitavel = 3,

    /// <summary>
    /// Link de redefinição inexistente, já usado ou vencido. Os três casos viram um só na
    /// tela — distinguir "não existe" de "expirou" ajudaria a sondar tokens.
    /// </summary>
    TokenInvalido = 4
}

/// <summary>
/// Saída de cadastro/login. Falha de credencial é resultado esperado do caso de uso, não
/// excepcional — devolver em vez de lançar deixa o controller tratar sem capturar exceção.
/// </summary>
public sealed record ResultadoDeAutenticacao
{
    private ResultadoDeAutenticacao(UsuarioDto? usuario, FalhaDeAutenticacao? falha)
    {
        Usuario = usuario;
        Falha = falha;
    }

    public UsuarioDto? Usuario { get; }

    public FalhaDeAutenticacao? Falha { get; }

    public bool Autenticou => Usuario is not null;

    public static ResultadoDeAutenticacao Sucesso(UsuarioDto usuario) => new(usuario, null);

    public static ResultadoDeAutenticacao Recusado(FalhaDeAutenticacao falha) => new(null, falha);
}
