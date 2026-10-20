// SPDX-License-Identifier: AGPL-3.0-only
using System;
using Autodesk.AutoCAD.Windows;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace BotPlusAutocadTool.UI
{
    internal sealed class ContextMenuController : IDisposable
    {
        private ContextMenuExtension _menu;
        private MenuItem _item;

        public void EnsureCreated()
        {
            if (_menu != null) return;
            var menu = new ContextMenuExtension { Title = "BotPlus AT" };
            var item = new MenuItem("Выбрать по образцу");
            item.Click += OnSelect;
            menu.MenuItems.Add(item);
            try { AcadApp.AddDefaultContextMenuExtension(menu); }
            catch { item.Click -= OnSelect; menu.Dispose(); throw; }
            _menu = menu;
            _item = item;
        }

        private static void OnSelect(object sender, EventArgs args)
        {
            try { AcadApp.DocumentManager.MdiActiveDocument?.SendStringToExecute("WSELECT ", true, false, false); }
            catch (Exception ex) { Infrastructure.PluginLog.Error("Context menu command", ex); }
        }

        public void Dispose()
        {
            if (_menu == null) return;
            _item.Click -= OnSelect;
            AcadApp.RemoveDefaultContextMenuExtension(_menu);
            _menu.Dispose();
            _menu = null;
            _item = null;
        }
    }
}
