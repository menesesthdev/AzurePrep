using System.Net;
using System.Net.Mail;
using AzurePrep.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace AzurePrep.Infrastructure.Email;

/// <summary>Envio real por SMTP, registrado só quando <c>Email:SmtpHost</c> está configurado.</summary>
/// <remarks>
/// <para>
/// Usa <see cref="SmtpClient"/> da BCL para não trazer dependência nova. A Microsoft recomenda
/// MailKit para cenários modernos (o SmtpClient não suporta, por exemplo, OAuth no SMTP); se um
/// dia isso apertar, a troca é só esta classe — quem chama conhece apenas
/// <see cref="IEnviadorDeEmail"/>.
/// </para>
/// <para>
/// Falha de envio é registrada e engolida de propósito: o pedido de redefinição já foi gravado,
/// e propagar a exceção mudaria a resposta da tela justamente no caminho em que o e-mail
/// existe — o que denunciaria quais e-mails têm conta.
/// </para>
/// </remarks>
public sealed class EnviadorDeEmailSmtp : IEnviadorDeEmail
{
    private readonly OpcoesDeEmail _opcoes;
    private readonly ILogger<EnviadorDeEmailSmtp> _logger;

    public EnviadorDeEmailSmtp(OpcoesDeEmail opcoes, ILogger<EnviadorDeEmailSmtp> logger)
    {
        _opcoes = opcoes;
        _logger = logger;
    }

    public async Task EnviarAsync(
        string destinatario,
        string assunto,
        string corpo,
        CancellationToken cancellationToken = default)
    {
        using var cliente = new SmtpClient(_opcoes.SmtpHost, _opcoes.SmtpPort)
        {
            EnableSsl = _opcoes.UsarSsl
        };

        if (!string.IsNullOrWhiteSpace(_opcoes.Usuario))
        {
            cliente.Credentials = new NetworkCredential(_opcoes.Usuario, _opcoes.Senha);
        }

        using var mensagem = new MailMessage
        {
            From = new MailAddress(_opcoes.RemetenteEndereco, _opcoes.RemetenteNome),
            Subject = assunto,
            Body = corpo,
            IsBodyHtml = false
        };
        mensagem.To.Add(destinatario);

        try
        {
            await cliente.SendMailAsync(mensagem, cancellationToken);
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException or IOException)
        {
            _logger.LogError(ex, "Falha ao enviar e-mail por SMTP para {Destinatario}.", destinatario);
        }
    }
}
