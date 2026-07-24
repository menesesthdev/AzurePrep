using AzurePrep.Application.Abstractions;
using AzurePrep.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AzurePrep.Infrastructure.Persistence;

/// <summary>
/// Contexto EF Core da aplicação. Também cumpre o papel de <see cref="IUnitOfWork"/>
/// (o SaveChangesAsync do DbContext já satisfaz a interface), mantendo a Application
/// desacoplada do EF.
/// </summary>
public class AzurePrepDbContext : DbContext, IUnitOfWork
{
    public AzurePrepDbContext(DbContextOptions<AzurePrepDbContext> options)
        : base(options)
    {
    }

    public DbSet<Exame> Exams => Set<Exame>();
    public DbSet<AreaDeHabilidade> SkillAreas => Set<AreaDeHabilidade>();
    public DbSet<Questao> Questions => Set<Questao>();
    public DbSet<OpcaoDeResposta> AnswerOptions => Set<OpcaoDeResposta>();
    public DbSet<TentativaDeProva> ExamAttempts => Set<TentativaDeProva>();
    public DbSet<RespostaDaTentativa> ExamAttemptAnswers => Set<RespostaDaTentativa>();
    public DbSet<Usuario> Users => Set<Usuario>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AzurePrepDbContext).Assembly);
    }
}
