using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MakanApp.Infrastructure.Persistence;

public sealed class MakanDbContextFactory : IDesignTimeDbContextFactory<MakanDbContext>
{
    public MakanDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "ConnectionStrings__MakanDatabase");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = "Server=(localdb)\\MSSQLLocalDB;Database=MakanApp;Trusted_Connection=True;TrustServerCertificate=True";
        }

        var options = new DbContextOptionsBuilder<MakanDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new MakanDbContext(options);
    }
}
