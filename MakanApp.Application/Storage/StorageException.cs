namespace MakanApp.Application.Storage;

public sealed class StorageException(string code, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string Code { get; } = code;
}
