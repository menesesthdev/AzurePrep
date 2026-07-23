using AzurePrep.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzurePrep.Infrastructure.Persistence.Configurations;

public sealed class TentativaDeProvaConfiguration : IEntityTypeConfiguration<TentativaDeProva>
{
    public void Configure(EntityTypeBuilder<TentativaDeProva> builder)
    {
        builder.ToTable("ExamAttempts");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.ExamId).IsRequired();
        builder.Property(a => a.StartedAt).IsRequired();
        builder.Property(a => a.FinishedAt);
        builder.Property(a => a.ScorePercent).HasPrecision(5, 2);
        builder.Property(a => a.Passed);

        builder.HasOne<Exame>()
            .WithMany()
            .HasForeignKey(a => a.ExamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(a => a.Answers)
            .WithOne()
            .HasForeignKey(ans => ans.ExamAttemptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(a => a.Answers).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
