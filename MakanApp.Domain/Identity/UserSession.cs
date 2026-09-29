namespace MakanApp.Domain.Identity;

public sealed class UserSession
{
    private UserSession()
    {
    }

    private UserSession(
        Guid id,
        Guid userId,
        byte[] tokenHash,
        DateTime createdAtUtc,
        DateTime expiresAtUtc)
    {
        Id = id;
        UserId = userId;
        TokenHash = tokenHash;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public byte[] TokenHash { get; private set; } = [];
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public Guid? SelectedMembershipId { get; private set; }
    public string? SelectedRole { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static UserSession Create(
        Guid userId,
        byte[] tokenHash,
        DateTime createdAtUtc,
        DateTime expiresAtUtc) =>
        new(Guid.NewGuid(), userId, tokenHash, createdAtUtc, expiresAtUtc);

    public bool IsActive(DateTime nowUtc) =>
        !RevokedAtUtc.HasValue && nowUtc < ExpiresAtUtc;

    public void Revoke(DateTime revokedAtUtc)
    {
        RevokedAtUtc ??= revokedAtUtc;
    }

    public void SelectPersonalWorkspace()
    {
        SelectedMembershipId = null;
        SelectedRole = null;
    }

    public void SelectOrganizationWorkspace(Guid membershipId, string role)
    {
        SelectedMembershipId = membershipId;
        SelectedRole = role;
    }
}
