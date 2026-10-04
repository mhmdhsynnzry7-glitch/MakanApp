using System.Security.Cryptography;
using MakanApp.Application.Storage;

namespace MakanApp.Infrastructure.Storage;

public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _rootPath;

    public LocalFileStorage(StorageOptions options)
    {
        var expanded = Environment.ExpandEnvironmentVariables(options.RootPath);
        if (string.IsNullOrWhiteSpace(expanded) || !Path.IsPathFullyQualified(expanded))
        {
            throw new InvalidOperationException(
                "Storage:RootPath must be an absolute path supplied by Development or Testing configuration.");
        }

        _rootPath = Path.GetFullPath(expanded).TrimEnd(Path.DirectorySeparatorChar);
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<StoredFileResult> SaveAsync(
        string storageKey,
        Stream content,
        long maxSizeBytes,
        CancellationToken cancellationToken)
    {
        var destinationPath = ResolvePath(storageKey);
        var directory = Path.GetDirectoryName(destinationPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Guid.NewGuid():N}.uploading");
        long totalBytes = 0;

        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var output = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81_920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[81_920];
                int bytesRead;
                while ((bytesRead = await content.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    totalBytes += bytesRead;
                    if (totalBytes > maxSizeBytes)
                    {
                        throw new StorageException(
                            StorageErrorCodes.FileTooLarge,
                            "حجم واقعی فایل از سقف مجاز بیشتر است.");
                    }

                    hash.AppendData(buffer, 0, bytesRead);
                    await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                }

                await output.FlushAsync(cancellationToken);
            }

            if (totalBytes == 0)
            {
                throw new StorageException(StorageErrorCodes.FileEmpty, "فایل خالی قابل بارگذاری نیست.");
            }

            File.Move(temporaryPath, destinationPath, false);
            return new StoredFileResult(totalBytes, Convert.ToHexString(hash.GetHashAndReset()));
        }
        catch (StorageException)
        {
            DeleteIfPresent(temporaryPath);
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            DeleteIfPresent(temporaryPath);
            throw new StorageException(
                StorageErrorCodes.FileStorageFailed,
                "ذخیره محتوای فایل ناموفق بود.",
                exception);
        }
        finally
        {
            DeleteIfPresent(temporaryPath);
        }
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolvePath(storageKey);
        if (!File.Exists(path))
        {
            throw new StorageException(
                StorageErrorCodes.FileStorageFailed,
                "محتوای فایل در مخزن پیدا نشد.");
        }

        Stream stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81_920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DeleteIfPresent(ResolvePath(storageKey));
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(File.Exists(ResolvePath(storageKey)));
    }

    private string ResolvePath(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey) || Path.IsPathFullyQualified(storageKey))
        {
            throw InvalidKey();
        }

        var segments = storageKey.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment => segment is "." or ".." ||
                                                           segment.Contains('\\') ||
                                                           segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
        {
            throw InvalidKey();
        }

        var path = Path.GetFullPath(Path.Combine([_rootPath, .. segments]));
        var rootPrefix = _rootPath + Path.DirectorySeparatorChar;
        if (!path.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw InvalidKey();
        }

        return path;
    }

    private static StorageException InvalidKey() =>
        new(StorageErrorCodes.FileStorageFailed, "کلید داخلی فایل معتبر نیست.");

    private static void DeleteIfPresent(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
