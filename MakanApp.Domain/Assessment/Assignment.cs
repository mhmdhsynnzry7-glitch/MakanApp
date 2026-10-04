namespace MakanApp.Domain.Assessment;

public sealed class Assignment
{
    private Assignment()
    {
    }

    private Assignment(
        Guid id,
        Guid organizationId,
        Guid classId,
        Guid createdByMembershipId,
        DateTime createdAtUtc)
    {
        Id = id;
        OrganizationId = organizationId;
        ClassId = classId;
        CreatedByMembershipId = createdByMembershipId;
        CurrentVersionNumber = 1;
        Status = AssignmentStatus.Draft;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ClassId { get; private set; }
    public Guid CreatedByMembershipId { get; private set; }
    public int CurrentVersionNumber { get; private set; }
    public AssignmentStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsDraft => Status == AssignmentStatus.Draft;
    public bool IsRecipientVisible => Status is AssignmentStatus.Published or AssignmentStatus.Closed;

    public static Assignment CreateDraft(
        Guid organizationId,
        Guid classId,
        Guid createdByMembershipId,
        DateTime createdAtUtc) =>
        new(
            Guid.NewGuid(),
            organizationId,
            classId,
            createdByMembershipId,
            createdAtUtc);

    public void Publish(DateTime publishedAtUtc)
    {
        if (!IsDraft)
        {
            throw new InvalidOperationException("فقط تکلیف پیش‌نویس قابل انتشار است.");
        }

        Status = AssignmentStatus.Published;
        PublishedAtUtc = publishedAtUtc;
        UpdatedAtUtc = publishedAtUtc;
    }

    public void MarkDraftUpdated(DateTime updatedAtUtc)
    {
        if (!IsDraft)
        {
            throw new InvalidOperationException("فقط تکلیف پیش‌نویس قابل ویرایش است.");
        }

        UpdatedAtUtc = updatedAtUtc;
    }
}
