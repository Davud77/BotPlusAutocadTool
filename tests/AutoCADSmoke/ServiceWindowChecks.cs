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
using BotPlusAutocadTool.ViewModels;
using System.Threading.Tasks;

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
            var header = (Grid)window.FindName("ServiceHeader");
            var description = (TextBlock)window.FindName("ProductDescription");
            Check(header.Children.Contains(description) && Grid.GetColumn(description) == 2,
                "Product description is not beside BotPlus AT");
            var panel = (UpdatePanel)window.FindName("InlineUpdates");
            panel.Model.Dispose();
            var release = ReleaseVersion.FindLatest("[{\"tag_name\":\"v1.7.1\",\"html_url\":\"https://github.com/Davud77/BotPlusAutocadTool/releases/tag/v1.7.1\",\"assets\":[{\"name\":\"BotPlusAutocadTool-1.7.1-Setup.exe\",\"browser_download_url\":\"https://github.com/Davud77/BotPlusAutocadTool/releases/download/v1.7.1/BotPlusAutocadTool-1.7.1-Setup.exe\"}]}]");
            panel.Model = new UpdatePanelViewModel(new Version(1,7,1), () => false,
                (_, _) => Task.FromResult(release), (_, _, _) => Task.FromResult(@"C:\Updates\BotPlusAutocadTool-1.7.1-Setup.exe"));
            panel.Model.CheckAsync().GetAwaiter().GetResult(); root.UpdateLayout();
            Check(Children(panel).OfType<TextBlock>().Count(text => text.Text == "1.7.1") == 2,
                "Inline current/latest version bindings failed");
            var download = Children(panel).OfType<Button>().Single(button => Equals(button.Content, "Скачать актуальную"));
            Check(download.IsEnabled, "Current release installer button disabled");
            panel.Model.DownloadAsync().GetAwaiter().GetResult(); root.UpdateLayout();
            Check(Children(panel).OfType<TextBox>().Any(text => text.IsReadOnly && text.Text == panel.Model.SavedPath && text.IsVisible == panel.IsVisible),
                "Downloaded installer path is not available in service window");
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
