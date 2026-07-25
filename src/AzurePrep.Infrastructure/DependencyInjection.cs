using AzurePrep.Application.Abstractions;
using AzurePrep.Infrastructure.Email;
using AzurePrep.Infrastructure.Persistence;
using AzurePrep.Infrastructure.Persistence.Repositories;
using AzurePrep.Infrastructure.Seguranca;
using AzurePrep.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AzurePrep.Infrastructure;

/// <summary>Registra o acesso a dados (EF Core + SQLite) e os serviços de infraestrutura.</summary>
public static class DependencyInjection
{
    public const string ConnectionStringName = "Default";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
                               ?? "Data Source=App_Data/azureprep.db";

        services.AddDbContext<AzurePrepDbContext>(options => options.UseSqlite(connectionString));

        // O mesmo DbContext (scoped) cumpre o papel de Unit of Work.
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AzurePrepDbContext>());

        services.AddScoped<IExameRepository, ExameRepository>();
        services.AddScoped<ITentativaDeProvaRepository, TentativaDeProvaRepository>();
        services.AddScoped<IUsuarioRepository, UsuarioRepository>();

        services.AddSingleton<IClock, SystemClock>();

        // Sem estado entre chamadas (salt é sorteado a cada hash), então singleton basta.
        services.AddSingleton<IHasherDeSenha, HasherDeSenhaPbkdf2>();
        services.AddSingleton<IGeradorDeTokenSeguro, GeradorDeTokenSeguro>();

        AdicionarEnvioDeEmail(services, configuration);

        return services;
    }

    /// <summary>
    /// Registra o envio real só se houver SMTP configurado; sem isso, o enviador de log. Mesma
    /// disciplina dos provedores OAuth: a app sobe e o fluxo de "esqueci minha senha" funciona
    /// em desenvolvimento sem servidor de e-mail, com o link aparecendo no console.
    /// </summary>
    private static void AdicionarEnvioDeEmail(IServiceCollection services, IConfiguration configuration)
    {
        // Leitura chave a chave, como já é feito com as credenciais OAuth no Web: evita trazer
        // o pacote de binder só por isso e deixa explícito o que a seção aceita.
        var secao = configuration.GetSection(OpcoesDeEmail.Secao);
        var opcoes = new OpcoesDeEmail
        {
            SmtpHost = secao["SmtpHost"],
            SmtpPort = int.TryParse(secao["SmtpPort"], out var porta) ? porta : 587,
            UsarSsl = !bool.TryParse(secao["UsarSsl"], out var ssl) || ssl,
            Usuario = secao["Usuario"],
            Senha = secao["Senha"],
            RemetenteEndereco = secao["RemetenteEndereco"] ?? "nao-responda@azureprep.local",
            RemetenteNome = secao["RemetenteNome"] ?? "AzurePrep"
        };

        services.AddSingleton(opcoes);

        if (opcoes.EstaConfigurado)
        {
            services.AddSingleton<IEnviadorDeEmail, EnviadorDeEmailSmtp>();
        }
        else
        {
            services.AddSingleton<IEnviadorDeEmail, EnviadorDeEmailParaLog>();
        }
    }
}
