// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.IO;

namespace BotPlusAutocadTool.Services
{
    internal static class PdfOutputPaths
    {
        internal static string SheetFileName(int index, string sheetNumber)
        {
            string number = sheetNumber ?? (index + 1).ToString();
            foreach (char invalid in Path.GetInvalidFileNameChars()) number = number.Replace(invalid, '_');
            return $"{index + 1:D3}_Лист_{number}.pdf";
        }
        internal static string[] OpenTargets(string output, bool separateSheets, bool openFolder, bool openFile, string firstSheetNumber)
        {
            var targets = new List<string>();
            if (openFolder) targets.Add(separateSheets ? output : Path.GetDirectoryName(Path.GetFullPath(output)));
            if (openFile) targets.Add(separateSheets ? Path.Combine(output, SheetFileName(0, firstSheetNumber)) : output);
            return targets.ToArray();
        }
    }
}
