using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Messaging;

public sealed class AbuseReportConfiguration : IEntityTypeConfiguration<AbuseReport>
{
    public void Configure(EntityTypeBuilder<AbuseReport> builder)
    {
        builder.ToTable("AbuseReports", "messaging", table =>
        {
            table.HasCheckConstraint("CK_AbuseReports_Reason", "[Reason] IN (1, 2, 3, 4, 5, 6)");
            table.HasCheckConstraint("CK_AbuseReports_Status", "[Status] IN (1, 2, 3, 4)");
            table.HasCheckConstraint(
                "CK_AbuseReports_MessageKind",
                "[ReportedMessageKind] IN (1, 2, 3, 4, 5)");
        });
        builder.HasKey(report => report.Id);
        builder.Property(report => report.Description)
            .HasMaxLength(AbuseReport.MaximumDescriptionLength);
        builder.Property(report => report.ReportedContentSnapshot)
            .HasMaxLength(Message.StorageMaximumTextLength);
        builder.Property(report => report.ReportedMessageVersion)
            .HasColumnType("binary(8)")
            .ValueGeneratedNever();
        builder.Property(report => report.RequestPayloadHash)
            .HasColumnType("binary(32)")
            .ValueGeneratedNever();
        builder.Property(report => report.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(report => report.RowVersion).IsRowVersion();
        builder.HasIndex(report => new { report.ReporterUserId, report.ClientReportId })
            .IsUnique()
            .HasDatabaseName("UX_AbuseReports_Reporter_ClientReportId");
        builder.HasIndex(report => new { report.Status, report.CreatedAtUtc })
            .HasDatabaseName("IX_AbuseReports_Status_CreatedAtUtc");
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(report => report.ReporterUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(report => report.ReportedUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Conversation>()
            .WithMany()
            .HasForeignKey(report => report.ConversationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Message>()
            .WithMany()
            .HasForeignKey(report => new { report.MessageId, report.ConversationId })
            .HasPrincipalKey(message => new { message.Id, message.ConversationId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
