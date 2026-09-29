namespace MakanApp.Domain.Organization;

public sealed class OrganizationPerson
{
    private OrganizationPerson()
    {
    }

    private OrganizationPerson(
        Guid id,
        Guid organizationId,
        Guid personId,
        DateTime createdAtUtc,
        DateTime activatedAtUtc)
    {
        Id = id;
        OrganizationId = organizationId;
        PersonId = personId;
        Status = OrganizationPersonStatus.Active;
        CreatedAtUtc = createdAtUtc;
        ActivatedAtUtc = activatedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid PersonId { get; private set; }
    public OrganizationPersonStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ActivatedAtUtc { get; private set; }
    public DateTime? EndedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsActive =>
        Status == OrganizationPersonStatus.Active && !EndedAtUtc.HasValue;

    public static OrganizationPerson CreateActive(
        Guid organizationId,
        Guid personId,
        DateTime createdAtUtc) =>
        new(Guid.NewGuid(), organizationId, personId, createdAtUtc, createdAtUtc);

    public void End(DateTime endedAtUtc)
    {
        if (EndedAtUtc.HasValue)
        {
            return;
        }

        Status = OrganizationPersonStatus.Ended;
        EndedAtUtc = endedAtUtc;
    }
}
