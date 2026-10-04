namespace MakanApp.Application.Assessment;

public sealed class AssessmentException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
