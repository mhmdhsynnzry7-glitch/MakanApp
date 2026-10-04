namespace MakanApp.Application.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string RootPath { get; init; } = string.Empty;
    public long MaxFileSizeBytes { get; init; } = 10 * 1024 * 1024;
    public IReadOnlySet<string> AllowedContentTypes { get; init; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "application/pdf",
            "image/jpeg",
            "image/png",
            "text/plain"
        };
    public TimeSpan UnattachedLifetime { get; init; } = TimeSpan.FromHours(24);
}
