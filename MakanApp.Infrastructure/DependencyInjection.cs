using System.Security.Cryptography;
using System.Text;
using MakanApp.Application.Academic;
using MakanApp.Application.Assessment;
using MakanApp.Application.Guardian;
using MakanApp.Application.Identity;
using MakanApp.Application.Organization;
using MakanApp.Application.Storage;
using MakanApp.Infrastructure.Academic;
using MakanApp.Infrastructure.Assessment;
using MakanApp.Infrastructure.Guardian;
using MakanApp.Infrastructure.Identity;
using MakanApp.Infrastructure.Organization;
using MakanApp.Infrastructure.Storage;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MakanApp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string environmentName)
    {
        var connectionString = configuration.GetConnectionString("MakanDatabase");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:MakanDatabase must be configured before the application starts.");
        }

        services.AddDbContext<MakanDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IIdentityStore, EfIdentityStore>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IOrganizationStore, EfOrganizationStore>();
        services.AddScoped<OrganizationService>();
        services.AddScoped<IOrganizationService>(serviceProvider =>
            serviceProvider.GetRequiredService<OrganizationService>());
        services.AddScoped<IGuardianStore, EfGuardianStore>();
        services.AddScoped<IGuardianService, GuardianService>();
        services.AddScoped<IAcademicStore, EfAcademicStore>();
        services.AddScoped<IAcademicService, AcademicService>();
        services.AddScoped<IAcademicSessionStore, EfAcademicSessionStore>();
        services.AddScoped<IAcademicSessionService, AcademicSessionService>();
        services.AddScoped<IAssignmentStore, EfAssignmentStore>();
        services.AddScoped<IAssignmentService, AssignmentService>();
        services.AddScoped<ISubmissionStore, EfSubmissionStore>();
        services.AddScoped<ISubmissionService, SubmissionService>();
        services.AddScoped<IFileAssetStore, EfFileAssetStore>();
        services.AddScoped<IStorageService, StorageService>();
        services.AddScoped<IAccessContextResolver, GuardianAccessContextResolver>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(CreateOtpOptions(configuration, environmentName));
        services.AddSingleton(CreateSessionOptions(configuration));
        var storageOptions = CreateStorageOptions(configuration);
        services.AddSingleton(storageOptions);
        services.AddSingleton<FileUploadPolicy>();
        services.AddSingleton<IIdentitySecurity>(
            new IdentityCryptography(CreateSecurityKey(configuration, environmentName)));

        if (IsDevelopmentLike(environmentName))
        {
            services.AddSingleton<DevelopmentOtpStore>();
            services.AddSingleton<ISmsSender, DevelopmentSmsSender>();
            services.AddSingleton<LocalFileStorage>();
            services.AddSingleton<IFileStorage>(serviceProvider =>
                serviceProvider.GetRequiredService<LocalFileStorage>());
        }
        else
        {
            services.AddSingleton<ISmsSender, UnavailableSmsSender>();
            services.AddSingleton<IFileStorage, UnavailableFileStorage>();
        }

        return services;
    }

    private static OtpOptions CreateOtpOptions(
        IConfiguration configuration,
        string environmentName) => new()
        {
            Lifetime = TimeSpan.FromSeconds(ReadPositiveInt(
                configuration,
                "Identity:Otp:LifetimeSeconds",
                300)),
            ResendDelay = TimeSpan.FromSeconds(
                environmentName.Equals("Testing", StringComparison.OrdinalIgnoreCase)
                    ? 0
                    : ReadPositiveInt(configuration, "Identity:Otp:ResendDelaySeconds", 30)),
            RateLimitWindow = TimeSpan.FromSeconds(ReadPositiveInt(
                configuration,
                "Identity:Otp:RateLimitWindowSeconds",
                3600)),
            MaxRequestsPerWindow = ReadPositiveInt(
                configuration,
                "Identity:Otp:MaxRequestsPerWindow",
                environmentName.Equals("Testing", StringComparison.OrdinalIgnoreCase) ? 1000 : 5),
            MaxFailedAttempts = ReadPositiveInt(
                configuration,
                "Identity:Otp:MaxFailedAttempts",
                5)
        };

    private static SessionOptions CreateSessionOptions(IConfiguration configuration) => new()
    {
        Lifetime = TimeSpan.FromSeconds(ReadPositiveInt(
            configuration,
            "Identity:Session:LifetimeSeconds",
            2_592_000))
    };

    private static StorageOptions CreateStorageOptions(IConfiguration configuration)
    {
        var configuredTypes = configuration
            .GetSection($"{StorageOptions.SectionName}:AllowedContentTypes")
            .GetChildren()
            .Select(item => item.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim().ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new StorageOptions
        {
            RootPath = configuration[$"{StorageOptions.SectionName}:RootPath"] ?? string.Empty,
            MaxFileSizeBytes = ReadPositiveLong(
                configuration,
                $"{StorageOptions.SectionName}:MaxFileSizeBytes",
                10 * 1024 * 1024),
            AllowedContentTypes = configuredTypes.Count > 0
                ? configuredTypes
                : new StorageOptions().AllowedContentTypes,
            UnattachedLifetime = TimeSpan.FromHours(ReadPositiveInt(
                configuration,
                $"{StorageOptions.SectionName}:UnattachedLifetimeHours",
                24))
        };
    }

    private static byte[] CreateSecurityKey(
        IConfiguration configuration,
        string environmentName)
    {
        var configuredKey = configuration["Identity:SecurityKey"];
        if (!string.IsNullOrWhiteSpace(configuredKey))
        {
            var keyBytes = Encoding.UTF8.GetBytes(configuredKey);
            if (keyBytes.Length < 32)
            {
                throw new InvalidOperationException(
                    "Identity:SecurityKey must contain at least 32 UTF-8 bytes.");
            }

            return keyBytes;
        }

        if (!IsDevelopmentLike(environmentName))
        {
            throw new InvalidOperationException(
                "Identity:SecurityKey must be supplied by a secret store outside Development and Testing.");
        }

        return RandomNumberGenerator.GetBytes(32);
    }

    private static int ReadPositiveInt(
        IConfiguration configuration,
        string key,
        int fallback)
    {
        var value = configuration[key];
        return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
    }

    private static long ReadPositiveLong(
        IConfiguration configuration,
        string key,
        long fallback)
    {
        var value = configuration[key];
        return long.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
    }

    private static bool IsDevelopmentLike(string environmentName) =>
        environmentName.Equals("Development", StringComparison.OrdinalIgnoreCase) ||
        environmentName.Equals("Testing", StringComparison.OrdinalIgnoreCase);
}
