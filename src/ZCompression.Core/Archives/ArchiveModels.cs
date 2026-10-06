namespace ZCompression.Core.Archives;

public enum ArchiveFormat
{
    Zip,
    SevenZip,
    Tar,
    TarGZip,
    GZip,
    BZip2,
    Xz,
    Zstandard,
    Rar,
}

public enum CompressionPreset { Store, Fastest, Fast, Normal, High, Ultra }

public sealed record ArchiveEntryInfo(
    string Name,
    string Path,
    long OriginalSize,
    long CompressedSize,
    DateTime? Modified,
    string? Checksum,
    bool IsDirectory)
{
    public double CompressionRatio => OriginalSize == 0 ? 0 : 1d - (double)CompressedSize / OriginalSize;
}

public sealed record ArchiveProgress(
    double Percent,
    string CurrentFile,
    int CompletedFiles,
    int TotalFiles,
    long ProcessedBytes,
    long TotalBytes,
    double BytesPerSecond,
    TimeSpan Elapsed);

public sealed record CompressionRequest(
    IReadOnlyList<string> Sources,
    string Destination,
    ArchiveFormat Format,
    CompressionPreset Level = CompressionPreset.Normal,
    string? Password = null);

public sealed record ArchiveUpdateRequest(
    string ArchivePath,
    IReadOnlyList<string> Sources,
    ArchiveFormat Format,
    string DestinationFolder = "",
    CompressionPreset Level = CompressionPreset.High,
    string? Password = null);

public sealed record ExtractionRequest(
    string ArchivePath,
    string DestinationDirectory,
    string? Password = null,
    System.Text.Encoding? LegacyEncoding = null,
    long MaximumExpandedBytes = 100L * 1024 * 1024 * 1024,
    int MaximumFileCount = 1_000_000,
    IReadOnlyList<string>? SelectedPaths = null,
    string RelativeRoot = "");

public sealed record ArchiveCapabilities(bool CanRead, bool CanWrite, bool CanEncrypt, bool CanModify, string? Limitation = null);
