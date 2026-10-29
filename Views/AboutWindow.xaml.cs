// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json;
using BotPlusAutocadTool.Infrastructure;
using BotPlusAutocadTool.Services;

namespace BotPlusAutocadTool.Views
{
    public partial class AboutWindow : Window
    {
        public sealed class EditableSettings : PluginSettings
        {
            public int PlotScaleIndex { get => PlotScale == 100 ? 1 : 0; set => PlotScale = value == 1 ? 100 : 1; }
        }
        private bool _loading;
        public AboutWindow()
        {
            InitializeComponent();
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            VersionText.Text = "Версия " + (version.Build == 0 ? version.ToString(2) : version.ToString(3));
            LoadEditor(JsonConvert.DeserializeObject<EditableSettings>(JsonConvert.SerializeObject(SettingsService.Settings)));
            AddHandler(System.Windows.Controls.Primitives.ToggleButton.CheckedEvent, new RoutedEventHandler(SaveCheckbox), true);
            AddHandler(System.Windows.Controls.Primitives.ToggleButton.UncheckedEvent, new RoutedEventHandler(SaveCheckbox), true);
        }

        private void LoadEditor(EditableSettings settings)
        {
            _loading = true;
            try { DataContext = settings; }
            finally { _loading = false; }
        }
        private void SaveCheckbox(object sender, RoutedEventArgs args)
        {
            if (_loading || args.OriginalSource is not CheckBox checkbox) return;
            string path = checkbox.GetBindingExpression(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty)?.ParentBinding.Path?.Path;
            var property = path == null ? null : typeof(PluginSettings).GetProperty(path);
            if (property?.PropertyType != typeof(bool)) return;
            bool value = checkbox.IsChecked == true;
            if ((bool)property.GetValue(SettingsService.Settings) == value) return;
            property.SetValue(SettingsService.Settings, value);
            SettingsService.Save();
        }

        private void OpenProject(object sender, RoutedEventArgs args) => Open("https://github.com/Davud77/BotPlusAutocadTool");
        private void ResetClick(object sender, RoutedEventArgs args) => LoadEditor(new EditableSettings());
        private void BrowseOutputClick(object sender, RoutedEventArgs args)
        {
            var edited = (EditableSettings)DataContext;
            string folder = UI.FolderPicker.Choose(edited.PlotOutputDirectory, this);
            if (folder == null) return;
            edited.PlotOutputDirectory = folder;
            OutputFolderBox.SetCurrentValue(TextBox.TextProperty, folder);
            OutputFolderBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        }
        private static bool HasErrors(DependencyObject parent)
        {
            if (Validation.GetHasError(parent)) return true;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
                if (HasErrors(VisualTreeHelper.GetChild(parent, i))) return true;
            return false;
        }
        private void SaveClick(object sender, RoutedEventArgs args)
        {
            var edited = (EditableSettings)DataContext;
            if (HasErrors(this) || !double.IsFinite(edited.FrameInsertionScale) || edited.FrameInsertionScale <= 0 ||
                edited.FrameInsertionScale > 1000000 || edited.ScaleNumerator < 1 || edited.ScaleDenominator < 1)
            { MessageBox.Show(this, "Проверьте числа. Масштабы должны быть положительными; масштаб вставки — не больше 1000000.", Title); return; }
            if (!string.IsNullOrWhiteSpace(edited.PlotOutputDirectory) && !Path.IsPathFullyQualified(edited.PlotOutputDirectory))
            { MessageBox.Show(this, "Укажите полный путь к папке PDF или оставьте поле пустым.", Title); return; }
            var settings = SettingsService.Settings;
            settings.FrameInsertionScale = edited.FrameInsertionScale;
            settings.PlotScale = edited.PlotScale;
            settings.PlotOutputDirectory = edited.PlotOutputDirectory?.Trim() ?? "";
            settings.PlotInBackground = edited.PlotInBackground;
            settings.PlotTransparency = edited.PlotTransparency;
            settings.KeepExistingPdfFiles = edited.KeepExistingPdfFiles;
            settings.OpenFolderAfterPlot = edited.OpenFolderAfterPlot;
            settings.OpenFileAfterPlot = edited.OpenFileAfterPlot;
            settings.ScaleNumerator = edited.ScaleNumerator; settings.ScaleDenominator = edited.ScaleDenominator;
            settings.LengthUnit = edited.LengthUnit; settings.AreaUnit = edited.AreaUnit;
            settings.AutoCheckUpdates = edited.AutoCheckUpdates; settings.IncludePrereleaseUpdates = edited.IncludePrereleaseUpdates;
            if (!SettingsService.TrySave())
            { MessageBox.Show(this, "Не удалось сохранить настройки. Подробности записаны в журнал.", Title); return; }
            Close();
        }
        private void CheckUpdates(object sender, RoutedEventArgs args) => UpdateCheckService.Check(true);
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
