using MakanApp.Domain.Identity;

namespace MakanApp.Application.Identity;

public interface IIdentityService
{
    Task<RequestOtpResult> RequestOtpAsync(
        RequestOtpCommand command,
        CancellationToken cancellationToken);

    Task<VerifyOtpResult> VerifyOtpAsync(
        VerifyOtpCommand command,
        CancellationToken cancellationToken);

    Task<SessionIdentity?> AuthenticateAsync(
        string rawToken,
        CancellationToken cancellationToken);

    Task<CurrentUserResult> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<CurrentUserResult> CompleteProfileAsync(
        Guid userId,
        CompleteProfileCommand command,
        CancellationToken cancellationToken);

    Task LogoutAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken);
}

public interface ISmsSender
{
    Task SendOtpAsync(
        string normalizedPhoneNumber,
        Guid challengeId,
        string code,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken);
}

public interface IIdentitySecurity
{
    string GenerateOtpCode();
    byte[] GenerateSalt();
    byte[] HashOtp(string code, byte[] salt);
    string GenerateSessionToken();
    byte[] HashSessionToken(string rawToken);
}

public interface IIdentityTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}

public interface IIdentityStore
{
    Task<IIdentityTransaction> BeginSerializableTransactionAsync(
        CancellationToken cancellationToken);

    Task<OtpChallenge?> GetLatestOtpChallengeAsync(
        string normalizedPhoneNumber,
        CancellationToken cancellationToken);

    Task<OtpChallenge?> GetOtpChallengeAsync(
        Guid challengeId,
        string normalizedPhoneNumber,
        CancellationToken cancellationToken);

    Task<int> CountOtpChallengesAsync(
        string normalizedPhoneNumber,
        DateTime createdAfterUtc,
        CancellationToken cancellationToken);

    Task<UserCredential?> GetMobileCredentialAsync(
        string normalizedPhoneNumber,
        CancellationToken cancellationToken);

    Task<UserCredential?> GetMobileCredentialByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<User?> GetUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<Person?> GetPersonAsync(Guid personId, CancellationToken cancellationToken);

    Task<UserSession?> GetSessionByTokenHashAsync(
        byte[] tokenHash,
        CancellationToken cancellationToken);

    Task<UserSession?> GetSessionAsync(
        Guid sessionId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<bool> UsernameExistsAsync(
        string normalizedUsername,
        Guid exceptUserId,
        CancellationToken cancellationToken);

    void Add(OtpChallenge challenge);
    void Add(User user);
    void Add(Person person);
    void Add(UserCredential credential);
    void Add(UserSession session);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
