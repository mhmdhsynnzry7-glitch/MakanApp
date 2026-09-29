using MakanApp.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Identity;

public sealed class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("Persons", "identity");
        builder.HasKey(person => person.Id);
        builder.Property(person => person.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(person => person.LastName).HasMaxLength(100).IsRequired();
        builder.Property(person => person.DisplayName).HasMaxLength(200);
        builder.Property(person => person.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(person => person.UpdatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(person => person.RowVersion).IsRowVersion();
    }
}
