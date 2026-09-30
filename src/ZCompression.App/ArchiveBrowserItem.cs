using ZCompression.Core.Archives;

namespace ZCompression.App;

public sealed record ArchiveBrowserItem(
    string Name,
    string Path,
    bool IsDirectory,
    bool IsParent,
    long OriginalSize,
    long CompressedSize,
    DateTime? Modified,
    string? Checksum,
    ArchiveEntryInfo? Entry)
{
    public string Icon => IsParent ? "📁" : IsDirectory ? "📂" : "📄";
    public string TypeText => IsParent ? "상위 폴더" : IsDirectory ? "파일 폴더" : GetTypeText(Name);

    private static string GetTypeText(string name)
    {
        var extension = System.IO.Path.GetExtension(name);
        return string.IsNullOrWhiteSpace(extension) ? "파일" : $"{extension.TrimStart('.').ToUpperInvariant()} 파일";
    }
}
