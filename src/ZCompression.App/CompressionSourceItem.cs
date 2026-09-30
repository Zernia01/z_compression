namespace ZCompression.App;

public sealed record CompressionSourceItem(string FullPath, string Name, bool IsDirectory, long Size)
{
    public string Icon => IsDirectory ? "📁" : "📄";
    public string TypeText => IsDirectory ? "폴더" : "파일";
    public string SizeText => IsDirectory ? "—" : FormatSize(Size);

    private static string FormatSize(double value)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var index = 0;
        while (value >= 1024 && index < units.Length - 1) { value /= 1024; index++; }
        return $"{value:0.##} {units[index]}";
    }
}
