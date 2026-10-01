namespace ZCompression.Core.Settings;

public sealed record AppSettings
{
    public string Language { get; init; } = "auto";
    public string Theme { get; init; } = "system";
    public string DefaultArchiveFormat { get; init; } = "zip";
    public string DefaultCompressionLevel { get; init; } = "high";
    public bool CheckForUpdatesAtStartup { get; init; } = true;
    public bool FirstRunCompleted { get; init; }
    public int CpuThreads { get; init; }
    public string OperationPriority { get; init; } = "normal";
}

public interface ISettingsService
{
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
