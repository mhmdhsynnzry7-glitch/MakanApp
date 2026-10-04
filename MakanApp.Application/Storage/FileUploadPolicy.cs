namespace MakanApp.Application.Storage;

public sealed class FileUploadPolicy(StorageOptions options)
{
    private readonly StorageOptions _options = options;

    public ValidatedFileUpload Validate(UploadFileCommand command)
    {
        if (command.Content is null || command.DeclaredSizeBytes <= 0)
        {
            throw Error(StorageErrorCodes.FileEmpty, "فایل خالی قابل بارگذاری نیست.");
        }

        if (command.DeclaredSizeBytes > _options.MaxFileSizeBytes)
        {
            throw Error(StorageErrorCodes.FileTooLarge, "حجم فایل از سقف مجاز بیشتر است.");
        }

        var contentType = NormalizeContentType(command.ContentType);
        if (!_options.AllowedContentTypes.Contains(contentType))
        {
            throw Error(StorageErrorCodes.FileTypeNotAllowed, "نوع محتوای فایل مجاز نیست.");
        }

        return new ValidatedFileUpload(SanitizeFileName(command.OriginalFileName), contentType);
    }

    public static string CreateStorageKey(Guid fileAssetId, DateTime createdAtUtc) =>
        $"{createdAtUtc:yyyy/MM}/{fileAssetId:N}-{Guid.NewGuid():N}";

    public static string SanitizeFileName(string? value)
    {
        var candidate = (value ?? string.Empty)
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault() ?? "file";
        var sanitized = new string(candidate
            .Where(character => !char.IsControl(character) &&
                                character is not '<' and not '>' and not ':' and not '"' and
                                not '|' and not '?' and not '*')
            .ToArray())
            .Trim()
            .Trim('.');

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "file";
        }

        return sanitized.Length <= 255 ? sanitized : sanitized[..255];
    }

    private static string NormalizeContentType(string? value) =>
        (value ?? string.Empty).Split(';', 2)[0].Trim().ToLowerInvariant();

    private static StorageException Error(string code, string message) => new(code, message);
}

public sealed record ValidatedFileUpload(string OriginalFileName, string ContentType);
