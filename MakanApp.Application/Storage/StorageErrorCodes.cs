namespace MakanApp.Application.Storage;

public static class StorageErrorCodes
{
    public const string FileNotFound = "FILE_NOT_FOUND";
    public const string FileNotReady = "FILE_NOT_READY";
    public const string FileNotAllowed = "FILE_NOT_ALLOWED";
    public const string FileEmpty = "FILE_EMPTY";
    public const string FileTooLarge = "FILE_TOO_LARGE";
    public const string FileTypeNotAllowed = "FILE_TYPE_NOT_ALLOWED";
    public const string FileUploadFailed = "FILE_UPLOAD_FAILED";
    public const string FileStorageFailed = "FILE_STORAGE_FAILED";
    public const string FileAlreadyDeleted = "FILE_ALREADY_DELETED";
    public const string FileInUse = "FILE_IN_USE";
    public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";
}
