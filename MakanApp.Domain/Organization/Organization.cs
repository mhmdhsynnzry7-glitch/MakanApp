namespace MakanApp.Domain.Organization;

public sealed class Organization
{
    private Organization()
    {
    }

    private Organization(Guid id, string name, DateTime createdAtUtc)
    {
        Id = id;
        Name = name;
        Status = OrganizationStatus.Active;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public OrganizationStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsActive => Status == OrganizationStatus.Active;

    public static Organization Create(string name, DateTime createdAtUtc)
    {
        var normalizedName = name?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName) || normalizedName.Length > 200)
        {
            throw new ArgumentException("نام سازمان معتبر نیست.", nameof(name));
        }

        return new Organization(Guid.NewGuid(), normalizedName, createdAtUtc);
    }

    public void Suspend() => Status = OrganizationStatus.Suspended;
}
