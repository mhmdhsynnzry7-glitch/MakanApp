namespace MakanApp.Domain.Identity;

public sealed class Person
{
    private Person()
    {
    }

    private Person(
        Guid id,
        string firstName,
        string lastName,
        string? displayName,
        DateTime createdAtUtc)
    {
        Id = id;
        FirstName = firstName;
        LastName = lastName;
        DisplayName = displayName;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string? DisplayName { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Person Create(
        string firstName,
        string lastName,
        string? displayName,
        DateTime createdAtUtc) =>
        new(Guid.NewGuid(), firstName, lastName, displayName, createdAtUtc);

    public void Update(
        string firstName,
        string lastName,
        string? displayName,
        DateTime updatedAtUtc)
    {
        FirstName = firstName;
        LastName = lastName;
        DisplayName = displayName;
        UpdatedAtUtc = updatedAtUtc;
    }
}
