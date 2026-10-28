// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using BotPlusAutocadTool.Services;

namespace BotPlusAutocadTool.Views
{
    public partial class UpdateDownloadWindow : Window
    {
        private readonly ReleaseVersion _release;
        private CancellationTokenSource _download;
        private string _saved;
        private bool _closed;

        public UpdateDownloadWindow(ReleaseVersion release)
        {
            InitializeComponent();
            _release = release;
            VersionText.Text = "Доступна версия " + release.Tag;
            Closed += (_, _) => { _closed = true; _download?.Cancel(); };
        }

        private async void DownloadClick(object sender, RoutedEventArgs e)
        {
            if (_download != null) return;
            var cancellation = new CancellationTokenSource();
            _download = cancellation;
            DownloadButton.IsEnabled = false;
            CloseButton.Content = "Отменить загрузку";
            DownloadProgress.Visibility = Visibility.Visible;
            DownloadProgress.IsIndeterminate = true;
            StatusText.Text = "Подготовка загрузки…";
            var progress = new Progress<UpdateDownloadProgress>(value =>
            {
                if (_closed) return;
                DownloadProgress.IsIndeterminate = value.Total <= 0;
                DownloadProgress.Value = value.Percent;
                StatusText.Text = value.Total > 0 ? $"Скачано {value.Received / 1048576.0:F1} из {value.Total / 1048576.0:F1} МБ ({value.Percent:F0}%)" :
                    $"Скачано {value.Received / 1048576.0:F1} МБ";
            });
            try
            {
                _saved = await UpdateDownloadService.DownloadAsync(_release,
                    Path.Combine(PluginPaths.UserDataDirectory, "Updates", _release.Version.ToString()), progress, cancellation.Token);
                if (_closed) return;
                DownloadProgress.IsIndeterminate = false;
                DownloadProgress.Value = 100;
                StatusText.Text = "Установщик скачан, SHA-256 проверена. Закройте AutoCAD и запустите этот файл:";
                SavedPathText.Text = _saved;
                FolderButton.Visibility = Visibility.Visible;
                DownloadButton.Visibility = Visibility.Collapsed;
            }
            catch (OperationCanceledException)
            {
                if (!_closed) StatusText.Text = "Загрузка отменена. Можно повторить её кнопкой «Обновить».";
            }
            catch (System.Exception ex)
            {
                Infrastructure.PluginLog.Error("Download update", ex);
                if (!_closed) StatusText.Text = "Не удалось скачать обновление: " + ex.Message + " Можно повторить загрузку.";
            }
            finally
            {
                _download = null;
                cancellation.Dispose();
                if (!_closed)
                {
                    DownloadProgress.IsIndeterminate = false;
                    DownloadButton.IsEnabled = true;
                    CloseButton.Content = "Закрыть";
                }
            }
        }

        private void CloseClick(object sender, RoutedEventArgs e)
        {
            if (_download != null) _download.Cancel();
            else Close();
        }

        private void OpenFolderClick(object sender, RoutedEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo(Path.GetDirectoryName(_saved)) { UseShellExecute = true }); }
            catch (System.Exception ex) { StatusText.Text = "Не удалось открыть папку: " + ex.Message; }
        }
    }
}
