using AzurePrep.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzurePrep.Infrastructure.Persistence.Configurations;

public sealed class OpcaoDeRespostaConfiguration : IEntityTypeConfiguration<OpcaoDeResposta>
{
    public void Configure(EntityTypeBuilder<OpcaoDeResposta> builder)
    {
        builder.ToTable("AnswerOptions");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.QuestionId).IsRequired();
        builder.Property(o => o.Text).IsRequired().HasMaxLength(1000);
        builder.Property(o => o.IsCorrect).IsRequired();
        builder.Property(o => o.OrderIndex).IsRequired();
    }
}
