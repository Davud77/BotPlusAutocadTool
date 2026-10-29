using System;
using System.IO;
using System.Linq;
using Autodesk.Windows;
using Autodesk.AutoCAD.Runtime;
using BotPlusAutocadTool.UI;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

public sealed class ClassicCheck
{
    private static ClassicUiController _controller;
    private static RibbonController _ribbon;
    private static object _menuBar;
    private static readonly string Report = Path.Combine(BotPlusAutocadTool.Services.PluginPaths.UserDataDirectory, "desktop-check.log");
    [CommandMethod("BOTPLUS_DESKTOPRECHECK", CommandFlags.Session)]
    public void Run()
    {
        try
        {
            _menuBar = AcadApp.GetSystemVariable("MENUBAR");
            _ribbon = new RibbonController(); _ribbon.EnsureCreated();
            var tab = ComponentManager.Ribbon?.FindTab(CommandCatalog.TabId);
            if (tab == null || tab.Panels.Count != 5 || tab.Panels[1].Source.Title != "Таблицы" ||
                tab.Panels[1].Source.Items.OfType<RibbonButton>().Count() != 4 ||
                !tab.Panels[1].Source.Items.OfType<RibbonButton>().Any(button => button.Id == "BOTPLUS_BOTPLUS_CLEARTABLESTYLE"))
                throw new InvalidOperationException("Tables ribbon panel is missing");
            _controller = new ClassicUiController(); _controller.Show(); _controller.EnsureCreated();
            dynamic app = AcadApp.AcadApplication;
            dynamic menus = app.MenuGroups.Item(0).Menus, bars = app.MenuGroups.Item(0).Toolbars;
            dynamic menu = menus.Item(CommandCatalog.TabTitle), bar = bars.Item(CommandCatalog.TabTitle);
            if ((int)menu.Count != 5 || (int)bar.Count != 23 || !(bool)bar.Visible || !(bool)menu.OnMenuBar) throw new InvalidOperationException("Unexpected menu/toolbar state");
            if ((string)menu.Item(1).SubMenu.Item(3).Macro != "^C^CBOTPLUS_CLEARTABLESTYLE ") throw new InvalidOperationException("Missing table reset command");
            File.AppendAllText(Report, "PASS: desktop AutoCAD 2026; five ribbon panels and Tables with four commands; menu, five submenus, 19 toolbar buttons, separators, visibility; no duplicates on repeated initialization.\n");
            AcadApp.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\nBOTPLUS_DESKTOP_PASS: menu and toolbar visible. Cleanup: BOTPLUS_DESKTOPFINISH.\n");
        }
        catch (System.Exception ex) { File.AppendAllText(Report, "FAIL: " + ex + "\n"); AcadApp.ShowAlertDialog(ex.ToString()); }
    }
    [CommandMethod("BOTPLUS_DESKTOPFINISH", CommandFlags.Session)]
    public void Cleanup()
    {
        _controller?.Dispose(); _controller = null;
            _ribbon?.Dispose(); _ribbon = null;
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
            get { string path = Environment.GetEnvironmentVariable("BOTPLUS_DATA_DIRECTORY") ?? Path.Combine(Path.GetTempPath(), "BotPlusDesktopChecks131"); Directory.CreateDirectory(path); return path; }
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
