namespace MakanApp.Domain.Identity;

public sealed class UserCredential
{
    private UserCredential()
    {
    }

    private UserCredential(
        Guid id,
        Guid userId,
        CredentialKind kind,
        string normalizedIdentifier,
        DateTime createdAtUtc)
    {
        Id = id;
        UserId = userId;
        Kind = kind;
        NormalizedIdentifier = normalizedIdentifier;
        CreatedAtUtc = createdAtUtc;
        VerifiedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public CredentialKind Kind { get; private set; }
    public string NormalizedIdentifier { get; private set; } = string.Empty;
    public DateTime VerifiedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static UserCredential CreateMobile(
        Guid userId,
        string normalizedPhoneNumber,
        DateTime createdAtUtc) =>
        new(
            Guid.NewGuid(),
            userId,
            CredentialKind.MobilePhone,
            normalizedPhoneNumber,
            createdAtUtc);
}
