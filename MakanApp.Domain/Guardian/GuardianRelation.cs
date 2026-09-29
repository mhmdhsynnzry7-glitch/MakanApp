namespace MakanApp.Domain.Guardian;

public sealed class GuardianRelation
{
    private GuardianRelation()
    {
    }

    private GuardianRelation(
        Guid id,
        Guid organizationId,
        Guid guardianUserId,
        Guid learnerOrganizationPersonId,
        GuardianRelationStatus status,
        DateTime validFromUtc,
        DateTime createdAtUtc)
    {
        Id = id;
        OrganizationId = organizationId;
        GuardianUserId = guardianUserId;
        LearnerOrganizationPersonId = learnerOrganizationPersonId;
        Status = status;
        ValidFromUtc = validFromUtc;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid GuardianUserId { get; private set; }
    public Guid LearnerOrganizationPersonId { get; private set; }
    public GuardianRelationStatus Status { get; private set; }
    public DateTime ValidFromUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? EndedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static GuardianRelation CreatePending(
        Guid organizationId,
        Guid guardianUserId,
        Guid learnerOrganizationPersonId,
        DateTime createdAtUtc) =>
        new(
            Guid.NewGuid(),
            organizationId,
            guardianUserId,
            learnerOrganizationPersonId,
            GuardianRelationStatus.Pending,
            createdAtUtc,
            createdAtUtc);

    public static GuardianRelation CreateActive(
        Guid organizationId,
        Guid guardianUserId,
        Guid learnerOrganizationPersonId,
        DateTime createdAtUtc) =>
        new(
            Guid.NewGuid(),
            organizationId,
            guardianUserId,
            learnerOrganizationPersonId,
            GuardianRelationStatus.Active,
            createdAtUtc,
            createdAtUtc);

    public bool IsActiveAt(DateTime nowUtc) =>
        Status == GuardianRelationStatus.Active &&
        !EndedAtUtc.HasValue &&
        ValidFromUtc <= nowUtc;

    public void Activate(DateTime activatedAtUtc)
    {
        if (Status != GuardianRelationStatus.Pending)
        {
            throw new InvalidOperationException(
                "فقط رابطه سرپرستی در انتظار را می‌توان فعال کرد.");
        }

        Status = GuardianRelationStatus.Active;
        ValidFromUtc = activatedAtUtc;
    }

    public void End(DateTime endedAtUtc)
    {
        if (Status is GuardianRelationStatus.Ended or GuardianRelationStatus.Revoked)
        {
            return;
        }

        Status = GuardianRelationStatus.Ended;
        EndedAtUtc = endedAtUtc;
    }

    public void Revoke(DateTime revokedAtUtc)
    {
        if (Status is GuardianRelationStatus.Ended or GuardianRelationStatus.Revoked)
        {
            return;
        }

        Status = GuardianRelationStatus.Revoked;
        EndedAtUtc = revokedAtUtc;
    }
}
