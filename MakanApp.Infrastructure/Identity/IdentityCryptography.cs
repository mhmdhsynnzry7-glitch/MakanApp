using System.Security.Cryptography;
using System.Text;
using MakanApp.Application.Identity;

namespace MakanApp.Infrastructure.Identity;

public sealed class IdentityCryptography(byte[] key) : IIdentitySecurity
{
    public string GenerateOtpCode() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    public byte[] GenerateSalt() => RandomNumberGenerator.GetBytes(16);

    public byte[] HashOtp(string code, byte[] salt) =>
        ComputeHash("otp", code, salt);

    public string GenerateSessionToken()
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return token.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public byte[] HashSessionToken(string rawToken) =>
        ComputeHash("session", rawToken, []);

    private byte[] ComputeHash(string purpose, string value, byte[] salt)
    {
        using var hmac = new HMACSHA256(key);
        var purposeBytes = Encoding.UTF8.GetBytes(purpose);
        var valueBytes = Encoding.UTF8.GetBytes(value);
        var input = new byte[purposeBytes.Length + 1 + salt.Length + valueBytes.Length];

        purposeBytes.CopyTo(input, 0);
        input[purposeBytes.Length] = 0;
        salt.CopyTo(input, purposeBytes.Length + 1);
        valueBytes.CopyTo(input, purposeBytes.Length + 1 + salt.Length);

        return hmac.ComputeHash(input);
    }
}
