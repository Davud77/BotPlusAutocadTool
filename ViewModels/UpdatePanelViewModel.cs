// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using BotPlusAutocadTool.Services;

namespace BotPlusAutocadTool.ViewModels
{
    public sealed class UpdatePanelViewModel : BaseViewModel
    {
        private sealed class Command : ICommand
        {
            private readonly Func<bool> _can;
            private readonly Action _run;
            public Command(Action run, Func<bool> can) { _run = run; _can = can; }
            public bool CanExecute(object parameter) => _can();
            public void Execute(object parameter) { if (_can()) _run(); }
            public event EventHandler CanExecuteChanged;
            public void Refresh() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
        private readonly Version _installed;
        private readonly Func<bool> _includePreview;
        private readonly Func<bool, CancellationToken, Task<ReleaseVersion>> _fetch;
        private readonly Func<ReleaseVersion, IProgress<UpdateDownloadProgress>, CancellationToken, Task<string>> _download;
        private readonly Action<string, Exception> _log;
        private readonly Command _checkCommand, _downloadCommand, _cancelCommand;
        private CancellationTokenSource _operation;
        private ReleaseVersion _release;
        private bool _closed, _checking, _downloading;
        private string _latest = "Не проверена", _status = "Нажмите «Проверить обновления».", _savedPath = "";
        private double _percent;

        public UpdatePanelViewModel(Version installed, Func<bool> includePreview,
            Func<bool, CancellationToken, Task<ReleaseVersion>> fetch,
            Func<ReleaseVersion, IProgress<UpdateDownloadProgress>, CancellationToken, Task<string>> download,
            Action<string, Exception> log = null)
        {
            _installed = new Version(installed.Major, installed.Minor, Math.Max(0, installed.Build));
            _includePreview = includePreview; _fetch = fetch; _download = download; _log = log;
            _checkCommand = new Command(async () => await CheckAsync(), () => !_closed && !IsBusy);
            _downloadCommand = new Command(async () => await DownloadAsync(), () => CanDownload);
            _cancelCommand = new Command(Cancel, () => !_closed && IsBusy);
        }
        public string CurrentVersion => _installed.Build == 0 ? _installed.ToString(2) : _installed.ToString(3);
        public string LatestVersion { get => _latest; private set => SetProperty(ref _latest, value); }
        public string Status { get => _status; private set => SetProperty(ref _status, value); }
        public string SavedPath { get => _savedPath; private set { if (SetProperty(ref _savedPath, value)) OnPropertyChanged(nameof(HasSavedPath)); } }
        public bool HasSavedPath => !string.IsNullOrEmpty(SavedPath);
        public bool IsBusy => _checking || _downloading;
        public bool IsIndeterminate { get; private set; }
        public double ProgressPercent { get => _percent; private set => SetProperty(ref _percent, value); }
        public bool CanDownload => !_closed && !IsBusy && _release?.InstallerUrl != null && (!_release.IsPrerelease || _includePreview());
        public ICommand CheckCommand => _checkCommand;
        public ICommand DownloadCommand => _downloadCommand;
        public ICommand CancelCommand => _cancelCommand;

        private void RefreshState()
        {
            OnPropertiesChanged(nameof(IsBusy), nameof(IsIndeterminate), nameof(CanDownload));
            _checkCommand.Refresh(); _downloadCommand.Refresh(); _cancelCommand.Refresh();
        }
        public void PresentRelease(ReleaseVersion release)
        {
            if (_closed || IsBusy) return;
            ApplyRelease(release);
            RefreshState();
        }
        private void ApplyRelease(ReleaseVersion release)
        {
            _release = release;
            LatestVersion = release == null ? "Не опубликована" : release.Tag.TrimStart('v', 'V');
            Status = release == null ? "Опубликованные версии не найдены." : release.Version > _installed ? "Доступна новая версия." :
                release.Version == _installed ? "Установлена актуальная версия." : "Установленная версия новее опубликованной.";
            if (release != null && release.InstallerUrl == null) Status += " Установщик ещё не опубликован.";
        }
        public void ChannelChanged()
        {
            if (_closed || _downloading) return;
            _operation?.Cancel();
            _release = null; LatestVersion = "Не проверена";
            Status = "Настройка выпусков изменена. Проверьте обновления ещё раз.";
            RefreshState();
        }
        public async Task CheckAsync()
        {
            if (_closed || IsBusy) return;
            var cancellation = new CancellationTokenSource(); var token = cancellation.Token;
            _operation = cancellation; _checking = true; _release = null;
            LatestVersion = "Проверка…"; Status = "Проверяем опубликованные версии…";
            IsIndeterminate = true; ProgressPercent = 0; RefreshState();
            bool preview = _includePreview();
            try
            {
                var release = await _fetch(preview, token);
                token.ThrowIfCancellationRequested();
                if (_closed) return;
                if (preview != _includePreview()) { ChannelChanged(); return; }
                ApplyRelease(release);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            { if (!_closed) { LatestVersion = "Не проверена"; Status = "Проверка отменена. Можно повторить её."; } }
            catch (Exception ex)
            {
                _log?.Invoke("Check latest release", ex);
                if (!_closed) { LatestVersion = "Не удалось получить"; Status = "Не удалось проверить обновления: " + ex.Message; }
            }
            finally
            {
                if (ReferenceEquals(_operation, cancellation)) _operation = null;
                _checking = false; IsIndeterminate = false; cancellation.Dispose();
                if (!_closed) RefreshState();
            }
        }
        public async Task DownloadAsync()
        {
            if (!CanDownload) return;
            var release = _release;
            var cancellation = new CancellationTokenSource(); var token = cancellation.Token;
            _operation = cancellation; _downloading = true;
            SavedPath = ""; Status = "Подготовка загрузки…"; IsIndeterminate = true; ProgressPercent = 0; RefreshState();
            var progress = new Progress<UpdateDownloadProgress>(value =>
            {
                if (_closed || !ReferenceEquals(_operation, cancellation) || token.IsCancellationRequested) return;
                IsIndeterminate = value.Total <= 0; ProgressPercent = value.Percent;
                Status = value.Total > 0 ? $"Скачано {value.Received / 1048576.0:F1} из {value.Total / 1048576.0:F1} МБ ({value.Percent:F0}%)" : $"Скачано {value.Received / 1048576.0:F1} МБ";
                OnPropertyChanged(nameof(IsIndeterminate));
            });
            try
            {
                string path = await _download(release, progress, token);
                token.ThrowIfCancellationRequested();
                if (_closed) return;
                SavedPath = path; ProgressPercent = 100;
                Status = "Установщик скачан, SHA-256 проверена. Закройте AutoCAD и запустите этот файл:";
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            { if (!_closed) Status = "Загрузка отменена. Можно повторить её."; }
            catch (Exception ex)
            {
                _log?.Invoke("Download latest release", ex);
                if (!_closed) Status = "Не удалось скачать обновление: " + ex.Message + " Можно повторить загрузку.";
            }
            finally
            {
                if (ReferenceEquals(_operation, cancellation)) _operation = null;
                _downloading = false; IsIndeterminate = false; cancellation.Dispose();
                if (!_closed) RefreshState();
            }
        }
        public void ReportCopyError(Exception ex) => Status = "Не удалось скопировать путь: " + ex.Message;
        private void Cancel() { _operation?.Cancel(); Status = "Отмена…"; }
        protected override void OnDisposing() { _closed = true; _operation?.Cancel(); }
    }
}
