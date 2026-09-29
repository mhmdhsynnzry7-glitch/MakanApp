namespace MakanApp.Application.Identity;

public static class IdentityErrorCodes
{
    public const string PhoneInvalid = "PHONE_INVALID";
    public const string OtpInvalid = "OTP_INVALID";
    public const string OtpExpired = "OTP_EXPIRED";
    public const string OtpAlreadyUsed = "OTP_ALREADY_USED";
    public const string OtpTooManyAttempts = "OTP_TOO_MANY_ATTEMPTS";
    public const string OtpRateLimited = "OTP_RATE_LIMITED";
    public const string AuthRequired = "AUTH_REQUIRED";
    public const string UsernameAlreadyExists = "USERNAME_ALREADY_EXISTS";
    public const string ProfileValidationFailed = "PROFILE_VALIDATION_FAILED";
    public const string SmsProviderUnavailable = "SMS_PROVIDER_UNAVAILABLE";
}
