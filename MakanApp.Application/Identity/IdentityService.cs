using System.Text.RegularExpressions;
using MakanApp.Domain.Identity;

namespace MakanApp.Application.Identity;

public sealed partial class IdentityService(
    IIdentityStore store,
    IIdentitySecurity security,
    ISmsSender smsSender,
    TimeProvider timeProvider,
    OtpOptions otpOptions,
    SessionOptions sessionOptions) : IIdentityService
{
    public async Task<RequestOtpResult> RequestOtpAsync(
        RequestOtpCommand command,
        CancellationToken cancellationToken)
    {
        var phoneNumber = NormalizePhoneNumber(command.PhoneNumber);
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var latestChallenge = await store.GetLatestOtpChallengeAsync(
            phoneNumber,
            cancellationToken);

        if (latestChallenge is not null &&
            latestChallenge.CreatedAtUtc + otpOptions.ResendDelay > nowUtc)
        {
            throw new IdentityException(
                IdentityErrorCodes.OtpRateLimited,
                "برای درخواست مجدد کد کمی صبر کنید.");
        }

        var recentCount = await store.CountOtpChallengesAsync(
            phoneNumber,
            nowUtc - otpOptions.RateLimitWindow,
            cancellationToken);

        if (recentCount >= otpOptions.MaxRequestsPerWindow)
        {
            throw new IdentityException(
                IdentityErrorCodes.OtpRateLimited,
                "تعداد درخواست‌های کد بیش از حد مجاز است.");
        }

        latestChallenge?.Supersede(nowUtc);

        var code = security.GenerateOtpCode();
        var salt = security.GenerateSalt();
        var expiresAtUtc = nowUtc + otpOptions.Lifetime;
        var challenge = OtpChallenge.Create(
            phoneNumber,
            security.HashOtp(code, salt),
            salt,
            nowUtc,
            expiresAtUtc,
            otpOptions.MaxFailedAttempts);

        store.Add(challenge);
        await store.SaveChangesAsync(cancellationToken);
        await smsSender.SendOtpAsync(
            phoneNumber,
            challenge.Id,
            code,
            expiresAtUtc,
            cancellationToken);

        return new RequestOtpResult(
            challenge.Id,
            expiresAtUtc,
            nowUtc + otpOptions.ResendDelay);
    }

    public async Task<VerifyOtpResult> VerifyOtpAsync(
        VerifyOtpCommand command,
        CancellationToken cancellationToken)
    {
        var phoneNumber = NormalizePhoneNumber(command.PhoneNumber);
        if (string.IsNullOrWhiteSpace(command.Code))
        {
            throw OtpException(OtpVerificationResult.Invalid);
        }

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        await using var transaction = await store.BeginSerializableTransactionAsync(cancellationToken);
        var challenge = await store.GetOtpChallengeAsync(
            command.ChallengeId,
            phoneNumber,
            cancellationToken);

        if (challenge is null)
        {
            throw OtpException(OtpVerificationResult.Invalid);
        }

        var verificationResult = challenge.Verify(
            security.HashOtp(command.Code.Trim(), challenge.Salt),
            nowUtc);

        if (verificationResult != OtpVerificationResult.Valid)
        {
            await store.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw OtpException(verificationResult);
        }

        var credential = await store.GetMobileCredentialAsync(phoneNumber, cancellationToken);
        User user;
        if (credential is null)
        {
            user = User.Create(nowUtc);
            credential = UserCredential.CreateMobile(user.Id, phoneNumber, nowUtc);
            store.Add(user);
            store.Add(credential);
        }
        else
        {
            user = await store.GetUserAsync(credential.UserId, cancellationToken)
                ?? throw new InvalidOperationException("کاربر مرتبط با روش ورود پیدا نشد.");
        }

        var rawToken = security.GenerateSessionToken();
        var expiresAtUtc = nowUtc + sessionOptions.Lifetime;
        var session = UserSession.Create(
            user.Id,
            security.HashSessionToken(rawToken),
            nowUtc,
            expiresAtUtc);
        store.Add(session);

        await store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new VerifyOtpResult(
            rawToken,
            "Bearer",
            expiresAtUtc,
            await BuildCurrentUserAsync(user, phoneNumber, cancellationToken));
    }

    public async Task<SessionIdentity?> AuthenticateAsync(
        string rawToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return null;
        }

        var session = await store.GetSessionByTokenHashAsync(
            security.HashSessionToken(rawToken),
            cancellationToken);
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;

        return session is not null && session.IsActive(nowUtc)
            ? new SessionIdentity(session.UserId, session.Id)
            : null;
    }

    public async Task<CurrentUserResult> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await store.GetUserAsync(userId, cancellationToken)
            ?? throw AuthRequiredException();
        var credential = await store.GetMobileCredentialByUserIdAsync(userId, cancellationToken)
            ?? throw AuthRequiredException();

        return await BuildCurrentUserAsync(
            user,
            credential.NormalizedIdentifier,
            cancellationToken);
    }

    public async Task<CurrentUserResult> CompleteProfileAsync(
        Guid userId,
        CompleteProfileCommand command,
        CancellationToken cancellationToken)
    {
        var firstName = NormalizeRequiredName(command.FirstName);
        var lastName = NormalizeRequiredName(command.LastName);
        var displayName = NormalizeDisplayName(command.DisplayName);
        var username = NormalizeUsername(command.Username, out var normalizedUsername);
        var user = await store.GetUserAsync(userId, cancellationToken)
            ?? throw AuthRequiredException();

        if (await store.UsernameExistsAsync(
                normalizedUsername,
                userId,
                cancellationToken))
        {
            throw new IdentityException(
                IdentityErrorCodes.UsernameAlreadyExists,
                "این نام کاربری قبلاً استفاده شده است.");
        }

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        Person person;
        if (user.PersonId.HasValue)
        {
            person = await store.GetPersonAsync(user.PersonId.Value, cancellationToken)
                ?? throw new InvalidOperationException("پروفایل مرتبط با کاربر پیدا نشد.");
            person.Update(firstName, lastName, displayName, nowUtc);
        }
        else
        {
            person = Person.Create(firstName, lastName, displayName, nowUtc);
            store.Add(person);
        }

        user.SetProfile(person.Id, username, normalizedUsername, nowUtc);
        await store.SaveChangesAsync(cancellationToken);

        var credential = await store.GetMobileCredentialByUserIdAsync(userId, cancellationToken)
            ?? throw AuthRequiredException();
        return await BuildCurrentUserAsync(
            user,
            credential.NormalizedIdentifier,
            cancellationToken);
    }

    public async Task LogoutAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await store.GetSessionAsync(sessionId, userId, cancellationToken);
        if (session is null)
        {
            throw AuthRequiredException();
        }

        session.Revoke(timeProvider.GetUtcNow().UtcDateTime);
        await store.SaveChangesAsync(cancellationToken);
    }

    private async Task<CurrentUserResult> BuildCurrentUserAsync(
        User user,
        string phoneNumber,
        CancellationToken cancellationToken)
    {
        Person? person = null;
        if (user.PersonId.HasValue)
        {
            person = await store.GetPersonAsync(user.PersonId.Value, cancellationToken);
        }

        return new CurrentUserResult(
            user.Id,
            phoneNumber,
            user.IsProfileComplete,
            person?.FirstName,
            person?.LastName,
            person?.DisplayName,
            user.Username);
    }

    private static string NormalizePhoneNumber(string phoneNumber)
    {
        if (!PhoneNumberNormalizer.TryNormalize(phoneNumber, out var normalized))
        {
            throw new IdentityException(
                IdentityErrorCodes.PhoneInvalid,
                "شماره تلفن معتبر نیست.");
        }

        return normalized;
    }

    private static string NormalizeRequiredName(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 100)
        {
            throw ProfileValidationException();
        }


        return normalized;
    }

    private static string? NormalizeDisplayName(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized?.Length > 200)
        {
            throw ProfileValidationException();
        }

        return normalized;
    }

    private static string NormalizeUsername(string? value, out string normalizedUsername)
    {
        var username = value?.Trim();
        if (string.IsNullOrWhiteSpace(username) ||
            username.Length is < 3 or > 32 ||
            !UsernamePattern().IsMatch(username))
        {
            throw ProfileValidationException();
        }

        normalizedUsername = username.ToUpperInvariant();
        return username;
    }

    private static IdentityException OtpException(OtpVerificationResult result) => result switch
    {
        OtpVerificationResult.Expired => new(
            IdentityErrorCodes.OtpExpired,
            "کد یک‌بارمصرف منقضی شده است."),
        OtpVerificationResult.AlreadyUsed => new(
            IdentityErrorCodes.OtpAlreadyUsed,
            "این کد یک‌بارمصرف قبلاً استفاده شده است."),
        OtpVerificationResult.TooManyAttempts => new(
            IdentityErrorCodes.OtpTooManyAttempts,
            "تعداد تلاش‌های ناموفق بیش از حد مجاز است."),
        _ => new IdentityException(
            IdentityErrorCodes.OtpInvalid,
            "کد یک‌بارمصرف معتبر نیست.")
    };

    private static IdentityException AuthRequiredException() =>
        new(IdentityErrorCodes.AuthRequired, "برای ادامه باید وارد شوید.");

    private static IdentityException ProfileValidationException() =>
        new(
            IdentityErrorCodes.ProfileValidationFailed,
            "اطلاعات پروفایل معتبر نیست.");

    [GeneratedRegex("^[A-Za-z0-9._]+$", RegexOptions.CultureInvariant)]
    private static partial Regex UsernamePattern();
}
