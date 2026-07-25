namespace AzurePrep.Infrastructure.Email;

/// <summary>
/// Configuração do envio de e-mail, lida da seção <c>Email</c>. Segue o mesmo padrão dos
/// provedores OAuth: sem <see cref="SmtpHost"/> configurado o envio real não é registrado e a
/// app usa o enviador de log — assim o projeto sobe e o fluxo de "esqueci minha senha"
/// funciona em desenvolvimento sem servidor de e-mail nenhum.
/// </summary>
public sealed class OpcoesDeEmail
{
    public const string Secao = "Email";

    public string? SmtpHost { get; set; }

    public int SmtpPort { get; set; } = 587;

    /// <summary>STARTTLS. Só desligar em servidor local de teste.</summary>
    public bool UsarSsl { get; set; } = true;

    public string? Usuario { get; set; }

    public string? Senha { get; set; }

    /// <summary>Remetente. Muitos provedores recusam envio se não casar com a conta autenticada.</summary>
    public string RemetenteEndereco { get; set; } = "nao-responda@azureprep.local";

    public string RemetenteNome { get; set; } = "AzurePrep";

    public bool EstaConfigurado => !string.IsNullOrWhiteSpace(SmtpHost);
}
