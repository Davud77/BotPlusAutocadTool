// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using BotPlusAutocadTool.Services;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace BotPlusAutocadTool.UI
{
    internal static class AdditionalFunctionsMenu
    {
        private static ContextMenu _openMenu;
        internal static void Show()
        {
            System.Collections.Generic.IReadOnlyList<LispEntry> entries;
            try { entries = LispCatalog.Read(); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Дополнительные функции"); return; }
            var menu = new ContextMenu { Placement = PlacementMode.MousePoint, MaxHeight = 600 };
            if (entries.Count == 0)
                menu.Items.Add(new MenuItem { Header = "Встроенная библиотека пуста", IsEnabled = false });
            foreach (var group in entries.GroupBy(e => e.Group).OrderBy(g => g.Key, StringComparer.CurrentCulture))
            {
                var parent = new MenuItem { Header = new TextBlock { Text = group.Key } };
                foreach (var entry in group.OrderBy(e => e.Title, StringComparer.CurrentCulture))
                {
                    var item = new MenuItem { Header = new TextBlock { Text = entry.Title }, ToolTip = entry.UnavailableReason ?? entry.Description, IsEnabled = entry.UnavailableReason == null };
                    item.Click += (_, _) =>
                    {
                        try { AcadApp.DocumentManager.MdiActiveDocument?.SendStringToExecute(LispCatalog.BuildExpression(entry), true, false, false); }
                        catch (Exception ex) { MessageBox.Show(ex.Message, "Дополнительные функции"); }
                    };
                    parent.Items.Add(item);
                }
                menu.Items.Add(parent);
            }
            menu.Closed += (_, _) => { if (ReferenceEquals(_openMenu, menu)) _openMenu = null; };
            _openMenu?.SetCurrentValue(ContextMenu.IsOpenProperty, false);
            _openMenu = menu;
            menu.IsOpen = true;
        }
    }
}
