// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace BotPlusAutocadTool.Services
{
    public static class UpdateCheckService
    {
        private static readonly HttpClient Client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        private static CancellationTokenSource _cancel;
        private static Task<ReleaseVersion> _pending;
        private static readonly UpdateSchedule Schedule = new UpdateSchedule();

        public static void Start()
        {
            Stop();
            try
            {
                var settings = SettingsService.Settings;
                Schedule.NewSession();
                // Every new AutoCAD session checks, even if another session checked today.
                if (settings.AutoCheckUpdates) Check(false);
                AcadApp.Idle += OnIdle;
            }
            catch
            {
                Stop();
                throw;
            }
        }
        public static void Check(bool manual)
        {
            if (manual) { Views.AboutWindow.OpenUpdates(); return; }
            if (_pending != null) return;
            Schedule.Checked(DateTime.UtcNow);
            SettingsService.Settings.LastUpdateCheckUtc = DateTime.UtcNow;
            SettingsService.Save();
            _cancel = new CancellationTokenSource();
            bool prerelease = SettingsService.Settings.IncludePrereleaseUpdates;
            _pending = Fetch(prerelease, _cancel.Token);
        }
        private static async Task<ReleaseVersion> Fetch(bool prerelease, CancellationToken token)
            => await FetchRelease(prerelease, token, false).ConfigureAwait(false);

        public static async Task<ReleaseVersion> FetchLatestAsync(bool prerelease, CancellationToken token)
            => await FetchRelease(prerelease, token, true).ConfigureAwait(false);

        private static async Task<ReleaseVersion> FetchRelease(bool prerelease, CancellationToken token, bool latest)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/Davud77/BotPlusAutocadTool/releases?per_page=30"))
            {
                request.Headers.UserAgent.ParseAdd("BotPlusAutocadTool/" + Assembly.GetExecutingAssembly().GetName().Version);
                request.Headers.Accept.ParseAdd("application/vnd.github+json");
                using (var response = await Client.SendAsync(request, token).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    var json = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                    return latest ? ReleaseVersion.FindLatest(json, prerelease) : ReleaseVersion.FindUpdate(json, Assembly.GetExecutingAssembly().GetName().Version, prerelease);
                }
            }
        }
        private static void OnIdle(object sender, EventArgs e)
        {
            try { ProcessIdle(); }
            catch (System.Exception ex)
            {
                // Unexpected host/startup failures must not repeat on every Idle.
                Stop();
                Infrastructure.PluginLog.Error("Update callback stopped", ex);
            }
        }
        private static void ProcessIdle()
        {
            if (_pending == null && SettingsService.Settings.AutoCheckUpdates && Schedule.IsDue(DateTime.UtcNow))
                Check(false);
            if (_pending == null || !_pending.IsCompleted || AcadApp.DocumentManager.MdiActiveDocument == null) return;
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (!string.IsNullOrEmpty(doc.CommandInProgress)) return;
            var task = _pending;
            _pending = null;
            _cancel?.Dispose(); _cancel = null;
            try
            {
                var update = task.GetAwaiter().GetResult();
                if (!SettingsService.Settings.AutoCheckUpdates) return;
                SettingsService.Settings.LastUpdateCheckUtc = DateTime.UtcNow;
                SettingsService.Save();
                if (update != null)
                {
                    if (SettingsService.Settings.LastNotifiedUpdateTag == update.Tag) return;
                    SettingsService.Settings.LastNotifiedUpdateTag = update.Tag;
                    SettingsService.Save();
                    Views.AboutWindow.ShowUpdate(update);
                }
            }
            catch (System.Exception ex)
            {
                Infrastructure.PluginLog.Error("Automatic update check", ex);
            }
        }
        public static void Stop()
        {
            AcadApp.Idle -= OnIdle;
            _cancel?.Cancel(); _cancel?.Dispose(); _cancel = null;
            _pending?.ContinueWith(task => { var ignored = task.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            _pending = null;
        }
    }
}
