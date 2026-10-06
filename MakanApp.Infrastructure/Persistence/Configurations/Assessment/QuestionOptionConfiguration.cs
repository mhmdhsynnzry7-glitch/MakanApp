using MakanApp.Domain.Assessment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Assessment;

public sealed class QuestionOptionConfiguration : IEntityTypeConfiguration<QuestionOption>
{
    public void Configure(EntityTypeBuilder<QuestionOption> builder)
    {
        builder.ToTable("QuestionOptions", "assessment", table =>
        {
            table.HasCheckConstraint("CK_QuestionOptions_Order", "[Order] > 0");
        });
        builder.HasKey(option => option.Id);
        builder.Property(option => option.Text)
            .HasMaxLength(QuestionOption.MaximumTextLength)
            .IsRequired();
        builder.HasIndex(option => new
        {
            option.OrganizationId,
            option.QuestionVersionId,
            option.Order
        })
            .IsUnique()
            .HasDatabaseName("UX_QuestionOptions_Organization_Question_Order");
    }
}
