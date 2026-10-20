// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Diagnostics;

namespace BotPlusAutocadTool.Services
{
    internal static class PublishedOutputService
    {
        internal static void Open(string output, bool separateSheets, bool openFolder, bool openFile, string firstSheetNumber, Action<string> warning)
        {
            foreach (var target in PdfOutputPaths.OpenTargets(output, separateSheets, openFolder, openFile, firstSheetNumber))
            {
                try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true })?.Dispose(); }
                catch (Exception ex)
                {
                    Infrastructure.PluginLog.Error("Open published output", ex);
                    warning?.Invoke("PDF сохранён, но не удалось открыть: " + target + ". " + ex.Message);
                }
            }
        }
    }
}
