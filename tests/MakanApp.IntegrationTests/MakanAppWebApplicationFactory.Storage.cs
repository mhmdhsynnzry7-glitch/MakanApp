using MakanApp.Application.Storage;
using MakanApp.Domain.Storage;
using MakanApp.Infrastructure.Persistence;
using MakanApp.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MakanApp.IntegrationTests;

public sealed partial class MakanAppWebApplicationFactory
{
    public void FailNextStorageSave() =>
        Services.GetRequiredService<StorageFailureSwitch>().FailNextSave();

    public async Task<FileAssetDatabaseRecord> GetFileAssetAsync(Guid fileAssetId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.FileAssets
            .AsNoTracking()
            .Where(fileAsset => fileAsset.Id == fileAssetId)
            .Select(fileAsset => new FileAssetDatabaseRecord(
                fileAsset.Id,
                fileAsset.OrganizationId,
                fileAsset.UploadedByUserId,
                fileAsset.OriginalFileName,
                fileAsset.StorageKey,
                fileAsset.Status,
                fileAsset.SizeBytes,
                fileAsset.Sha256Hash,
                fileAsset.RejectedAtUtc,
                fileAsset.UnattachedExpiresAtUtc))
            .SingleAsync();
    }

    public async Task<FileAssetDatabaseRecord> GetLatestFileAssetForUserAsync(Guid userId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var id = await dbContext.FileAssets
            .Where(fileAsset => fileAsset.UploadedByUserId == userId)
            .OrderByDescending(fileAsset => fileAsset.CreatedAtUtc)
            .Select(fileAsset => fileAsset.Id)
            .FirstAsync();
        return await GetFileAssetAsync(id);
    }

    public async Task<bool> StoredBytesExistAsync(Guid fileAssetId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var storageKey = await dbContext.FileAssets
            .Where(fileAsset => fileAsset.Id == fileAssetId)
            .Select(fileAsset => fileAsset.StorageKey)
            .SingleAsync();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
        return await storage.ExistsAsync(storageKey, CancellationToken.None);
    }

    public async Task<bool> DuplicateStorageKeyIsRejectedAsync(Guid fileAssetId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var existing = await dbContext.FileAssets.AsNoTracking().SingleAsync(
            fileAsset => fileAsset.Id == fileAssetId);
        dbContext.FileAssets.Add(FileAsset.CreatePending(
            Guid.NewGuid(),
            existing.OrganizationId,
            existing.UploadedByUserId,
            "duplicate.txt",
            existing.StorageKey,
            "text/plain",
            DateTime.UtcNow,
            DateTime.UtcNow.AddHours(1)));
        try
        {
            await dbContext.SaveChangesAsync();
            return false;
        }
        catch (DbUpdateException)
        {
            return true;
        }
    }

    public async Task BumpFileAssetRowVersionAsync(Guid fileAssetId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [storage].[FileAssets] SET [UnattachedExpiresAtUtc] = DATEADD(second, 1, [UnattachedExpiresAtUtc]) WHERE [Id] = {fileAssetId}");
    }
}

public sealed record FileAssetDatabaseRecord(
    Guid Id,
    Guid? OrganizationId,
    Guid UploadedByUserId,
    string OriginalFileName,
    string StorageKey,
    FileAssetStatus Status,
    long SizeBytes,
    string? Sha256Hash,
    DateTime? RejectedAtUtc,
    DateTime UnattachedExpiresAtUtc);

public sealed class StorageFailureSwitch
{
    private int _failNextSave;

    public void FailNextSave() => Interlocked.Exchange(ref _failNextSave, 1);
    public bool ConsumeSaveFailure() => Interlocked.Exchange(ref _failNextSave, 0) == 1;
}

public sealed class FaultInjectingFileStorage(
    LocalFileStorage inner,
    StorageFailureSwitch failureSwitch) : IFileStorage
{
    public Task<StoredFileResult> SaveAsync(
        string storageKey,
        Stream content,
        long maxSizeBytes,
        CancellationToken cancellationToken)
    {
        if (failureSwitch.ConsumeSaveFailure())
        {
            throw new StorageException(
                StorageErrorCodes.FileStorageFailed,
                "Injected storage failure.");
        }

        return inner.SaveAsync(storageKey, content, maxSizeBytes, cancellationToken);
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken) =>
        inner.OpenReadAsync(storageKey, cancellationToken);

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken) =>
        inner.DeleteAsync(storageKey, cancellationToken);

    public Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken) =>
        inner.ExistsAsync(storageKey, cancellationToken);
}
