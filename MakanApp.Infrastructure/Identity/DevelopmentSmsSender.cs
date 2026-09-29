using System.Collections.Concurrent;
using MakanApp.Application.Identity;

namespace MakanApp.Infrastructure.Identity;

public sealed record DevelopmentOtpMessage(
    string PhoneNumber,
    string Code,
    DateTime ExpiresAtUtc);

public sealed class DevelopmentOtpStore
{
    private readonly ConcurrentDictionary<Guid, DevelopmentOtpMessage> _messages = new();

    public bool TryGet(Guid challengeId, out DevelopmentOtpMessage? message) =>
        _messages.TryGetValue(challengeId, out message);

    internal void Set(Guid challengeId, DevelopmentOtpMessage message) =>
        _messages[challengeId] = message;
}

public sealed class DevelopmentSmsSender(DevelopmentOtpStore store) : ISmsSender
{
    public Task SendOtpAsync(
        string normalizedPhoneNumber,
        Guid challengeId,
        string code,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        store.Set(
            challengeId,
            new DevelopmentOtpMessage(normalizedPhoneNumber, code, expiresAtUtc));
        return Task.CompletedTask;
    }
}

public sealed class UnavailableSmsSender : ISmsSender
{
    public Task SendOtpAsync(
        string normalizedPhoneNumber,
        Guid challengeId,
        string code,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken) =>
        throw new IdentityException(
            IdentityErrorCodes.SmsProviderUnavailable,
            "ارسال پیامک در این محیط پیکربندی نشده است.");
}
