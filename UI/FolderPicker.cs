// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace BotPlusAutocadTool.UI
{
    internal static class FolderPicker
    {
        internal static string Choose(string current, Window owner = null)
        {
            var dialog = new OpenFolderDialog { Title = "Выберите папку сохранения", Multiselect = false };
            if (!string.IsNullOrWhiteSpace(current) && Directory.Exists(current)) dialog.InitialDirectory = current;
            bool? accepted = owner == null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
            return accepted == true ? dialog.FolderName : null;
        }
    }
}
