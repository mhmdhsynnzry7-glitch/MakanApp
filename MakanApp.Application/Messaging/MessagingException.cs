namespace MakanApp.Application.Messaging;

public sealed class MessagingException(string code, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string Code { get; } = code;
}
