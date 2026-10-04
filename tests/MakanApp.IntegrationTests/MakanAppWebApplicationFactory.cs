using MakanApp.Application.Identity;
using MakanApp.Application.Storage;
using MakanApp.Domain.Identity;
using MakanApp.Infrastructure.Identity;
using MakanApp.Infrastructure.Persistence;
using MakanApp.Infrastructure.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MakanAppWebApplicationFactory :
    WebApplicationFactory<Program>,
    IAsyncLifetime
{
    private const string ConnectionStringEnvironmentVariable =
        "ConnectionStrings__MakanDatabase";

    public const string DatabaseName = "MakanApp_Submission_IntegrationTests_Step6C";

    private const string TestConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=" + DatabaseName + ";Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

    private readonly string? _originalConnectionString =
        Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);

    public string StorageRootPath { get; } = Path.Combine(
        Path.GetTempPath(),
        "MakanApp-StorageTests",
        Guid.NewGuid().ToString("N"));

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
                ["ConnectionStrings:MakanDatabase"] = TestConnectionString,
                ["Storage:RootPath"] = StorageRootPath,
                ["Storage:MaxFileSizeBytes"] = "1024",
                ["Storage:UnattachedLifetimeHours"] = "1",
                ["Storage:AllowedContentTypes:0"] = "text/plain",
                ["Storage:AllowedContentTypes:1"] = "application/pdf"
            });
        });
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<StorageOptions>();
            services.AddSingleton(new StorageOptions
            {
                RootPath = StorageRootPath,
                MaxFileSizeBytes = 1024,
                UnattachedLifetime = TimeSpan.FromHours(1),
                AllowedContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "text/plain",
                    "application/pdf"
                }
            });
            services.AddSingleton<StorageFailureSwitch>();
            services.RemoveAll<IFileStorage>();
            services.AddSingleton<IFileStorage>(serviceProvider =>
                new FaultInjectingFileStorage(
                    serviceProvider.GetRequiredService<LocalFileStorage>(),
                    serviceProvider.GetRequiredService<StorageFailureSwitch>()));
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
            try
            {
                DeleteStorageRoot();
            }
            finally
            {
                Environment.SetEnvironmentVariable(
                    ConnectionStringEnvironmentVariable,
                    _originalConnectionString);
            }
        }
    }

    private void DeleteStorageRoot()
    {
        var expectedParent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MakanApp-StorageTests"))
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var resolvedRoot = Path.GetFullPath(StorageRootPath);
        if (resolvedRoot.StartsWith(expectedParent, StringComparison.OrdinalIgnoreCase) &&
            Directory.Exists(resolvedRoot))
        {
            Directory.Delete(resolvedRoot, true);
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
