namespace MakanApp.Domain.Storage;

public sealed class FileAsset
{
    private FileAsset()
    {
    }

    private FileAsset(
        Guid id,
        Guid? organizationId,
        Guid uploadedByUserId,
        string originalFileName,
        string storageKey,
        string contentType,
        DateTime createdAtUtc,
        DateTime unattachedExpiresAtUtc)
    {
        if (id == Guid.Empty || uploadedByUserId == Guid.Empty)
        {
            throw new ArgumentException("شناسه فایل و بارگذار الزامی است.");
        }

        if (string.IsNullOrWhiteSpace(originalFileName) || string.IsNullOrWhiteSpace(storageKey) ||
            string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentException("فراداده فایل کامل نیست.");
        }

        if (unattachedExpiresAtUtc <= createdAtUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(unattachedExpiresAtUtc));
        }

        Id = id;
        OrganizationId = organizationId;
        UploadedByUserId = uploadedByUserId;
        OriginalFileName = originalFileName;
        StorageKey = storageKey;
        ContentType = contentType;
        Status = FileAssetStatus.Pending;
        CreatedAtUtc = createdAtUtc;
        UnattachedExpiresAtUtc = unattachedExpiresAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public Guid UploadedByUserId { get; private set; }
    public string OriginalFileName { get; private set; } = string.Empty;
    public string StorageKey { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public string? Sha256Hash { get; private set; }
    public FileAssetStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public DateTime? RejectedAtUtc { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    public DateTime UnattachedExpiresAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static FileAsset CreatePending(
        Guid id,
        Guid? organizationId,
        Guid uploadedByUserId,
        string originalFileName,
        string storageKey,
        string contentType,
        DateTime createdAtUtc,
        DateTime unattachedExpiresAtUtc) =>
        new(
            id,
            organizationId,
            uploadedByUserId,
            originalFileName,
            storageKey,
            contentType,
            createdAtUtc,
            unattachedExpiresAtUtc);

    public void MarkReady(long sizeBytes, string sha256Hash, DateTime completedAtUtc)
    {
        if (Status != FileAssetStatus.Pending)
        {
            throw new InvalidOperationException("فقط فایل در انتظار می‌تواند آماده شود.");
        }

        if (sizeBytes <= 0 || sha256Hash.Length != 64 || completedAtUtc < CreatedAtUtc)
        {
            throw new ArgumentException("نتیجه ذخیره فایل معتبر نیست.");
        }

        SizeBytes = sizeBytes;
        Sha256Hash = sha256Hash;
        CompletedAtUtc = completedAtUtc;
        Status = FileAssetStatus.Ready;
    }

    public void MarkRejected(DateTime rejectedAtUtc)
    {
        if (Status is FileAssetStatus.Rejected or FileAssetStatus.Deleted)
        {
            return;
        }

        RejectedAtUtc = rejectedAtUtc;
        Status = FileAssetStatus.Rejected;
        SizeBytes = 0;
        Sha256Hash = null;
        CompletedAtUtc = null;
    }

    public void MarkDeleted(DateTime deletedAtUtc)
    {
        if (Status == FileAssetStatus.Deleted)
        {
            throw new InvalidOperationException("فایل قبلاً حذف شده است.");
        }

        DeletedAtUtc = deletedAtUtc;
        Status = FileAssetStatus.Deleted;
    }
}
