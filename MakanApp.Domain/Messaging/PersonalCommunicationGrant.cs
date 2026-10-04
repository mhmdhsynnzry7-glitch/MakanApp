namespace MakanApp.Domain.Messaging;

public sealed class PersonalCommunicationGrant
{
    private PersonalCommunicationGrant()
    {
    }

    private PersonalCommunicationGrant(
        Guid id,
        DirectUserPair pair,
        DateTime grantedAtUtc)
    {
        Id = id;
        LowerUserId = pair.LowerUserId;
        HigherUserId = pair.HigherUserId;
        Status = PersonalCommunicationGrantStatus.Active;
        GrantedAtUtc = grantedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid LowerUserId { get; private set; }
    public Guid HigherUserId { get; private set; }
    public PersonalCommunicationGrantStatus Status { get; private set; }
    public DateTime GrantedAtUtc { get; private set; }
    public DateTime? EndedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsActive => Status == PersonalCommunicationGrantStatus.Active && !EndedAtUtc.HasValue;

    public static PersonalCommunicationGrant CreateActive(
        Guid firstUserId,
        Guid secondUserId,
        DateTime grantedAtUtc)
    {
        if (grantedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان اعطای مجوز باید UTC باشد.", nameof(grantedAtUtc));
        }

        return new PersonalCommunicationGrant(
            Guid.NewGuid(),
            DirectUserPair.Create(firstUserId, secondUserId),
            grantedAtUtc);
    }

    public void Revoke(DateTime revokedAtUtc)
    {
        if (!IsActive)
        {
            return;
        }

        Status = PersonalCommunicationGrantStatus.Revoked;
        EndedAtUtc = revokedAtUtc;
    }
}
