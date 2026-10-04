using MakanApp.Application.Organization;
using MakanApp.Application.Storage;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Storage;
using Xunit;

namespace MakanApp.UnitTests.Storage;

public sealed class StorageModelTests
{
    private readonly StorageOptions _options = new()
    {
        MaxFileSizeBytes = 100,
        AllowedContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "text/plain"
        },
        UnattachedLifetime = TimeSpan.FromHours(1)
    };

    [Fact]
    public void EmptyFileIsRejected()
    {
        var exception = Assert.Throws<StorageException>(() => Policy().Validate(
            new UploadFileCommand("file.txt", "text/plain", 0, Stream.Null)));

        Assert.Equal(StorageErrorCodes.FileEmpty, exception.Code);
    }

    [Fact]
    public void OversizedFileIsRejected()
    {
        var exception = Assert.Throws<StorageException>(() => Policy().Validate(
            new UploadFileCommand("file.txt", "text/plain", 101, Stream.Null)));

        Assert.Equal(StorageErrorCodes.FileTooLarge, exception.Code);
    }

    [Fact]
    public void DisallowedContentTypeIsRejected()
    {
        var exception = Assert.Throws<StorageException>(() => Policy().Validate(
            new UploadFileCommand("file.exe", "application/octet-stream", 10, Stream.Null)));

        Assert.Equal(StorageErrorCodes.FileTypeNotAllowed, exception.Code);
    }

    [Fact]
    public void UnsafeClientPathIsReducedToSafeDisplayName()
    {
        var result = Policy().Validate(
            new UploadFileCommand("../../private/<report>.txt", "text/plain", 10, Stream.Null));

        Assert.Equal("report.txt", result.OriginalFileName);
    }

    [Fact]
    public void StorageKeyIsServerGeneratedAndUnique()
    {
        var id = Guid.NewGuid();
        var nowUtc = DateTime.UtcNow;
        var first = FileUploadPolicy.CreateStorageKey(id, nowUtc);
        var second = FileUploadPolicy.CreateStorageKey(id, nowUtc);

        Assert.NotEqual(first, second);
        Assert.DoesNotContain("report.txt", first);
        Assert.Contains(id.ToString("N"), first);
    }

    [Fact]
    public void PendingFileCanBecomeReadyWithIntegrityMetadata()
    {
        var file = CreatePending();

        file.MarkReady(4, new string('A', 64), DateTime.UtcNow);

        Assert.Equal(FileAssetStatus.Ready, file.Status);
        Assert.Equal(4, file.SizeBytes);
        Assert.NotNull(file.CompletedAtUtc);
    }

    [Fact]
    public void RejectedFileCannotBecomeReady()
    {
        var file = CreatePending();
        file.MarkRejected(DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            file.MarkReady(4, new string('A', 64), DateTime.UtcNow));
        Assert.Equal(FileAssetStatus.Rejected, file.Status);
    }

    [Fact]
    public void DeletedFileCannotBecomeReadyOrBeDeletedTwice()
    {
        var file = CreatePending();
        file.MarkDeleted(DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            file.MarkReady(4, new string('A', 64), DateTime.UtcNow));
        Assert.Throws<InvalidOperationException>(() => file.MarkDeleted(DateTime.UtcNow));
    }

    [Fact]
    public void UploaderInMatchingPersonalContextCanAccessPersonalFile()
    {
        var userId = Guid.NewGuid();
        var file = CreatePending(userId: userId);
        var context = Context(userId, WorkspaceType.Personal, null);

        Assert.True(FileAccessPolicy.IsUploader(file, context));
        Assert.True(FileAccessPolicy.IsCurrentScope(file, context));
    }

    [Fact]
    public void DifferentUserCannotAccessUnattachedFile()
    {
        var file = CreatePending(userId: Guid.NewGuid());
        var context = Context(Guid.NewGuid(), WorkspaceType.Personal, null);

        Assert.False(FileAccessPolicy.IsUploader(file, context));
    }

    [Fact]
    public void OrganizationFileRequiresMatchingCurrentOrganization()
    {
        var userId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var file = CreatePending(organizationId, userId);

        Assert.True(FileAccessPolicy.IsCurrentScope(
            file,
            Context(userId, WorkspaceType.Organization, organizationId)));
        Assert.False(FileAccessPolicy.IsCurrentScope(
            file,
            Context(userId, WorkspaceType.Organization, Guid.NewGuid())));
    }

    [Fact]
    public void PersonalWorkspaceCannotAccessOrganizationFile()
    {
        var userId = Guid.NewGuid();
        var file = CreatePending(Guid.NewGuid(), userId);

        Assert.False(FileAccessPolicy.IsCurrentScope(
            file,
            Context(userId, WorkspaceType.Personal, null)));
    }

    private FileUploadPolicy Policy() => new(_options);

    private static FileAsset CreatePending(Guid? organizationId = null, Guid? userId = null)
    {
        var nowUtc = DateTime.UtcNow;
        return FileAsset.CreatePending(
            Guid.NewGuid(),
            organizationId,
            userId ?? Guid.NewGuid(),
            "report.txt",
            $"2026/10/{Guid.NewGuid():N}",
            "text/plain",
            nowUtc,
            nowUtc.AddHours(1));
    }

    private static AccessContext Context(
        Guid userId,
        WorkspaceType workspaceType,
        Guid? organizationId) =>
        new(
            userId,
            userId,
            Guid.NewGuid(),
            workspaceType,
            organizationId,
            workspaceType == WorkspaceType.Organization ? Guid.NewGuid() : null,
            workspaceType == WorkspaceType.Organization ? OrganizationRole.Student : null,
            null);
}
