using System.Reflection;
using BotPlusAutocadTool.Services;
using Acad = Autodesk.AutoCAD.ApplicationServices.Application;
using Box = System.Windows.MessageBox;

internal static class UpdateFlowChecks
{
    private static readonly BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
    private static FieldInfo Field(string name) => typeof(UpdateCheckService).GetField(name, PrivateStatic)!;
    private static Task<ReleaseVersion> Pending => (Task<ReleaseVersion>)Field("_pending").GetValue(null);
    private static readonly ReleaseVersion Update = ReleaseVersion.FindUpdate("[{\"tag_name\":\"v1.3.1\",\"draft\":false,\"prerelease\":false,\"html_url\":\"https://github.com/Davud77/BotPlusAutocadTool/releases/tag/v1.3.1\"}]", new Version(1,2,1));
    private static void Seed(Task<ReleaseVersion> task)
    {
        ((UpdateSchedule)Field("Schedule").GetValue(null)!).Checked(DateTime.UtcNow);
        Field("_pending").SetValue(null, task);
    }
    private static void Expect(bool result, string message) { if (!result) throw new Exception(message); }
    internal static void Run()
    {
        BotPlusAutocadTool.Views.AboutWindow.OpenRequests = 0;
        BotPlusAutocadTool.Views.AboutWindow.Presentations = 0;
        SettingsService.Settings.AutoCheckUpdates = false;
        UpdateCheckService.Start(); Box.Shown = 0;
        UpdateCheckService.Check(true);
        Expect(BotPlusAutocadTool.Views.AboutWindow.OpenRequests == 1 && Pending == null, "Manual check did not open inline service updates immediately");
        SettingsService.Settings.AutoCheckUpdates = true; SettingsService.Settings.LastNotifiedUpdateTag = null;
        Seed(Task.FromResult(Update));
        Acad.DocumentManager.Active.CommandInProgress = "LINE";
        Acad.RaiseIdle(); Expect(BotPlusAutocadTool.Views.AboutWindow.Presentations == 0 && Pending != null, "Auto update interrupted a command");
        Acad.DocumentManager.Active.CommandInProgress = ""; Acad.DocumentManager.HasActiveDocument = false;
        Acad.RaiseIdle(); Expect(BotPlusAutocadTool.Views.AboutWindow.Presentations == 0 && Pending != null, "Auto update displayed without a document");
        Acad.DocumentManager.HasActiveDocument = true; Acad.RaiseIdle();
        Expect(BotPlusAutocadTool.Views.AboutWindow.Presentations == 1 && SettingsService.Settings.LastNotifiedUpdateTag == Update.Tag, "Auto update did not use service window");
        Seed(Task.FromResult(Update)); Acad.RaiseIdle();
        Expect(BotPlusAutocadTool.Views.AboutWindow.Presentations == 1, "Same automatic notification repeated");
        var waiting = new TaskCompletionSource<ReleaseVersion>(); Seed(waiting.Task);
        UpdateCheckService.Check(true); UpdateCheckService.Check(true);
        Expect(ReferenceEquals(Pending, waiting.Task) && BotPlusAutocadTool.Views.AboutWindow.OpenRequests == 3, "Manual route altered background request");
        waiting.SetResult(Update); Acad.RaiseIdle();
        Seed(Task.FromResult<ReleaseVersion>(null!)); Acad.RaiseIdle();
        foreach (var error in new Exception[] { new HttpRequestException("HTTP 403 rate limit"), new TaskCanceledException("Timeout"), new Newtonsoft.Json.JsonReaderException("Malformed GitHub response") })
        {
            Seed(Task.FromException<ReleaseVersion>(error)); Acad.RaiseIdle();
            Expect(Pending == null && Box.Shown == 0, "Background error opened an extra popup or stuck request");
        }
        Seed(Task.FromResult(Update)); SettingsService.Settings.AutoCheckUpdates = false;
        Acad.RaiseIdle(); Expect(BotPlusAutocadTool.Views.AboutWindow.Presentations == 1, "Disabled automatic check still notified");
        Seed(new TaskCompletionSource<ReleaseVersion>().Task);
        UpdateCheckService.Stop();
        Expect(Pending == null && Acad.SubscriberCount == 0 && Box.Shown == 0, "Shutdown or no-extra-popup contract failed");
        Console.WriteLine("PASS: manual checks route immediately to service updates; automatic checks gate on document/command, suppress duplicates, contain failures and open no message boxes.");
    }
    internal static async Task Live(string snapshotPath)
    {
        var fetch = typeof(UpdateCheckService).GetMethod("Fetch", PrivateStatic)!;
        string json = File.ReadAllText(snapshotPath);
        var executingVersion = Assembly.GetExecutingAssembly().GetName().Version!;
        foreach (bool preview in new[] { false, true })
        {
            var expected = ReleaseVersion.FindUpdate(json, executingVersion, preview);
            var actual = await (Task<ReleaseVersion>)fetch.Invoke(null, new object[] { preview, CancellationToken.None })!;
            Expect(actual?.Tag == expected?.Tag, "Real GitHub client differs from release snapshot");
            Console.WriteLine($"LIVE_PASS: actual plugin HTTP client, prerelease={preview}, update={actual?.Tag ?? "none"}");
            var latest = await UpdateCheckService.FetchLatestAsync(preview, CancellationToken.None);
            Expect(latest?.Tag == ReleaseVersion.FindLatest(json, preview)?.Tag, "Inline latest client differs from release snapshot");
            Expect(latest?.InstallerUrl != null, "Latest published installer missing");
            Console.WriteLine($"LIVE_PASS: inline latest HTTP client, prerelease={preview}, latest={latest.Tag}, installer={latest.InstallerName}");
        }
        foreach (var installed in new[] { new Version(1,0,0), new Version(1,1,1), new Version(1,2,1), new Version(1,3,1), new Version(9,0,0) })
            foreach (bool preview in new[] { false, true })
            {
                var update = ReleaseVersion.FindUpdate(json, installed, preview);
                Expect(update == null || update.Version > installed, "Real releases proposed downgrade/equal version");
                Console.WriteLine($"LIVE_PASS: installed={installed}, prerelease={preview}, update={update?.Tag ?? "none"}");
            }
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        try
        {
            await (Task<ReleaseVersion>)fetch.Invoke(null, new object[] { false, cancellation.Token })!;
            throw new Exception("Cancelled request continued");
        }
        catch (OperationCanceledException) { Console.WriteLine("LIVE_PASS: HTTP cancellation honored"); }
    }
}
