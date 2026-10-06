using MakanApp.Domain.Assessment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Assessment;

public sealed class QuestionVersionConfiguration : IEntityTypeConfiguration<QuestionVersion>
{
    public void Configure(EntityTypeBuilder<QuestionVersion> builder)
    {
        builder.ToTable("QuestionVersions", "assessment", table =>
        {
            table.HasCheckConstraint("CK_QuestionVersions_Order", "[Order] > 0");
            table.HasCheckConstraint("CK_QuestionVersions_Type", "[Type] IN (1, 2)");
            table.HasCheckConstraint("CK_QuestionVersions_Score", "[Score] > 0");
        });
        builder.HasKey(question => question.Id);
        builder.HasAlternateKey(question => new { question.OrganizationId, question.Id })
            .HasName("UQ_QuestionVersions_OrganizationId_Id");
        builder.Property(question => question.Prompt)
            .HasMaxLength(QuestionVersion.MaximumPromptLength)
            .IsRequired();
        builder.Property(question => question.Score).HasPrecision(9, 2);
        builder.Property(question => question.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(question => question.RowVersion).IsRowVersion();
        builder.HasIndex(question => new
        {
            question.OrganizationId,
            question.ExamVersionId,
            question.Order
        })
            .IsUnique()
            .HasDatabaseName("UX_QuestionVersions_Organization_ExamVersion_Order");

        builder.HasOne<ExamVersion>()
            .WithMany()
            .HasForeignKey(question => new { question.OrganizationId, question.ExamVersionId })
            .HasPrincipalKey(version => new { version.OrganizationId, version.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(question => question.Options)
            .WithOne()
            .HasForeignKey(option => new { option.OrganizationId, option.QuestionVersionId })
            .HasPrincipalKey(question => new { question.OrganizationId, question.Id })
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(question => question.Options)
            .UsePropertyAccessMode(Microsoft.EntityFrameworkCore.PropertyAccessMode.Field);
    }
}
