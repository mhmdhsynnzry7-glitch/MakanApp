namespace MakanApp.Domain.Identity;

public enum OtpVerificationResult
{
    Valid,
    Invalid,
    Expired,
    AlreadyUsed,
    TooManyAttempts
}
