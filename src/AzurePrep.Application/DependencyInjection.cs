using AzurePrep.Application.Autenticacao;
using AzurePrep.Application.Exames;
using AzurePrep.Application.Sessoes;
using Microsoft.Extensions.DependencyInjection;

namespace AzurePrep.Application;

/// <summary>Registra os serviços de caso de uso da camada Application.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ICatalogoDeExamesService, CatalogoDeExamesService>();
        services.AddScoped<ISessaoDeProvaService, SessaoDeProvaService>();
        services.AddScoped<IAutenticacaoService, AutenticacaoService>();

        return services;
    }
}
