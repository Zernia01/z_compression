namespace ZCompression.Core.Archives;

public interface IArchiveEngine
{
    IReadOnlyDictionary<ArchiveFormat, ArchiveCapabilities> Capabilities { get; }
    Task CompressAsync(CompressionRequest request, IProgress<ArchiveProgress>? progress = null, CancellationToken cancellationToken = default);
    Task UpdateAsync(ArchiveUpdateRequest request, IProgress<ArchiveProgress>? progress = null, CancellationToken cancellationToken = default);
    Task ExtractAsync(ExtractionRequest request, IProgress<ArchiveProgress>? progress = null, CancellationToken cancellationToken = default);
    Task ExtractEntryAsync(string archivePath, string entryPath, string destinationPath, string? password = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ArchiveEntryInfo>> ListAsync(string archivePath, string? password = null, System.Text.Encoding? legacyEncoding = null, CancellationToken cancellationToken = default);
    Task TestAsync(string archivePath, string? password = null, CancellationToken cancellationToken = default);
}
