// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.PlottingServices;
using Autodesk.AutoCAD.Runtime;
using BotPlusAutocadTool.Models;
using BotPlusAutocadTool.Services;
using BotPlusAutocadTool.ViewModels;
using A = Autodesk.AutoCAD.ApplicationServices.Application;

public sealed class PlotStyle17Checks
{
    [CommandMethod("BOTPLUS_PLOTSTYLE17TEST")]
    public void Run()
    {
        var doc = A.DocumentManager.MdiActiveDocument;
        var db = doc.Database;
        var oldBackground = A.GetSystemVariable("BACKGROUNDPLOT");
        var oldTransparency = A.GetSystemVariable("PLOTTRANSPARENCYOVERRIDE");
        try
        {
            var choices = PlotStyleService.GetChoices(db);
            string extension = PlotStyleService.Extension(db);
            string mono = choices.Select(choice => choice.FileName).FirstOrDefault(name => string.Equals(name, "monochrome" + extension, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("Native monochrome style missing: " + string.Join(",", choices.Select(choice => choice.Name)));
            if (choices.Where(choice => !string.IsNullOrEmpty(choice.FileName)).Any(choice => !choice.FileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Incompatible plot styles are listed");
            foreach (string invalid in new[] { "missing-" + Guid.NewGuid().ToString("N") + extension, "wrong" + (db.PlotStyleMode ? ".stb" : ".ctb") })
            {
                bool rejected = false;
                try { PlotStyleService.Resolve(db, invalid); } catch (InvalidOperationException) { rejected = true; }
                if (!rejected) throw new InvalidOperationException("Invalid style was accepted");
            }
            using (var vm = new BatchPlotViewModel()) vm.SelectedPlotStyle = vm.PlotStyleChoices.Single(choice => choice.FileName == mono);
            using (var vm = new BatchPlotViewModel())
            {
                if (vm.SelectedPlotStyle.FileName != mono) throw new InvalidOperationException("Plot style choice was not restored");
                vm.SelectedPlotStyle = vm.PlotStyleChoices.First();
            }
            var item = Plot141Checks.Frame(db);
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var block = (BlockReference)tr.GetObject(item.BlockId, OpenMode.ForRead);
                var definition = (BlockTableRecord)tr.GetObject(block.BlockTableRecord, OpenMode.ForRead);
                foreach (var id in definition.Cast<ObjectId>())
                    if (tr.GetObject(id, OpenMode.ForWrite) is Solid solid) solid.ColorIndex = 1;
                var model = (Layout)tr.GetObject(LayoutManager.Current.GetLayoutId("Model"), OpenMode.ForWrite);
                PlotSettingsValidator.Current.SetCurrentStyleSheet(model, mono); model.PlotPlotStyles = true;
                tr.Commit();
            }
            string folder = Path.Combine(Path.GetDirectoryName(doc.Name), "style17-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "mode.txt"), db.PlotStyleMode ? "CTB" : "STB");
            A.SetSystemVariable("BACKGROUNDPLOT", 0); A.SetSystemVariable("PLOTTRANSPARENCYOVERRIDE", 0);
            // Reuse one named page setup: no stale style may survive a change.
            foreach (var entry in new[] { ("mono", mono), ("off", ""), ("dwg", (string)null) })
            {
                string dsd = Path.Combine(folder, entry.Item1 + ".dsd"), pdf = Path.Combine(folder, entry.Item1 + ".pdf");
                if (!BlockExportStage1Service.Run(doc, dsd, new List<BatchPlotItem> { item }, pdf, false, entry.Item2)) throw new InvalidOperationException("Style preparation failed");
                using (var tr = db.TransactionManager.StartTransaction())
                using (var data = new DsdData())
                {
                    data.ReadDsd(dsd);
                    var dict = (DBDictionary)tr.GetObject(db.PlotSettingsDictionaryId, OpenMode.ForRead);
                    var settings = (PlotSettings)tr.GetObject(dict.GetAt(data.GetDsdEntryCollection()[0].Nps), OpenMode.ForRead);
                    if (settings.PlotPlotStyles != (entry.Item1 != "off") || !string.Equals(settings.CurrentStyleSheet, entry.Item1 == "off" ? "" : mono, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Style flags did not reach page setup: " + settings.CurrentStyleSheet);
                }
                doc.Editor.Command("_.QSAVE");
                using (var data = new DsdData())
                {
                    data.ReadDsd(dsd); data.SheetType = SheetType.MultiPdf;
                    A.Publisher.PublishExecute(data, PlotConfigManager.SetCurrentConfig(PdfPlotterService.PlotterName));
                }
                if (!PdfPostProcessingService.ProcessPdf(pdf, new[] { item }, doc.Editor)) throw new InvalidOperationException("Style PDF processing failed");
            }
            doc.Editor.WriteMessage("\nBOTPLUS_STYLE17_PASS: " + extension + " choices, persistence, missing/wrong style rejected, reused setup, style on/off/from-DWG and native PDFs; " + folder + "\n");
        }
        catch (System.Exception ex) { doc.Editor.WriteMessage("\nBOTPLUS_STYLE17_FAIL: " + ex + "\n"); }
        finally { A.SetSystemVariable("BACKGROUNDPLOT", oldBackground); A.SetSystemVariable("PLOTTRANSPARENCYOVERRIDE", oldTransparency); }
    }
}
