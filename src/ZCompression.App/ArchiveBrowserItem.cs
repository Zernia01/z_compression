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
    public string TypeText => IsParent ? L["ParentFolder"] : IsDirectory ? L["FileFolder"] : GetTypeText(Name);
    private static LocalizationManager L => LocalizationManager.Instance;

    private static string GetTypeText(string name)
    {
        var extension = System.IO.Path.GetExtension(name);
        return string.IsNullOrWhiteSpace(extension) ? L["File"] : string.Format(L["FileFormat"], extension.TrimStart('.').ToUpperInvariant());
    }
}
