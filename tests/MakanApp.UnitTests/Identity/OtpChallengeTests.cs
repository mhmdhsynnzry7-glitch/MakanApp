using MakanApp.Domain.Identity;
using Xunit;

namespace MakanApp.UnitTests.Identity;

public sealed class OtpChallengeTests
{
    private static readonly DateTime CreatedAtUtc =
        new(2026, 9, 29, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void VerifyReturnsExpiredAfterExpiry()
    {
        var challenge = CreateChallenge(maxFailedAttempts: 3);

        var result = challenge.Verify([1, 2, 3], CreatedAtUtc.AddMinutes(5));

        Assert.Equal(OtpVerificationResult.Expired, result);
    }

    [Fact]
    public void VerifyConsumesCodeAndPreventsReuse()
    {
        var challenge = CreateChallenge(maxFailedAttempts: 3);

        var firstResult = challenge.Verify([1, 2, 3], CreatedAtUtc.AddMinutes(1));
        var secondResult = challenge.Verify([1, 2, 3], CreatedAtUtc.AddMinutes(2));

        Assert.Equal(OtpVerificationResult.Valid, firstResult);
        Assert.Equal(OtpVerificationResult.AlreadyUsed, secondResult);
        Assert.Equal(CreatedAtUtc.AddMinutes(1), challenge.ConsumedAtUtc);
    }

    [Fact]
    public void VerifyLocksChallengeAtAttemptLimit()
    {
        var challenge = CreateChallenge(maxFailedAttempts: 2);

        var firstResult = challenge.Verify([9, 9, 9], CreatedAtUtc.AddSeconds(10));
        var secondResult = challenge.Verify([8, 8, 8], CreatedAtUtc.AddSeconds(20));
        var correctAfterLimit = challenge.Verify([1, 2, 3], CreatedAtUtc.AddSeconds(30));

        Assert.Equal(OtpVerificationResult.Invalid, firstResult);
        Assert.Equal(OtpVerificationResult.TooManyAttempts, secondResult);
        Assert.Equal(OtpVerificationResult.TooManyAttempts, correctAfterLimit);
        Assert.Equal(2, challenge.FailedAttempts);
    }

    private static OtpChallenge CreateChallenge(int maxFailedAttempts) =>
        OtpChallenge.Create(
            "+989121234567",
            [1, 2, 3],
            new byte[16],
            CreatedAtUtc,
            CreatedAtUtc.AddMinutes(5),
            maxFailedAttempts);
}
