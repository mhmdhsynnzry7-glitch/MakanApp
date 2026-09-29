namespace MakanApp.Domain.Organization;

public sealed class Invitation
{
    private Invitation()
    {
    }

    private Invitation(
        Guid id,
        Guid organizationId,
        Guid destinationUserId,
        Guid invitedByUserId,
        OrganizationRole role,
        DateTime createdAtUtc,
        DateTime expiresAtUtc)
    {
        Id = id;
        OrganizationId = organizationId;
        DestinationUserId = destinationUserId;
        InvitedByUserId = invitedByUserId;
        Role = role;
        Status = InvitationStatus.Pending;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid DestinationUserId { get; private set; }
    public Guid InvitedByUserId { get; private set; }
    public OrganizationRole Role { get; private set; }
    public InvitationStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? AcceptedAtUtc { get; private set; }
    public DateTime? DeclinedAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public Guid? AcceptedMembershipId { get; private set; }
    public Guid? AcceptedRoleAssignmentId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Invitation Create(
        Guid organizationId,
        Guid destinationUserId,
        Guid invitedByUserId,
        OrganizationRole role,
        DateTime createdAtUtc,
        DateTime expiresAtUtc)
    {
        if (expiresAtUtc <= createdAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expiresAtUtc),
                "زمان انقضای دعوت باید پس از زمان ایجاد باشد.");
        }

        return new Invitation(
            Guid.NewGuid(),
            organizationId,
            destinationUserId,
            invitedByUserId,
            role,
            createdAtUtc,
            expiresAtUtc);
    }

    public InvitationStatus GetEffectiveStatus(DateTime nowUtc) =>
        Status == InvitationStatus.Pending && nowUtc >= ExpiresAtUtc
            ? InvitationStatus.Expired
            : Status;

    public InvitationAcceptanceResult Accept(
        Guid membershipId,
        Guid roleAssignmentId,
        DateTime acceptedAtUtc)
    {
        if (Status == InvitationStatus.Accepted)
        {
            return InvitationAcceptanceResult.AlreadyAccepted;
        }

        if (Status == InvitationStatus.Revoked)
        {
            return InvitationAcceptanceResult.Revoked;
        }

        if (Status == InvitationStatus.Declined)
        {
            return InvitationAcceptanceResult.Declined;
        }

        if (Status == InvitationStatus.Expired || acceptedAtUtc >= ExpiresAtUtc)
        {
            Status = InvitationStatus.Expired;
            return InvitationAcceptanceResult.Expired;
        }

        Status = InvitationStatus.Accepted;
        AcceptedAtUtc = acceptedAtUtc;
        AcceptedMembershipId = membershipId;
        AcceptedRoleAssignmentId = roleAssignmentId;
        return InvitationAcceptanceResult.Accepted;
    }

    public InvitationStatus Decline(DateTime declinedAtUtc)
    {
        if (Status != InvitationStatus.Pending)
        {
            return GetEffectiveStatus(declinedAtUtc);
        }

        if (declinedAtUtc >= ExpiresAtUtc)
        {
            Status = InvitationStatus.Expired;
            return Status;
        }

        Status = InvitationStatus.Declined;
        DeclinedAtUtc = declinedAtUtc;
        return Status;
    }

    public void Revoke(DateTime revokedAtUtc)
    {
        if (Status == InvitationStatus.Pending)
        {
            Status = InvitationStatus.Revoked;
            RevokedAtUtc = revokedAtUtc;
        }
    }
}
