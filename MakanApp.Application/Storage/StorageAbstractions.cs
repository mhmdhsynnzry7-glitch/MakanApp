using MakanApp.Domain.Storage;

namespace MakanApp.Application.Storage;

public interface IFileStorage
{
    Task<StoredFileResult> SaveAsync(
        string storageKey,
        Stream content,
        long maxSizeBytes,
        CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken);
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken);
}

public interface IFileAssetStore
{
    Task<FileAsset?> GetAsync(Guid fileAssetId, CancellationToken cancellationToken);
    Task<FileAsset?> GetForUpdateAsync(Guid fileAssetId, CancellationToken cancellationToken);
    void Add(FileAsset fileAsset);
    void SetOriginalRowVersion(FileAsset fileAsset, byte[] rowVersion);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IFileAssetBoundAccessResolver
{
    Task<bool> CanReadBoundFileAsync(
        FileAsset fileAsset,
        Organization.AccessContext accessContext,
        CancellationToken cancellationToken);
}

public interface IStorageService
{
    Task<FileAssetResult> UploadFileAsync(
        Guid userId,
        Guid sessionId,
        UploadFileCommand command,
        CancellationToken cancellationToken);

    Task<FileAssetResult> GetFileMetadataAsync(
        Guid userId,
        Guid sessionId,
        Guid fileAssetId,
        CancellationToken cancellationToken);

    Task<FileDownloadResult> DownloadFileAsync(
        Guid userId,
        Guid sessionId,
        Guid fileAssetId,
        CancellationToken cancellationToken);

    Task<FileAssetResult> DeleteFileAsync(
        Guid userId,
        Guid sessionId,
        Guid fileAssetId,
        string expectedRowVersion,
        CancellationToken cancellationToken);
}
