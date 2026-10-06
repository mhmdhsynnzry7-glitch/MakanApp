using MakanApp.Application.Organization;
using MakanApp.Domain.Storage;

namespace MakanApp.Application.Storage;

public sealed class StorageService(
    IAccessContextResolver accessContextResolver,
    IFileAssetStore store,
    IFileAssetBoundAccessResolver boundAccessResolver,
    IFileStorage fileStorage,
    FileUploadPolicy uploadPolicy,
    StorageOptions options,
    TimeProvider timeProvider) : IStorageService
{
    public async Task<FileAssetResult> UploadFileAsync(
        Guid userId,
        Guid sessionId,
        UploadFileCommand command,
        CancellationToken cancellationToken)
    {
        var context = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        var validated = uploadPolicy.Validate(command);
        var nowUtc = UtcNow();
        var id = Guid.NewGuid();
        var fileAsset = FileAsset.CreatePending(
            id,
            context.OrganizationId,
            context.UserId,
            validated.OriginalFileName,
            FileUploadPolicy.CreateStorageKey(id, nowUtc),
            validated.ContentType,
            nowUtc,
            nowUtc.Add(options.UnattachedLifetime));

        store.Add(fileAsset);
        await store.SaveChangesAsync(cancellationToken);

        try
        {
            var stored = await fileStorage.SaveAsync(
                fileAsset.StorageKey,
                command.Content,
                options.MaxFileSizeBytes,
                cancellationToken);
            if (stored.SizeBytes != command.DeclaredSizeBytes)
            {
                throw new StorageException(
                    StorageErrorCodes.FileUploadFailed,
                    "اندازه واقعی فایل با اندازه اعلام‌شده یکسان نیست.");
            }

            fileAsset.MarkReady(stored.SizeBytes, stored.Sha256Hash, UtcNow());
            await store.SaveChangesAsync(cancellationToken);
            return ToResult(fileAsset);
        }
        catch (OperationCanceledException)
        {
            await TryRejectAndCleanAsync(fileAsset, CancellationToken.None);
            throw;
        }
        catch (StorageException)
        {
            await TryRejectAndCleanAsync(fileAsset, CancellationToken.None);
            throw;
        }
        catch (Exception exception)
        {
            await TryRejectAndCleanAsync(fileAsset, CancellationToken.None);
            throw new StorageException(
                StorageErrorCodes.FileUploadFailed,
                "تکمیل بارگذاری فایل ناموفق بود.",
                exception);
        }
    }

    public async Task<FileAssetResult> GetFileMetadataAsync(
        Guid userId,
        Guid sessionId,
        Guid fileAssetId,
        CancellationToken cancellationToken)
    {
        var context = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        var fileAsset = await GetAuthorizedAsync(context, fileAssetId, cancellationToken);
        return ToResult(fileAsset);
    }

    public async Task<FileDownloadResult> DownloadFileAsync(
        Guid userId,
        Guid sessionId,
        Guid fileAssetId,
        CancellationToken cancellationToken)
    {
        var context = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        var fileAsset = await GetAuthorizedAsync(context, fileAssetId, cancellationToken);
        if (fileAsset.Status != FileAssetStatus.Ready)
        {
            throw Error(StorageErrorCodes.FileNotReady, "فایل هنوز برای دریافت آماده نیست.");
        }

        try
        {
            var stream = await fileStorage.OpenReadAsync(fileAsset.StorageKey, cancellationToken);
            return new FileDownloadResult(
                stream,
                fileAsset.ContentType,
                fileAsset.OriginalFileName,
                fileAsset.SizeBytes);
        }
        catch (StorageException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new StorageException(
                StorageErrorCodes.FileStorageFailed,
                "دسترسی به محتوای فایل در مخزن ممکن نیست.",
                exception);
        }
    }

    public async Task<FileAssetResult> DeleteFileAsync(
        Guid userId,
        Guid sessionId,
        Guid fileAssetId,
        string expectedRowVersion,
        CancellationToken cancellationToken)
    {
        var context = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        var fileAsset = await store.GetForUpdateAsync(fileAssetId, cancellationToken) ?? throw NotFound();
        EnsureUploaderAuthorized(fileAsset, context);

        if (fileAsset.Status == FileAssetStatus.Deleted)
        {
            throw Error(StorageErrorCodes.FileAlreadyDeleted, "فایل قبلاً حذف شده است.");
        }

        if (fileAsset.RetainedAtUtc.HasValue)
        {
            throw Error(StorageErrorCodes.FileInUse, "فایل بخشی از یک سابقه رسمی است و قابل حذف نیست.");
        }

        var rowVersion = DecodeRowVersion(expectedRowVersion);
        if (!fileAsset.RowVersion.SequenceEqual(rowVersion))
        {
            throw Error(StorageErrorCodes.ConcurrencyConflict, "فایل هم‌زمان تغییر کرده است؛ فراداده را دوباره دریافت کنید.");
        }

        store.SetOriginalRowVersion(fileAsset, rowVersion);
        fileAsset.MarkDeleted(UtcNow());
        await store.SaveChangesAsync(cancellationToken);

        try
        {
            await fileStorage.DeleteAsync(fileAsset.StorageKey, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // رکورد Deleted و StorageKey برای پاک‌سازی مجدد قابل شناسایی می‌مانند.
        }

        return ToResult(fileAsset);
    }

    private async Task<FileAsset> GetAuthorizedAsync(
        AccessContext context,
        Guid fileAssetId,
        CancellationToken cancellationToken)
    {
        var fileAsset = await store.GetAsync(fileAssetId, cancellationToken) ?? throw NotFound();
        if (FileAccessPolicy.IsUploader(fileAsset, context))
        {
            EnsureCurrentScope(fileAsset, context);
        }
        else if (!FileAccessPolicy.IsCurrentScope(fileAsset, context) ||
                 !await boundAccessResolver.CanReadBoundFileAsync(
                     fileAsset,
                     context,
                     cancellationToken))
        {
            throw NotFound();
        }
        if (fileAsset.Status == FileAssetStatus.Deleted)
        {
            throw NotFound();
        }

        return fileAsset;
    }

    private static void EnsureUploaderAuthorized(FileAsset fileAsset, AccessContext context)
    {
        if (!FileAccessPolicy.IsUploader(fileAsset, context))
        {
            throw NotFound();
        }

        EnsureCurrentScope(fileAsset, context);
    }

    private static void EnsureCurrentScope(FileAsset fileAsset, AccessContext context)
    {
        if (!FileAccessPolicy.IsCurrentScope(fileAsset, context))
        {
            throw Error(StorageErrorCodes.FileNotAllowed, "دسترسی به فایل در فضای کاری فعلی مجاز نیست.");
        }
    }

    private async Task TryRejectAndCleanAsync(FileAsset fileAsset, CancellationToken cancellationToken)
    {
        try
        {
            fileAsset.MarkRejected(UtcNow());
            await store.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Pending باقی‌مانده با زمان انقضا برای reconciliation بعدی قابل شناسایی است.
        }

        try
        {
            await fileStorage.DeleteAsync(fileAsset.StorageKey, cancellationToken);
        }
        catch
        {
            // کلید رکورد امکان پاک‌سازی orphan را در job آینده حفظ می‌کند.
        }
    }

    private static byte[] DecodeRowVersion(string value)
    {
        try
        {
            var rowVersion = Convert.FromBase64String(value);
            return rowVersion.Length == 8 ? rowVersion : throw new FormatException();
        }
        catch (FormatException)
        {
            throw Error(StorageErrorCodes.ConcurrencyConflict, "نسخه رکورد معتبر نیست.");
        }
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private static StorageException NotFound() =>
        Error(StorageErrorCodes.FileNotFound, "فایل پیدا نشد.");

    private static StorageException Error(string code, string message) => new(code, message);

    private static FileAssetResult ToResult(FileAsset fileAsset) =>
        new(
            fileAsset.Id,
            fileAsset.OrganizationId,
            fileAsset.UploadedByUserId,
            fileAsset.OriginalFileName,
            fileAsset.ContentType,
            fileAsset.SizeBytes,
            fileAsset.Sha256Hash,
            fileAsset.Status,
            fileAsset.CreatedAtUtc,
            fileAsset.CompletedAtUtc,
            fileAsset.DeletedAtUtc,
            fileAsset.RetainedAtUtc,
            fileAsset.UnattachedExpiresAtUtc,
            Convert.ToBase64String(fileAsset.RowVersion));
}
