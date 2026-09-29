namespace MakanApp.Application.Identity;

public sealed class OtpOptions
{
    public TimeSpan Lifetime { get; init; } = TimeSpan.FromMinutes(5);
    public TimeSpan ResendDelay { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan RateLimitWindow { get; init; } = TimeSpan.FromHours(1);
    public int MaxRequestsPerWindow { get; init; } = 5;
    public int MaxFailedAttempts { get; init; } = 5;
}

public sealed class SessionOptions
{
    public TimeSpan Lifetime { get; init; } = TimeSpan.FromDays(30);
}
