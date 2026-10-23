// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BotPlusAutocadTool.Models
{
    public sealed class StampTransferTarget : INotifyPropertyChanged, IDisposable
    {
        private bool _selected;
        public bool IsSelected { get => _selected; set { _selected = value; OnPropertyChanged(); } }
        public string Name { get; set; }
        public string Location { get; set; }
        public string Status { get; set; }
        internal Action<StampProperties> Write { get; set; }
        internal Action Release { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;
        internal void SetStatus(string status) { Status = status; OnPropertyChanged(nameof(Status)); }
        private void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        public void Dispose() { Release?.Invoke(); Release = null; Write = null; }
    }
}
