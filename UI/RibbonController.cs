// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Windows;
using BotPlusAutocadTool.Infrastructure;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace BotPlusAutocadTool.UI
{
    internal sealed class RibbonController : IDisposable
    {
        private readonly Dictionary<string, ImageSource> _icons = new(StringComparer.Ordinal);
        private RibbonTab _tab;
        private RibbonControl _ribbon;

        public void EnsureCreated()
        {
            var ribbon = ComponentManager.Ribbon;
            if (ribbon == null) return;
            if (ReferenceEquals(_ribbon, ribbon) && ReferenceEquals(ribbon.FindTab(CommandCatalog.TabId), _tab) && _tab?.Panels.Count == CommandCatalog.Panels.Length)
                return;

            // Build before attaching: a missing resource cannot leave a half-built tab.
            var tab = new RibbonTab { Id = CommandCatalog.TabId, Name = CommandCatalog.TabTitle, Title = CommandCatalog.TabTitle };
            foreach (var definition in CommandCatalog.Panels)
            {
                var source = new RibbonPanelSource { Title = definition.Title };
                foreach (var button in definition.Buttons)
                    source.Items.Add(new RibbonButton
                    {
                        Id = "BOTPLUS_" + button.Command,
                        Text = button.Title,
                        ToolTip = button.Description + "\nКоманда: " + button.Command,
                        ShowText = true,
                        ShowImage = true,
                        Size = RibbonItemSize.Large,
                        Orientation = System.Windows.Controls.Orientation.Vertical,
                        LargeImage = GetIcon(button.Icon),
                        CommandHandler = new RibbonCommand(button.Command)
                    });
                tab.Panels.Add(new RibbonPanel { Source = source });
            }
            var previous = ribbon.FindTab(CommandCatalog.TabId);
            if (previous != null) ribbon.Tabs.Remove(previous);
            ribbon.Tabs.Add(tab);
            _ribbon = ribbon;
            _tab = tab;
        }

        private ImageSource GetIcon(string name)
        {
            if (_icons.TryGetValue(name, out var cached)) return cached;
            ImageSource icon = null;
            try
            {
                var assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri($"pack://application:,,,/{assemblyName};component/Resources/{name}", UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                icon = bitmap;
            }
            catch (Exception ex) { PluginLog.Error("Ribbon icon " + name, ex); }
            _icons[name] = icon;
            return icon;
        }

        public void Dispose()
        {
            if (_tab != null && _ribbon != null) _ribbon.Tabs.Remove(_tab);
            _tab = null;
            _ribbon = null;
            _icons.Clear();
        }
    }

    internal sealed class RibbonCommand : ICommand
    {
        private readonly string _name;
        public RibbonCommand(string name) => _name = name;
        public event EventHandler CanExecuteChanged { add { CommandManager.RequerySuggested += value; } remove { CommandManager.RequerySuggested -= value; } }
        public bool CanExecute(object parameter) => AcadApp.DocumentManager.MdiActiveDocument != null;
        public void Execute(object parameter)
        {
            try { AcadApp.DocumentManager.MdiActiveDocument?.SendStringToExecute(_name + " ", true, false, false); }
            catch (Exception ex) { PluginLog.Error("Ribbon command " + _name, ex); }
        }
    }
}
