// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Windows;
using System.Windows.Controls;
using BotPlusAutocadTool.ViewModels;

namespace BotPlusAutocadTool.Views
{
    public partial class UpdatePanel : UserControl
    {
        public UpdatePanel() => InitializeComponent();
        public UpdatePanelViewModel Model { get => DataContext as UpdatePanelViewModel; set => DataContext = value; }
        private void CopyPathClick(object sender, RoutedEventArgs args)
        {
            try { if (Model?.HasSavedPath == true) Clipboard.SetText(Model.SavedPath); }
            catch (Exception ex) { Model?.ReportCopyError(ex); }
        }
    }
}
