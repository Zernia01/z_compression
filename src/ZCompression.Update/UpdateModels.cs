namespace ZCompression.Update;

public sealed record UpdateManifest(string Version, Uri DownloadUrl, string Sha256, long Size, Uri? ReleaseNotesUrl);
public sealed record UpdateCheckResult(bool IsUpdateAvailable, Version CurrentVersion, Version LatestVersion, UpdateManifest? Manifest);

public interface IUpdateService
{
    Task<UpdateCheckResult> CheckAsync(Version currentVersion, CancellationToken cancellationToken = default);
    Task<string> DownloadAndVerifyAsync(UpdateManifest manifest, string destinationDirectory, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
}
