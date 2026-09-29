namespace MakanApp.Application.Guardian;

public sealed class GuardianException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
