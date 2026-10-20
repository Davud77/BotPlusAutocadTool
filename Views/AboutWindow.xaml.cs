// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using BotPlusAutocadTool.Infrastructure;
using BotPlusAutocadTool.Services;

namespace BotPlusAutocadTool.Views
{
    public partial class AboutWindow : Window
    {
        private bool _settingsLoaded;
        public AboutWindow()
        {
            InitializeComponent();
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            VersionText.Text = $"Версия {version.Major}.{version.Minor}.{version.Build}";
            AutoUpdateCheck.IsChecked = SettingsService.Settings.AutoCheckUpdates;
            PrereleaseUpdateCheck.IsChecked = SettingsService.Settings.IncludePrereleaseUpdates;
            _settingsLoaded = true;
        }

        private void UpdateSettingsChanged(object sender, RoutedEventArgs args)
        {
            if (!_settingsLoaded) return;
            SettingsService.Settings.AutoCheckUpdates = AutoUpdateCheck.IsChecked == true;
            SettingsService.Settings.IncludePrereleaseUpdates = PrereleaseUpdateCheck.IsChecked == true;
            SettingsService.Save();
        }

        private void OpenProject(object sender, RoutedEventArgs args) => Open("https://github.com/Davud77/BotPlusAutocadTool");
        private void CloseWindow(object sender, RoutedEventArgs args) => Close();
        private void OpenLogs(object sender, RoutedEventArgs args)
        {
            var directory = Path.Combine(PluginPaths.UserDataDirectory, "Logs");
            Directory.CreateDirectory(directory);
            Open(directory);
        }

        private static void Open(string target)
        {
            try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true })?.Dispose(); }
            catch (Exception ex)
            {
                PluginLog.Error("Open support resource", ex);
                MessageBox.Show("Не удалось открыть: " + target, "BotPlus AT", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
