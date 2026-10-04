using MakanApp.Application.Identity;
using MakanApp.Domain.Identity;
using MakanApp.Infrastructure.Identity;
using MakanApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MakanAppWebApplicationFactory :
    WebApplicationFactory<Program>,
    IAsyncLifetime
{
    private const string ConnectionStringEnvironmentVariable =
        "ConnectionStrings__MakanDatabase";

    public const string DatabaseName = "MakanApp_Assignments_IntegrationTests_Step6A";

    private const string TestConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=" + DatabaseName + ";Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

    private readonly string? _originalConnectionString =
        Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);

    public MakanAppWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable(
            ConnectionStringEnvironmentVariable,
            TestConnectionString);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:MakanDatabase"] = TestConnectionString
            });
        });
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        try
        {
            await using var scope = Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            await dbContext.Database.EnsureDeletedAsync();
            Dispose();
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                ConnectionStringEnvironmentVariable,
                _originalConnectionString);
        }
    }

    public string GetOtpCode(Guid challengeId)
    {
        var store = Services.GetRequiredService<DevelopmentOtpStore>();
        return store.TryGet(challengeId, out var message)
            ? message!.Code
            : throw new InvalidOperationException("Development OTP was not captured.");
    }

    public async Task ExpireChallengeAsync(Guid challengeId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var expiredAtUtc = DateTime.UtcNow.AddMinutes(-1);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [identity].[OtpChallenges] SET [ExpiresAtUtc] = {expiredAtUtc} WHERE [Id] = {challengeId}");
    }

    public async Task<int> CountUsersForPhoneAsync(string normalizedPhoneNumber)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.UserCredentials.CountAsync(
            credential => credential.Kind == CredentialKind.MobilePhone &&
                          credential.NormalizedIdentifier == normalizedPhoneNumber);
    }

    public async Task<bool> IsSessionRevokedAsync(string rawToken)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var security = scope.ServiceProvider.GetRequiredService<IIdentitySecurity>();
        var tokenHash = security.HashSessionToken(rawToken);
        var session = await dbContext.UserSessions.SingleAsync(
            candidate => candidate.TokenHash.SequenceEqual(tokenHash));
        return session.RevokedAtUtc.HasValue;
    }
}
