namespace MakanApp.Domain.Identity;

public sealed class User
{
    private User()
    {
    }

    private User(Guid id, DateTime createdAtUtc)
    {
        Id = id;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid? PersonId { get; private set; }
    public string? Username { get; private set; }
    public string? NormalizedUsername { get; private set; }
    public DateTime? ProfileCompletedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsProfileComplete =>
        PersonId.HasValue && !string.IsNullOrWhiteSpace(Username);

    public static User Create(DateTime createdAtUtc) =>
        new(Guid.NewGuid(), createdAtUtc);

    public void SetProfile(
        Guid personId,
        string username,
        string normalizedUsername,
        DateTime updatedAtUtc)
    {
        PersonId = personId;
        Username = username;
        NormalizedUsername = normalizedUsername;
        UpdatedAtUtc = updatedAtUtc;
        ProfileCompletedAtUtc ??= updatedAtUtc;
    }
}
