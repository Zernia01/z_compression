using System.Net;
using System.Text;
using ZCompression.Update;

namespace ZCompression.Tests;

[TestClass]
public sealed class UpdateRequestTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Download_StreamsAndVerifiesHashWithNoPartialFile(bool invalidHash)
    {
        var bytes = Enumerable.Range(0, 2_500_000).Select(index => (byte)(index % 251)).ToArray();
        using var http = new HttpClient(new PackageHandler(bytes));
        var service = new GitHubUpdateService(http, "owner", "repo");
        var root = Path.Combine(Path.GetTempPath(), "z-update-test-" + Guid.NewGuid().ToString("N"));
        var hash = invalidHash ? "invalid" : Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
        var manifest = new UpdateManifest("1.2.2", new Uri("https://github.com/owner/repo/package.zip"), hash, bytes.Length, null);
        try
        {
            if (invalidHash)
            {
                try { await service.DownloadAndVerifyAsync(manifest, root); Assert.Fail("Invalid package was accepted."); }
                catch (System.Security.Cryptography.CryptographicException) { }
                Assert.IsEmpty(Directory.GetFiles(root));
            }
            else
            {
                var path = await service.DownloadAndVerifyAsync(manifest, root);
                CollectionAssert.AreEqual(bytes, await File.ReadAllBytesAsync(path));
                Assert.HasCount(1, Directory.GetFiles(root));
            }
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private sealed class PackageHandler(byte[] bytes) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
    }

    [TestMethod]
    public async Task Check_UsesOneManifestRequestForNewAndCurrentVersions()
    {
        var handler = new Handler();
        using var http = new HttpClient(handler);
        var service = new GitHubUpdateService(http, "owner", "repo");
        var result = await service.CheckAsync(new Version(1, 2, 1));
        Assert.IsTrue(result.IsUpdateAvailable);
        Assert.HasCount(1, handler.Urls);
        Assert.AreEqual("https://github.com/owner/repo/releases/latest/download/update-manifest.json", handler.Urls[0]);
        var current = await service.CheckAsync(new Version(1, 2, 2));
        Assert.IsFalse(current.IsUpdateAvailable);
        Assert.HasCount(2, handler.Urls);
    }

    [TestMethod]
    public async Task Check_MissingManifestFallsBackAndSkipsAssetWhenAlreadyCurrent()
    {
        var handler = new Handler { Missing = true };
        using var http = new HttpClient(handler);
        var result = await new GitHubUpdateService(http, "owner", "repo").CheckAsync(new Version(1, 2, 2));
        Assert.IsFalse(result.IsUpdateAvailable);
        Assert.HasCount(2, handler.Urls);
    }

    private sealed class Handler : HttpMessageHandler
    {
        public List<string> Urls { get; } = [];
        public bool Missing { get; init; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.AbsoluteUri;
            Urls.Add(url);
            if (Missing && url.Contains("/latest/download/", StringComparison.Ordinal)) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            var content = url.Contains("api.github.com", StringComparison.Ordinal)
                ? "{\"tag_name\":\"v1.2.2\",\"assets\":[]}"
                : "{\"version\":\"1.2.2\",\"downloadUrl\":\"https://github.com/owner/repo/package.zip\",\"sha256\":\"hash\",\"size\":123}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content, Encoding.UTF8, "application/json") });
        }
    }
}
