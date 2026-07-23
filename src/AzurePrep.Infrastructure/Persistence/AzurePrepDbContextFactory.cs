using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AzurePrep.Infrastructure.Persistence;

/// <summary>
/// Factory usada apenas em design-time pelo <c>dotnet ef</c> (migrations). Em runtime, o
/// DbContext é criado pela injeção de dependência configurada em <see cref="DependencyInjection"/>.
/// </summary>
public sealed class AzurePrepDbContextFactory : IDesignTimeDbContextFactory<AzurePrepDbContext>
{
    public AzurePrepDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AzurePrepDbContext>()
            .UseSqlite("Data Source=azureprep.design.db")
            .Options;

        return new AzurePrepDbContext(options);
    }
}
