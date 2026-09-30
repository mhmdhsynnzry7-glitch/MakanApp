using System.Security.Cryptography;
using System.Text;
using MakanApp.Application.Academic;
using MakanApp.Application.Guardian;
using MakanApp.Application.Identity;
using MakanApp.Application.Organization;
using MakanApp.Infrastructure.Academic;
using MakanApp.Infrastructure.Guardian;
using MakanApp.Infrastructure.Identity;
using MakanApp.Infrastructure.Organization;
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
        services.AddScoped<IAccessContextResolver, GuardianAccessContextResolver>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(CreateOtpOptions(configuration, environmentName));
        services.AddSingleton(CreateSessionOptions(configuration));
        services.AddSingleton<IIdentitySecurity>(
            new IdentityCryptography(CreateSecurityKey(configuration, environmentName)));

        if (IsDevelopmentLike(environmentName))
        {
            services.AddSingleton<DevelopmentOtpStore>();
            services.AddSingleton<ISmsSender, DevelopmentSmsSender>();
        }
        else
        {
            services.AddSingleton<ISmsSender, UnavailableSmsSender>();
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

    private static bool IsDevelopmentLike(string environmentName) =>
        environmentName.Equals("Development", StringComparison.OrdinalIgnoreCase) ||
        environmentName.Equals("Testing", StringComparison.OrdinalIgnoreCase);
}
