using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace ZCompression.Update;

public sealed class GitHubUpdateService(HttpClient httpClient, string owner, string repository) : IUpdateService
{
    public const string DefaultOwner = "zernia01";
    public const string DefaultRepository = "z_compression";

    public async Task<UpdateCheckResult> CheckAsync(Version currentVersion, CancellationToken cancellationToken = default)
    {
        // Our releases always publish this manifest. Avoid a separate GitHub API
        // request before downloading the same metadata through the asset redirect.
        using var directRequest = new HttpRequestMessage(HttpMethod.Get, $"https://github.com/{owner}/{repository}/releases/latest/download/update-manifest.json");
        directRequest.Headers.UserAgent.Add(new ProductInfoHeaderValue("z_compression", currentVersion.ToString()));
        using var directResponse = await httpClient.SendAsync(directRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (directResponse.IsSuccessStatusCode)
        {
            var directManifest = await directResponse.Content.ReadFromJsonAsync<UpdateManifest>(cancellationToken: cancellationToken)
                ?? throw new InvalidDataException("Update manifest is invalid.");
            var directVersion = Version.Parse(directManifest.Version.TrimStart('v'));
            return new UpdateCheckResult(directVersion > currentVersion, currentVersion, directVersion, directManifest);
        }
        if (directResponse.StatusCode != System.Net.HttpStatusCode.NotFound) directResponse.EnsureSuccessStatusCode();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{owner}/{repository}/releases/latest");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("z_compression", currentVersion.ToString()));
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = json.RootElement;
        var tag = root.GetProperty("tag_name").GetString()?.TrimStart('v') ?? throw new InvalidDataException("Release has no version tag.");
        var latest = Version.Parse(tag);
        if (latest <= currentVersion) return new UpdateCheckResult(false, currentVersion, latest, null);
        var manifestAsset = root.GetProperty("assets").EnumerateArray().FirstOrDefault(a =>
            string.Equals(a.GetProperty("name").GetString(), "update-manifest.json", StringComparison.OrdinalIgnoreCase));
        if (manifestAsset.ValueKind == JsonValueKind.Undefined)
            return new UpdateCheckResult(latest > currentVersion, currentVersion, latest, null);

        var url = manifestAsset.GetProperty("browser_download_url").GetString()!;
        using var manifestRequest = new HttpRequestMessage(HttpMethod.Get, url);
        manifestRequest.Headers.UserAgent.Add(new ProductInfoHeaderValue("z_compression", currentVersion.ToString()));
        using var manifestResponse = await httpClient.SendAsync(manifestRequest, cancellationToken);
        manifestResponse.EnsureSuccessStatusCode();
        var manifest = await manifestResponse.Content.ReadFromJsonAsync<UpdateManifest>(cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("Update manifest is invalid.");
        return new UpdateCheckResult(latest > currentVersion, currentVersion, latest, manifest);
    }

    public async Task<string> DownloadAndVerifyAsync(UpdateManifest manifest, string destinationDirectory, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        progress?.Report(0);
        Directory.CreateDirectory(destinationDirectory);
        var target = Path.Combine(destinationDirectory, Path.GetFileName(manifest.DownloadUrl.LocalPath));
        var temporary = target + ".download";
        try
        {
            using var response = await httpClient.GetAsync(manifest.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var destination = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[1024 * 1024];
                long written = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    hash.AppendData(buffer, 0, read);
                    written += read;
                    if (manifest.Size > 0) progress?.Report(written * 100d / manifest.Size);
                }
            }
            if (manifest.Size > 0 && new FileInfo(temporary).Length != manifest.Size) throw new InvalidDataException("Downloaded update size does not match the manifest.");
            var actual = Convert.ToHexString(hash.GetHashAndReset());
            if (!actual.Equals(manifest.Sha256, StringComparison.OrdinalIgnoreCase)) throw new CryptographicException("Downloaded update SHA-256 does not match the manifest.");
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, target, true);
            return target;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
    }
}
