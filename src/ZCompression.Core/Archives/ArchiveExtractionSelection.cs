using ZCompression.Core.Security;

namespace ZCompression.Core.Archives;

internal sealed class ArchiveExtractionSelection
{
    private readonly string[]? _selected;
    private readonly string _prefix;
    private readonly string _destination;

    internal ArchiveExtractionSelection(ExtractionRequest request)
    {
        _destination = request.DestinationDirectory;
        var root = Validate(request.RelativeRoot, allowEmpty: true);
        _prefix = root.Length == 0 ? "" : root + "/";
        _selected = request.SelectedPaths?.Select(path => Validate(path, allowEmpty: false)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (_selected is not null && (_selected.Length == 0 || _selected.Any(path => !path.StartsWith(_prefix, StringComparison.OrdinalIgnoreCase))))
            throw new InvalidDataException("Selected entries must be inside the current archive folder.");
    }

    internal string? GetRelativePath(string path)
    {
        var normalized = path.Replace('\\', '/').TrimEnd('/');
        if (_selected is not null && !_selected.Any(selected => normalized.Equals(selected, StringComparison.OrdinalIgnoreCase) || normalized.StartsWith(selected + "/", StringComparison.OrdinalIgnoreCase)))
            return null;
        // Validate the original path before stripping any parent prefix.
        normalized = Validate(path, allowEmpty: false);
        if (!normalized.StartsWith(_prefix, StringComparison.OrdinalIgnoreCase)) return null;
        var relative = normalized[_prefix.Length..];
        return relative.Length == 0 ? null : relative;
    }

    private string Validate(string path, bool allowEmpty)
    {
        if (allowEmpty && path.Length == 0) return "";
        SafeExtractionPath.Resolve(_destination, path);
        var normalized = path.Replace('\\', '/').TrimEnd('/');
        if (normalized.Split('/').Any(part => part is "" or "." or ".."))
            throw new InvalidDataException("Archive selection contains an invalid path.");
        return normalized;
    }
}
