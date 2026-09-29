using MakanApp.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Identity;

public sealed class UserCredentialConfiguration : IEntityTypeConfiguration<UserCredential>
{
    public void Configure(EntityTypeBuilder<UserCredential> builder)
    {
        builder.ToTable("UserCredentials", "identity");
        builder.HasKey(credential => credential.Id);
        builder.Property(credential => credential.NormalizedIdentifier)
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(credential => credential.VerifiedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(credential => credential.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(credential => credential.RowVersion).IsRowVersion();

        builder.HasIndex(credential => new
        {
            credential.Kind,
            credential.NormalizedIdentifier
        })
            .IsUnique()
            .HasDatabaseName("UX_UserCredentials_Kind_Identifier");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(credential => credential.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
