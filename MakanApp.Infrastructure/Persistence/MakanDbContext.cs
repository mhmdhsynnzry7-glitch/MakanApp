using MakanApp.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Persistence;

public sealed class MakanDbContext(DbContextOptions<MakanDbContext> options)
    : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Person> Persons => Set<Person>();
    public DbSet<UserCredential> UserCredentials => Set<UserCredential>();
    public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MakanDbContext).Assembly);
    }
}