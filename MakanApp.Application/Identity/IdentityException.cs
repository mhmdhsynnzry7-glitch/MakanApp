namespace MakanApp.Application.Identity;

public sealed class IdentityException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
