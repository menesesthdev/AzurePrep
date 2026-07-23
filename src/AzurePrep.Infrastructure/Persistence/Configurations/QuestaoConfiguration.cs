using AzurePrep.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzurePrep.Infrastructure.Persistence.Configurations;

public sealed class QuestaoConfiguration : IEntityTypeConfiguration<Questao>
{
    public void Configure(EntityTypeBuilder<Questao> builder)
    {
        builder.ToTable("Questions");
        builder.HasKey(q => q.Id);

        builder.Property(q => q.ExamId).IsRequired();
        builder.Property(q => q.SkillAreaId).IsRequired();
        builder.Property(q => q.Text).IsRequired().HasMaxLength(2000);
        builder.Property(q => q.Explanation).IsRequired().HasMaxLength(4000);
        builder.Property(q => q.Type).IsRequired().HasConversion<int>();

        builder.HasMany(q => q.Options)
            .WithOne()
            .HasForeignKey(o => o.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        // A propriedade Options devolve uma lista ordenada nova; o EF acessa o campo _options.
        builder.Navigation(q => q.Options).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
