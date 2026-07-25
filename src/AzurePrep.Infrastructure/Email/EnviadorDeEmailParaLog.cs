using AzurePrep.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace AzurePrep.Infrastructure.Email;

/// <summary>
/// Substituto de desenvolvimento: escreve o e-mail no log em vez de enviar.
/// </summary>
/// <remarks>
/// É o que permite testar "esqueci minha senha" de ponta a ponta sem servidor de e-mail — o
/// link aparece no console e você abre no navegador. O aviso é em nível Warning de propósito:
/// se isso rodar em produção por configuração faltando, tem de doer no log, porque significa
/// que ninguém está recebendo link de redefinição.
/// </remarks>
public sealed class EnviadorDeEmailParaLog : IEnviadorDeEmail
{
    private readonly ILogger<EnviadorDeEmailParaLog> _logger;

    public EnviadorDeEmailParaLog(ILogger<EnviadorDeEmailParaLog> logger)
    {
        _logger = logger;
    }

    public Task EnviarAsync(
        string destinatario,
        string assunto,
        string corpo,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "SMTP não configurado — e-mail NÃO enviado. Conteúdo abaixo.\n"
            + "Para: {Destinatario}\nAssunto: {Assunto}\n{Corpo}",
            destinatario,
            assunto,
            corpo);

        return Task.CompletedTask;
    }
}
