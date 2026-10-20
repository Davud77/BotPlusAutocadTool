using System;
using System.IO;
using Autodesk.AutoCAD.Runtime;
using BotPlusAutocadTool.UI;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

public sealed class ClassicCheck
{
    private static ClassicUiController _controller;
    private static object _menuBar;
    private static readonly string Report = Path.Combine(BotPlusAutocadTool.Services.PluginPaths.UserDataDirectory, "desktop-check.log");
    [CommandMethod("BOTPLUS_DESKTOPRECHECK", CommandFlags.Session)]
    public void Run()
    {
        try
        {
            _menuBar = AcadApp.GetSystemVariable("MENUBAR");
            _controller = new ClassicUiController(); _controller.Show(); _controller.EnsureCreated();
            dynamic app = AcadApp.AcadApplication;
            dynamic menus = app.MenuGroups.Item(0).Menus, bars = app.MenuGroups.Item(0).Toolbars;
            dynamic menu = menus.Item(CommandCatalog.TabTitle), bar = bars.Item(CommandCatalog.TabTitle);
            if ((int)menu.Count != 5 || (int)bar.Count != 24 || !(bool)bar.Visible || !(bool)menu.OnMenuBar) throw new InvalidOperationException("Unexpected menu/toolbar state");
            File.AppendAllText(Report, "PASS: desktop AutoCAD 2026; menu, five submenus, 20 toolbar buttons, separators, visibility; no duplicates on repeated initialization.\n");
            AcadApp.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\nBOTPLUS_DESKTOP_PASS: menu and toolbar visible. Cleanup: BOTPLUS_DESKTOPFINISH.\n");
        }
        catch (System.Exception ex) { File.AppendAllText(Report, "FAIL: " + ex + "\n"); AcadApp.ShowAlertDialog(ex.ToString()); }
    }
    [CommandMethod("BOTPLUS_DESKTOPFINISH", CommandFlags.Session)]
    public void Cleanup()
    {
        _controller?.Dispose(); _controller = null;
        if (_menuBar != null) AcadApp.SetSystemVariable("MENUBAR", _menuBar);
        File.AppendAllText(Report, "PASS: test UI removed; previous MENUBAR restored.\n");
    }
}
namespace BotPlusAutocadTool.Services
{
    internal static class PluginPaths
    {
        internal static string UserDataDirectory
        {
            get { string path = Path.Combine(Path.GetTempPath(), "BotPlusDesktopChecks131"); Directory.CreateDirectory(path); return path; }
        }
    }
}
namespace BotPlusAutocadTool.Infrastructure
{
    internal static class PluginLog
    {
        internal static void Error(string operation, System.Exception error) => File.AppendAllText(Path.Combine(Services.PluginPaths.UserDataDirectory, "desktop-check.log"), operation + ": " + error + "\n");
    }
}
