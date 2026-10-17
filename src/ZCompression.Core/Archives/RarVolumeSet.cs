using System.Globalization;
using System.Text.RegularExpressions;

namespace ZCompression.Core.Archives;

public static class RarVolumeSet
{
    private static readonly Regex PartName = new(@"^(?<name>.+)\.part(?<number>[0-9]+)\.rar$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static bool IsPart(string path) => PartName.IsMatch(Path.GetFileName(path));
    public static string GetArchiveName(string path) => PartName.Match(Path.GetFileName(path)) is { Success: true } match ? match.Groups["name"].Value : Path.GetFileNameWithoutExtension(path);

    public static IReadOnlyList<FileInfo> FindParts(string path)
    {
        var file = new FileInfo(Path.GetFullPath(path));
        var match = PartName.Match(file.Name);
        if (!match.Success) return [file];
        var name = match.Groups["name"].Value;
        var parts = Directory.EnumerateFiles(file.DirectoryName!)
            .Select(candidate => (File: new FileInfo(candidate), Match: PartName.Match(Path.GetFileName(candidate))))
            .Where(item => item.Match.Success && item.Match.Groups["name"].Value.Equals(name, StringComparison.OrdinalIgnoreCase))
            .Select(item => (item.File, Number: int.Parse(item.Match.Groups["number"].Value, CultureInfo.InvariantCulture)))
            .OrderBy(item => item.Number).ToArray();
        for (var index = 0; index < parts.Length; index++)
            if (parts[index].Number != index + 1)
                throw new InvalidDataException($"분할 RAR의 {index + 1}번 조각이 없거나 번호가 중복됩니다. 모든 조각을 같은 폴더에 두세요. / Missing or duplicate RAR volume {index + 1}: {name}");
        if (parts.Length == 0) throw new FileNotFoundException("RAR volume was not found.", path);
        return parts.Select(item => item.File).ToArray();
    }

    public static string GetFirstPart(string path) => IsPart(path) ? FindParts(path)[0].FullName : path;
}
