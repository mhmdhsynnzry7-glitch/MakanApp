namespace MakanApp.Application.Identity;

public sealed record RequestOtpCommand(string PhoneNumber);

public sealed record RequestOtpResult(
    Guid ChallengeId,
    DateTime ExpiresAtUtc,
    DateTime ResendAvailableAtUtc);

public sealed record VerifyOtpCommand(
    Guid ChallengeId,
    string PhoneNumber,
    string Code);

public sealed record VerifyOtpResult(
    string AccessToken,
    string TokenType,
    DateTime ExpiresAtUtc,
    CurrentUserResult User);

public sealed record CompleteProfileCommand(
    string FirstName,
    string LastName,
    string? DisplayName,
    string Username);

public sealed record CurrentUserResult(
    Guid Id,
    string PhoneNumber,
    bool IsProfileComplete,
    string? FirstName,
    string? LastName,
    string? DisplayName,
    string? Username);

public sealed record SessionIdentity(Guid UserId, Guid SessionId);
