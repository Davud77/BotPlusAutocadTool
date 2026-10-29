// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using BotPlusAutocadTool.Services;
using BotPlusAutocadTool.Views;
using Newtonsoft.Json;

internal static class ServiceWindowChecks
{
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static IEnumerable<DependencyObject> Children(DependencyObject root)
    {
        yield return root;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in Children(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
    internal static void Run()
    {
        var saved = JsonConvert.DeserializeObject<PluginSettings>(JsonConvert.SerializeObject(SettingsService.Settings));
        try
        {
            var window = new AboutWindow();
            var root = (FrameworkElement)window.Content;
            root.Measure(new Size(640, 760)); root.Arrange(new Rect(0, 0, 640, 760)); root.UpdateLayout();
            var nodes = Children(root).ToArray();
            var updates = (GroupBox)window.FindName("UpdatesSection");
            var settings = (StackPanel)window.FindName("SettingsSection");
            Check(Grid.GetRow(settings) > Grid.GetRow(updates), "Settings are not below Updates");
            Check(settings.Children.OfType<GroupBox>().Select(group => group.Header.ToString()).SequenceEqual(new[] { "Форматка", "Пакетная печать", "Сумма длин" }), "Missing service settings groups");
            Check(!nodes.OfType<Button>().Any(button => Equals(button.Content, "Настройки")), "Extra settings button survived");
            var auto = (CheckBox)window.FindName("AutoUpdateCheck");
            auto.SetCurrentValue(ToggleButton.IsCheckedProperty, !saved.AutoCheckUpdates);
            Check(SettingsService.Settings.AutoCheckUpdates == !saved.AutoCheckUpdates, "Update checkbox not saved immediately");
            var edited = (AboutWindow.EditableSettings)window.DataContext;
            edited.FrameInsertionScale = 17; edited.PlotScale = 100; edited.ScaleNumerator = 2; edited.ScaleDenominator = 3;
            edited.LengthUnit = 1; edited.AreaUnit = 2; edited.PlotOutputDirectory = Path.GetTempPath();
            edited.PlotTransparency = true; edited.KeepExistingPdfFiles = true;
            nodes.OfType<Button>().Single(button => Equals(button.Content, "Сохранить")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var actual = JsonConvert.DeserializeObject<PluginSettings>(File.ReadAllText(Path.Combine(PluginPaths.UserDataDirectory, "settings.json")));
            Check(actual.FrameInsertionScale == 17 && actual.PlotScale == 100 && actual.ScaleNumerator == 2 && actual.ScaleDenominator == 3 &&
                actual.LengthUnit == 1 && actual.AreaUnit == 2 && actual.PlotTransparency && actual.KeepExistingPdfFiles && actual.AutoCheckUpdates == !saved.AutoCheckUpdates,
                "Integrated settings were not saved");
            var legacy = new SettingsWindow();
            Check(legacy.FindName("SettingsSection") != null && ((AboutWindow.EditableSettings)legacy.DataContext).FrameInsertionScale == 17,
                "Legacy settings entry point does not open integrated service settings");
            legacy.Close();
        }
        finally
        {
            foreach (var property in typeof(PluginSettings).GetProperties()) property.SetValue(SettingsService.Settings, property.GetValue(saved));
            SettingsService.Save();
        }
    }
}
