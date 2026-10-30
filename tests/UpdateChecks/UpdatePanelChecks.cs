using BotPlusAutocadTool.Services;
using BotPlusAutocadTool.ViewModels;
using Newtonsoft.Json;

internal static class UpdatePanelChecks
{
    private static void Expect(bool value, string message) { if (!value) throw new Exception(message); }
    private static ReleaseVersion Release(string version, bool preview = false, bool asset = true) => ReleaseVersion.FindLatest(JsonConvert.SerializeObject(new[] {
        new { tag_name="v"+version, draft=false, prerelease=preview,
            html_url="https://github.com/Davud77/BotPlusAutocadTool/releases/tag/v"+version,
            assets=asset ? new[] {new { name="BotPlusAutocadTool-"+version+"-Setup.exe", size=100,
                browser_download_url="https://github.com/Davud77/BotPlusAutocadTool/releases/download/v"+version+"/BotPlusAutocadTool-"+version+"-Setup.exe", digest="sha256:"+new string('a',64)}} : Array.Empty<object>() }
    }), true);

    internal static async Task Run()
    {
        var installed = new Version(1, 7, 1);
        var equal = Release("1.7.1");
        Expect(equal != null, "Latest parser discarded equal/current version");
        var pending = new TaskCompletionSource<ReleaseVersion>(); int calls = 0;
        using (var model = new UpdatePanelViewModel(installed, () => false,
            (_, _) => { calls++; return pending.Task; }, (_, _, _) => Task.FromResult("installer.exe")))
        {
            Expect(model.CurrentVersion == "1.7.1" && !model.CanDownload, "Initial current version/state missing");
            var check = model.CheckAsync();
            Expect(model.IsBusy && model.LatestVersion.Contains("Проверка") && !model.CheckCommand.CanExecute(null), "Checking state is not immediate");
            await model.CheckAsync(); Expect(calls == 1, "Duplicate check escaped busy guard");
            pending.SetResult(equal); await check;
            Expect(model.LatestVersion == "1.7.1" && model.CanDownload && model.Status.Contains("актуальная"), "Equal version cannot be downloaded");
            await model.DownloadAsync();
            Expect(model.HasSavedPath && model.ProgressPercent == 100 && model.Status.Contains("SHA-256"), "Download success/path missing");
        }
        foreach (var failure in new Exception[] { new HttpRequestException("HTTP 403"), new TaskCanceledException("timeout"), new JsonReaderException("invalid JSON") })
        {
            using var model = new UpdatePanelViewModel(installed, () => false, (_, _) => Task.FromException<ReleaseVersion>(failure), (_, _, _) => Task.FromResult("unused"));
            await model.CheckAsync();
            Expect(!model.IsBusy && !model.CanDownload && model.Status.Contains("Не удалось проверить"), "Check error was not inline/retryable");
        }
        using (var model = new UpdatePanelViewModel(installed, () => false, (_, _) => Task.FromResult<ReleaseVersion>(null!), (_, _, _) => Task.FromResult("unused")))
        {
            await model.CheckAsync(); Expect(model.LatestVersion.Contains("Не опубликована") && !model.CanDownload, "Empty release state failed");
            model.PresentRelease(Release("1.7")); Expect(model.CanDownload && model.Status.Contains("новее опубликованной"), "Published older version state failed");
            model.PresentRelease(Release("1.7.2", asset: false)); Expect(!model.CanDownload && model.Status.Contains("не опубликован"), "Missing installer asset was enabled");
        }
        bool include = true;
        using (var model = new UpdatePanelViewModel(installed, () => include, (_, _) => Task.FromResult(Release("2.0.0-beta.1", true)), (_, _, _) => Task.FromResult("unused")))
        {
            await model.CheckAsync(); Expect(model.CanDownload, "Preview was not accepted when enabled");
            include = false; model.ChannelChanged(); Expect(!model.CanDownload && model.LatestVersion == "Не проверена", "Preview channel change retained stale download");
        }
        var blocker = new TaskCompletionSource<string>(); CancellationToken downloadToken = default; IProgress<UpdateDownloadProgress> late = null;
        using (var model = new UpdatePanelViewModel(installed, () => false, (_, _) => Task.FromResult(equal), (_, p, token) => { downloadToken = token; late = p; return blocker.Task; }))
        {
            await model.CheckAsync(); var download = model.DownloadAsync();
            Expect(model.IsBusy && model.CancelCommand.CanExecute(null) && !model.CanDownload, "Download busy state failed");
            model.CancelCommand.Execute(null); Expect(downloadToken.IsCancellationRequested, "Cancellation token not signalled");
            blocker.SetCanceled(downloadToken); await download;
            Expect(!model.IsBusy && model.CanDownload && model.Status.Contains("отменена"), "Cancelled download was not retryable");
        }
        var slow = new TaskCompletionSource<ReleaseVersion>(); CancellationToken checkToken = default;
        var closing = new UpdatePanelViewModel(installed, () => false, (_, token) => { checkToken = token; return slow.Task; }, (_, _, _) => Task.FromResult("unused"));
        var request = closing.CheckAsync(); string beforeClose = closing.LatestVersion; closing.Dispose();
        slow.SetResult(equal); await request;
        Expect(checkToken.IsCancellationRequested && closing.LatestVersion == beforeClose && !closing.CanDownload, "Closed view received a late result");
        using (var model = new UpdatePanelViewModel(installed, () => false, (_, _) => Task.FromResult(equal), (_, _, _) => Task.FromException<string>(new IOException("SHA-256 mismatch"))))
        {
            await model.CheckAsync(); await model.DownloadAsync();
            Expect(model.Status.Contains("Не удалось скачать") && !model.HasSavedPath && model.CanDownload, "Download failure lost inline retry state");
        }
        Console.WriteLine("PASS: inline update states; latest/equal/older versions, immediate progress, busy guards, missing asset, preview policy, HTTP/JSON/timeout errors, download/path, cancel/retry and disposal without popups.");
    }
}
