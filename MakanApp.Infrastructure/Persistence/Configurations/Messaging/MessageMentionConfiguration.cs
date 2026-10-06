using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Messaging;

public sealed class MessageMentionConfiguration : IEntityTypeConfiguration<MessageMention>
{
    public void Configure(EntityTypeBuilder<MessageMention> builder)
    {
        builder.ToTable("MessageMentions", "messaging");
        builder.HasKey(mention => mention.Id);
        builder.Property(mention => mention.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.HasIndex(mention => new { mention.MessageId, mention.MentionedUserId })
            .IsUnique()
            .HasDatabaseName("UX_MessageMentions_Message_User");
        builder.HasOne<Message>()
            .WithMany()
            .HasForeignKey(mention => mention.MessageId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(mention => mention.MentionedUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
