namespace MakanApp.Application.Organization;

public sealed class OrganizationException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
