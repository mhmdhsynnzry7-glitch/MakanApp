using System.Security.Cryptography;

namespace MakanApp.Domain.Identity;

public sealed class OtpChallenge
{
    private OtpChallenge()
    {
    }

    private OtpChallenge(
        Guid id,
        string normalizedPhoneNumber,
        byte[] codeHash,
        byte[] salt,
        DateTime createdAtUtc,
        DateTime expiresAtUtc,
        int maxFailedAttempts)
    {
        Id = id;
        NormalizedPhoneNumber = normalizedPhoneNumber;
        CodeHash = codeHash;
        Salt = salt;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        MaxFailedAttempts = maxFailedAttempts;
    }

    public Guid Id { get; private set; }
    public string NormalizedPhoneNumber { get; private set; } = string.Empty;
    public byte[] CodeHash { get; private set; } = [];
    public byte[] Salt { get; private set; } = [];
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? ConsumedAtUtc { get; private set; }
    public DateTime? SupersededAtUtc { get; private set; }
    public int FailedAttempts { get; private set; }
    public int MaxFailedAttempts { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static OtpChallenge Create(
        string normalizedPhoneNumber,
        byte[] codeHash,
        byte[] salt,
        DateTime createdAtUtc,
        DateTime expiresAtUtc,
        int maxFailedAttempts) =>
        new(
            Guid.NewGuid(),
            normalizedPhoneNumber,
            codeHash,
            salt,
            createdAtUtc,
            expiresAtUtc,
            maxFailedAttempts);

    public OtpVerificationResult Verify(byte[] submittedCodeHash, DateTime nowUtc)
    {
        if (ConsumedAtUtc.HasValue || SupersededAtUtc.HasValue)
        {
            return OtpVerificationResult.AlreadyUsed;
        }

        if (nowUtc >= ExpiresAtUtc)
        {
            return OtpVerificationResult.Expired;
        }

        if (FailedAttempts >= MaxFailedAttempts)
        {
            return OtpVerificationResult.TooManyAttempts;
        }

        if (!CryptographicOperations.FixedTimeEquals(CodeHash, submittedCodeHash))
        {
            FailedAttempts++;
            return FailedAttempts >= MaxFailedAttempts
                ? OtpVerificationResult.TooManyAttempts
                : OtpVerificationResult.Invalid;
        }

        ConsumedAtUtc = nowUtc;
        return OtpVerificationResult.Valid;
    }

    public void Supersede(DateTime supersededAtUtc)
    {
        if (!ConsumedAtUtc.HasValue && !SupersededAtUtc.HasValue)
        {
            SupersededAtUtc = supersededAtUtc;
        }
    }
}
