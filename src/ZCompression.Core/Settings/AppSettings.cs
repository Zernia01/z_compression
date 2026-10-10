using ZCompression.Core.Archives;

namespace ZCompression.Core.Settings;

public sealed record AppSettings
{
    public string Language { get; init; } = "auto";
    public string Theme { get; init; } = "system";
    public string DefaultArchiveFormat { get; init; } = "zip";
    public string DefaultCompressionLevel { get; init; } = "high";
    public CompressionPreset GetDefaultCompressionLevel() => Enum.TryParse<CompressionPreset>(DefaultCompressionLevel, true, out var value) && Enum.IsDefined(value) ? value : CompressionPreset.High;
    public ArchiveFormat GetDefaultArchiveFormat() => DefaultArchiveFormat?.ToLowerInvariant() switch
    {
        "7z" or "sevenzip" => ArchiveFormat.SevenZip, "rar" => ArchiveFormat.Rar,
        "tar" => ArchiveFormat.Tar, "tar.gz" or "targzip" => ArchiveFormat.TarGZip, _ => ArchiveFormat.Zip,
    };
    public bool CheckForUpdatesAtStartup { get; init; } = true;
    public bool FirstRunCompleted { get; init; }
    public int CpuThreads { get; init; }
    public string OperationPriority { get; init; } = "normal";
    public string CompressShortcut { get; init; } = "Ctrl+N";
    public string ExtractShortcut { get; init; } = "Ctrl+E";
}

public interface ISettingsService
{
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
