namespace MakanApp.Application.Academic;

public sealed class AcademicException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
