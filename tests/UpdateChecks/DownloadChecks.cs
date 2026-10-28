using System.Net;
using System.Security.Cryptography;
using BotPlusAutocadTool.Services;
using Newtonsoft.Json;

internal static class DownloadChecks
{
    private const string Name = "BotPlusAutocadTool-1.4-Setup.exe";
    private const string Base = "https://github.com/Davud77/BotPlusAutocadTool/releases/download/v1.4/";
    private static readonly byte[] Payload = Enumerable.Range(0, 200000).Select(i => (byte)i).ToArray();
    private static string Digest => Convert.ToHexString(SHA256.HashData(Payload)).ToLowerInvariant();
    private static ReleaseVersion Release(string digest, long? size = null, string installerUrl = null, string checksumUrl = null) => ReleaseVersion.FindUpdate(
        JsonConvert.SerializeObject(new[] { new { tag_name = "v1.4", draft = false, prerelease = false,
            html_url = "https://github.com/Davud77/BotPlusAutocadTool/releases/tag/v1.4", assets = new[] {
                new { name = Name, browser_download_url = installerUrl ?? Base + Name, size = size ?? Payload.Length, digest },
                new { name = "SHA256SUMS-1.4.txt", browser_download_url = checksumUrl ?? Base + "SHA256SUMS-1.4.txt", size = 100L, digest = (string)null }
            } } }), new Version(1,3,3));
    private static void Expect(bool result, string message) { if (!result) throw new Exception(message); }
    internal static async Task Run()
    {
        string directory = Path.Combine(Path.GetTempPath(), "BotPlus-DownloadChecks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        using var client = new HttpClient(new Handler());
        var progress = new Collector();
        var release = Release("sha256:" + Digest);
        Expect(release.InstallerName == Name && release.InstallerUrl.AbsoluteUri == Base + Name, "Official installer not selected");
        string path = await UpdateDownloadService.DownloadAsync(client, release, directory, progress, CancellationToken.None);
        Expect(File.ReadAllBytes(path).SequenceEqual(Payload), "Downloaded bytes differ");
        Expect(progress.Values.Count > 1 && progress.Values[0].Received == 0 && progress.Values.Last().Percent == 100, "Download progress missing");
        await UpdateDownloadService.DownloadAsync(client, Release(null), directory, null, CancellationToken.None);
        Expect(File.ReadAllBytes(path).SequenceEqual(Payload), "Published checksum fallback failed");
        foreach (var invalid in new[] { Release("sha256:" + new string('0', 64)), Release("sha256:" + Digest, Payload.Length + 1),
            Release("sha256:" + Digest, installerUrl: "https://evil.invalid/setup.exe"),
            Release(null, checksumUrl: "https://github.com/other/repo/checksums.txt") })
        {
            try { await UpdateDownloadService.DownloadAsync(client, invalid, directory, null, CancellationToken.None); throw new Exception("Unsafe/incomplete download accepted"); }
            catch (InvalidDataException) { }
            Expect(File.ReadAllBytes(path).SequenceEqual(Payload), "Failure replaced the prior verified installer");
            Expect(!Directory.GetFiles(directory, "*.partial").Any(), "Partial download remained after failure");
        }
        using var rejected = new HttpClient(new Handler { Reject = true });
        try { await UpdateDownloadService.DownloadAsync(rejected, release, directory, null, CancellationToken.None); throw new Exception("HTTP failure accepted"); }
        catch (HttpRequestException) { }
        using var cancellation = new CancellationTokenSource();
        var cancelProgress = new Collector { OnReport = value => { if (value.Received > 0) cancellation.Cancel(); } };
        try { await UpdateDownloadService.DownloadAsync(client, release, directory, cancelProgress, cancellation.Token); throw new Exception("Cancellation ignored"); }
        catch (OperationCanceledException) { }
        Expect(!Directory.GetFiles(directory, "*.partial").Any() && File.ReadAllBytes(path).SequenceEqual(Payload), "Cancellation lost verified output or retained a partial file");
        await UpdateDownloadService.DownloadAsync(client, release, directory, null, CancellationToken.None);
        Console.WriteLine("PASS: immediate installer download, progress, SHA-256/digest and checksum fallback, exact release asset, HTTP failure, truncated/corrupt download, cancellation/cleanup/retry, prior installer preserved.");
    }
    private sealed class Collector : IProgress<UpdateDownloadProgress>
    {
        public readonly List<UpdateDownloadProgress> Values = new();
        public Action<UpdateDownloadProgress> OnReport;
        public void Report(UpdateDownloadProgress value) { Values.Add(value); OnReport?.Invoke(value); }
    }
    private sealed class Handler : HttpMessageHandler
    {
        public bool Reject;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (Reject) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));
            HttpContent content = request.RequestUri.AbsolutePath.EndsWith(".txt") ?
                new StringContent(Digest + "  " + Name + "\n") : new ByteArrayContent(Payload);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }
}
