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
    private static void Seed(Task<ReleaseVersion> task, bool manual)
    {
        ((UpdateSchedule)Field("Schedule").GetValue(null)!).Checked(DateTime.UtcNow);
        Field("_pending").SetValue(null, task); Field("_manual").SetValue(null, manual);
    }
    private static void Expect(bool result, string message) { if (!result) throw new Exception(message); }
    internal static void Run()
    {
        SettingsService.Settings.AutoCheckUpdates = false;
        UpdateCheckService.Start(); Box.Shown = 0;
        Seed(Task.FromResult(Update), true);
        Acad.DocumentManager.Active.CommandInProgress = "LINE";
        Acad.RaiseIdle(); Expect(Box.Shown == 0 && Pending != null, "Update interrupted an AutoCAD command");
        Acad.DocumentManager.Active.CommandInProgress = ""; Acad.DocumentManager.HasActiveDocument = false;
        Acad.RaiseIdle(); Expect(Box.Shown == 0 && Pending != null, "Update displayed without a document");
        Acad.DocumentManager.HasActiveDocument = true; Acad.RaiseIdle();
        Expect(Box.Shown == 1 && Box.LastText.Contains("v1.3.1"), "Manual update ignored while automatic checks disabled");

        SettingsService.Settings.AutoCheckUpdates = true; SettingsService.Settings.LastNotifiedUpdateTag = null;
        Seed(Task.FromResult(Update), false); Acad.RaiseIdle();
        Expect(Box.Shown == 2 && SettingsService.Settings.LastNotifiedUpdateTag == Update.Tag, "Automatic notification was not persisted");
        Seed(Task.FromResult(Update), false); Acad.RaiseIdle();
        Expect(Box.Shown == 2, "Same automatic notification repeated");
        var waiting = new TaskCompletionSource<ReleaseVersion>(); Seed(waiting.Task, false);
        UpdateCheckService.Check(true); UpdateCheckService.Check(true);
        Expect(ReferenceEquals(Pending, waiting.Task), "Repeated manual checks created duplicate requests");
        waiting.SetResult(Update); Acad.RaiseIdle();
        Expect(Box.Shown == 3, "Manual check did not override automatic notification suppression");

        Seed(Task.FromResult<ReleaseVersion>(null!), true); Acad.RaiseIdle();
        Expect(Box.Shown == 4 && Box.LastText.Contains("не найдено"), "No-new-version response missing");
        foreach (var error in new Exception[] { new HttpRequestException("HTTP 403 rate limit"), new TaskCanceledException("Timeout"), new Newtonsoft.Json.JsonReaderException("Malformed GitHub response") })
        {
            int before = Box.Shown;
            Seed(Task.FromException<ReleaseVersion>(error), false); Acad.RaiseIdle();
            Expect(Box.Shown == before && Pending == null, "Automatic network error opened a dialog or stuck request");
            Seed(Task.FromException<ReleaseVersion>(error), true); Acad.RaiseIdle();
            Expect(Box.Shown == before + 1 && Box.LastText.Contains("Не удалось проверить"), "Manual network error was hidden");
        }
        Seed(Task.FromResult(Update), false); SettingsService.Settings.AutoCheckUpdates = false; int shown = Box.Shown;
        Acad.RaiseIdle(); Expect(Box.Shown == shown, "Disabled automatic check still notified");
        Seed(new TaskCompletionSource<ReleaseVersion>().Task, true);
        UpdateCheckService.Stop();
        Expect(Pending == null && Acad.SubscriberCount == 0, "Stopping update checks left a task/subscription");
        Console.WriteLine("PASS: manual/automatic flow, disabled auto mode, document/command gating, duplicate requests/notifications, current version, HTTP 403/timeout/invalid JSON, shutdown.");
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
