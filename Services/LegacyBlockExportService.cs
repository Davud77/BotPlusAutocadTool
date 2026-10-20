// ============================================================================
// OOXGN - Copyright (c) 2026. All rights reserved.
// Author: OOXGN | Project: BotPlusAutocadTool
// ============================================================================

// File: Services/LegacyBlockExportService.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.PlottingServices;

namespace BotPlusAutocadTool.Services
{
    public static class LegacyBlockExportService
    {
        // --- Win32: short(8.3) -> long path ---
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetLongPathName(string lpszShortPath, StringBuilder lpszLongPath, uint cchBuffer);

        public static bool Run(Document doc, string dsdPath)
        {
            if (doc == null) return false;

            var db = doc.Database;
            var ed = doc.Editor;

            // Получаем корректный путь к DWG
            string dwgPathForDsd = GetBestDwgPath(doc);

            if (string.IsNullOrWhiteSpace(dwgPathForDsd) ||
                !Path.IsPathRooted(dwgPathForDsd) ||
                !File.Exists(dwgPathForDsd))
            {
                ed.WriteMessage("\n[BotPlus AT] Чертёж не сохранён или путь к DWG не найден. Сохраните DWG и повторите.");
                return false;
            }

            string folder = Path.GetDirectoryName(dwgPathForDsd) ?? "";
            string baseNameNoExt = Path.GetFileNameWithoutExtension(dwgPathForDsd) ?? "Drawing";

            var entries = new List<DsdSheet>();

            int index = 1;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var msBtr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                var psDict = (DBDictionary)tr.GetObject(db.PlotSettingsDictionaryId, OpenMode.ForRead);
                var usedSheetTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (ObjectId id in msBtr)
                {
                    if (!(tr.GetObject(id, OpenMode.ForRead) is BlockReference br))
                        continue;

                    Extents3d ext;
                    try { ext = br.GeometricExtents; }
                    catch { continue; }

                    string setupName = $"Revers-{index}-Model";

                    string rawTitle = $"{baseNameNoExt} Model {index}";
                    string sheetTitle = SanitizeSheetTitle(rawTitle);

                    if (!usedSheetTitles.Add(sheetTitle))
                    {
                        int k = 2;
                        string candidate;
                        do
                        {
                            candidate = SanitizeSheetTitle($"{rawTitle} ({k})");
                            k++;
                        } while (!usedSheetTitles.Add(candidate));
                        sheetTitle = candidate;
                    }

                    // Create / update PageSetup (PlotSettings)
                    PlotSettings ps;
                    if (psDict.Contains(setupName))
                    {
                        var psId = psDict.GetAt(setupName);
                        ps = (PlotSettings)tr.GetObject(psId, OpenMode.ForWrite);
                    }
                    else
                    {
                        var lm = LayoutManager.Current;
                        ObjectId modelLayoutId = lm.GetLayoutId("Model");
                        var modelLayout = (Layout)tr.GetObject(modelLayoutId, OpenMode.ForRead);

                        ps = new PlotSettings(modelLayout.ModelType);
                        ps.CopyFrom(modelLayout);

                        ps.PlotSettingsName = setupName;
                        ps.AddToPlotSettingsDictionary(db);
                        tr.AddNewlyCreatedDBObject(ps, true);

                        psDict = (DBDictionary)tr.GetObject(db.PlotSettingsDictionaryId, OpenMode.ForRead);
                    }

                    ApplyPlotSettingsForBlock(ps, ext);

                    entries.Add(new DsdSheet(dwgPathForDsd, "Model", setupName, sheetTitle));

                    index++;
                }

                tr.Commit();
                ed.WriteMessage($"\n[BotPlus AT] Создано/обновлено Page Setup'ов: {index - 1}");
            }

            if (index == 1)
            {
                ed.WriteMessage("\n[BotPlus AT] В модели не найдено ни одного блока для печати.");
                return false;
            }

            string pdfName = Path.Combine(folder, "WEAM_Model.pdf");

            DsdFileWriter.Write(dsdPath, pdfName, entries);

            ed.WriteMessage($"\n[BotPlus AT] DSD записан: {dsdPath}");
            ed.WriteMessage($"\n[BotPlus AT] DWG в DSD: {dwgPathForDsd}");
            ed.WriteMessage($"\n[BotPlus AT] PDF цель: {pdfName}");
            return true;
        }

        private static void ApplyPlotSettingsForBlock(PlotSettings ps, Extents3d ext)
        {
            PlotSettingsValidator psv = PlotSettingsValidator.Current;

            psv.RefreshLists(ps);

            psv.SetPlotConfigurationName(
                ps,
                "DWG To PDF.pc3",
                "ISO_A4_(210.00_x_297.00_MM)");

            var min2d = new Point2d(ext.MinPoint.X, ext.MinPoint.Y);
            var max2d = new Point2d(ext.MaxPoint.X, ext.MaxPoint.Y);
            psv.SetPlotWindowArea(ps, new Extents2d(min2d, max2d));

            psv.SetUseStandardScale(ps, true);
            psv.SetStdScaleType(ps, StdScaleType.ScaleToFit);
            psv.SetPlotCentered(ps, true);
        }

        private static string SanitizeSheetTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return "Sheet";

            return title
                .Replace("[", "_")
                .Replace("]", "_")
                .Replace(":", "_")
                .Trim();
        }

        private static string GetBestDwgPath(Document doc)
        {
            if (doc == null) return string.Empty;

            try
            {
                string dbFile = doc.Database?.Filename;
                if (!string.IsNullOrWhiteSpace(dbFile) && File.Exists(dbFile))
                    return ToLongPathIfPossible(dbFile);
            }
            catch { }

            // fallback: как есть
            return doc.Name ?? string.Empty;
        }

        private static string ToLongPathIfPossible(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;

            try
            {
                if (!File.Exists(path) && !Directory.Exists(path))
                    return path;

                var sb = new StringBuilder(512);
                uint len = GetLongPathName(path, sb, (uint)sb.Capacity);
                if (len == 0) return path;

                if (len >= sb.Capacity)
                {
                    sb = new StringBuilder((int)len + 1);
                    len = GetLongPathName(path, sb, (uint)sb.Capacity);
                    if (len == 0) return path;
                }

                return sb.ToString();
            }
            catch
            {
                return path;
            }
        }
    }
}
