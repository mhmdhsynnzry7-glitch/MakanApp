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
            connectionString = "Server=.;Database=MakanApp;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";
        }

        var options = new DbContextOptionsBuilder<MakanDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new MakanDbContext(options);
    }
}
