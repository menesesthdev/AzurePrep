using AzurePrep.Application.Abstractions;
using AzurePrep.Infrastructure.Persistence;
using AzurePrep.Infrastructure.Persistence.Repositories;
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

        services.AddSingleton<IClock, SystemClock>();

        return services;
    }
}
