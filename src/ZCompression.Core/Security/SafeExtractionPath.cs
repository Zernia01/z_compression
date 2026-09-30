namespace ZCompression.Core.Security;

public static class SafeExtractionPath
{
    public static string Resolve(string destinationDirectory, string entryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(entryName);

        if (Path.IsPathRooted(entryName))
        {
            throw new InvalidDataException("Archive entry contains an absolute path.");
        }

        var root = Path.GetFullPath(destinationDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedName = entryName.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var candidate = Path.GetFullPath(Path.Combine(root, normalizedName));

        if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Archive entry attempts to leave the destination directory.");
        }

        var relative = Path.GetRelativePath(root, candidate);
        if (relative.Split(Path.DirectorySeparatorChar).Any(part => part is ".."))
        {
            throw new InvalidDataException("Archive entry contains path traversal.");
        }

        return candidate;
    }

    public static void EnsureNoReparsePointAncestors(string destinationRoot, string candidate)
    {
        var root = Path.GetFullPath(destinationRoot);
        var current = Directory.Exists(candidate) ? candidate : Path.GetDirectoryName(candidate);
        while (current is not null && current.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            if (Directory.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("Extraction through a symbolic link or junction is not allowed.");
            }

            if (string.Equals(current.TrimEnd(Path.DirectorySeparatorChar), root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
            current = Path.GetDirectoryName(current);
        }
    }
}
