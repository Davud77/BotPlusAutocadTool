// SPDX-License-Identifier: AGPL-3.0-only
using System;
using Autodesk.AutoCAD.Runtime;
using BotPlusAutocadTool.Infrastructure;
using BotPlusAutocadTool.Services;
using BotPlusAutocadTool.UI;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: ExtensionApplication(typeof(BotPlusAutocadTool.App))]

namespace BotPlusAutocadTool
{
    public sealed class App : IExtensionApplication
    {
        private readonly RibbonController _ribbon = new();
        private readonly ContextMenuController _contextMenu = new();
        private readonly ClassicUiController _classic = new();
        private static App _instance;
        internal static void ShowClassicUi() => _instance?._classic.Show();
        private DateTime _nextUiCheckUtc;
        private bool _initialized;

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            _instance = this;
            RunSafely("Start selection indicator", SelectionLengthService.Start);
            RunSafely("Start update checks", UpdateCheckService.Start);
            AcadApp.Idle += OnIdle;
        }

        public void Terminate()
        {
            if (!_initialized) return;
            _initialized = false;
            AcadApp.Idle -= OnIdle;
            RunSafely("Stop updates", UpdateCheckService.Stop);
            RunSafely("Stop selection indicator", SelectionLengthService.Stop);
            RunSafely("Stop publication", DsdPublishService.Shutdown);
            RunSafely("Stop Excel sessions", TableExcelService.Shutdown);
            RunSafely("Stop copy restrictions", CopyProtectionService.Shutdown);
            RunSafely("Close length window", Commands.SumLengthCommand.CloseWindow);
            RunSafely("Remove context menu", _contextMenu.Dispose);
            RunSafely("Remove ribbon", _ribbon.Dispose);
            RunSafely("Remove classic UI", _classic.Dispose);
            _instance = null;
        }

        private void OnIdle(object sender, EventArgs args)
        {
            var now = DateTime.UtcNow;
            if (now < _nextUiCheckUtc) return;
            _nextUiCheckUtc = now.AddSeconds(1);
            try
            {
                _ribbon.EnsureCreated();
                _contextMenu.EnsureCreated();
                _classic.EnsureCreated();
            }
            catch (System.Exception ex)
            {
                _nextUiCheckUtc = now.AddSeconds(30);
                PluginLog.Error("Create UI", ex);
            }
        }

        private static void RunSafely(string operation, Action action)
        {
            try { action(); }
            catch (System.Exception ex) { PluginLog.Error(operation, ex); }
        }
    }
}
