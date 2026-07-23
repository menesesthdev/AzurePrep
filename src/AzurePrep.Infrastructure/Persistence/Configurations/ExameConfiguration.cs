using AzurePrep.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzurePrep.Infrastructure.Persistence.Configurations;

public sealed class ExameConfiguration : IEntityTypeConfiguration<Exame>
{
    public void Configure(EntityTypeBuilder<Exame> builder)
    {
        builder.ToTable("Exams");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Code).IsRequired().HasMaxLength(20);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.TimeLimitMinutes).IsRequired();
        builder.Property(e => e.PassingScorePercent).IsRequired();
        builder.Property(e => e.TotalQuestions).IsRequired();

        builder.HasIndex(e => e.Code).IsUnique();

        builder.HasMany(e => e.SkillAreas)
            .WithOne()
            .HasForeignKey(s => s.ExamId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Questions)
            .WithOne()
            .HasForeignKey(q => q.ExamId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(e => e.SkillAreas).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(e => e.Questions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
