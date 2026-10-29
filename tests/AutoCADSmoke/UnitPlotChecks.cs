// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.PlottingServices;
using Autodesk.AutoCAD.Runtime;
using BotPlusAutocadTool.Models;
using BotPlusAutocadTool.Services;
using A = Autodesk.AutoCAD.ApplicationServices.Application;

public sealed class UnitPlotChecks
{
    [CommandMethod("BOTPLUS_UNITINSERTTEST")]
    public void InsertUnits()
    {
        var doc = A.DocumentManager.MdiActiveDocument;
        var db = doc.Database;
        var originalUnits = db.Insunits;
        try
        {
            var definition = FormatInsertService.EnsureDefinition(db);
            double referenceWidth = 0, referenceHeight = 0;
            foreach (int code in new[] { 4, 6, 7, 1, 2 })
            foreach (int scale in new[] { 1, 100 })
            {
                db.Insunits = (UnitsValue)code;
                var id = FormatInsertService.Insert(db, definition, Point3d.Origin, scale);
                var service = new BatchPlotService { PlotScale = scale };
                var item = service.GetFormatBlocks().Single(i => i.BlockId == id);
                double factor = PlotUnits.Get(code).MillimetersPerUnit / scale;
                double width = (item.Extents.MaxPoint.X - item.Extents.MinPoint.X) * factor;
                double height = (item.Extents.MaxPoint.Y - item.Extents.MinPoint.Y) * factor;
                if (referenceWidth == 0) { referenceWidth = width; referenceHeight = height; }
                if (Math.Abs(width - referenceWidth) > .01 || Math.Abs(height - referenceHeight) > .01 || item.FormatDefinition == null)
                    throw new InvalidOperationException("Inserted frame changed physical size for unit " + code);
                using var tr = db.TransactionManager.StartTransaction();
                tr.GetObject(id, OpenMode.ForWrite).Erase(); tr.Commit();
            }
            var vm = new BotPlusAutocadTool.ViewModels.BatchPlotViewModel();
            vm.DrawingUnitsChoice = 2; vm.Dispose();
            using var restored = new BotPlusAutocadTool.ViewModels.BatchPlotViewModel();
            if (restored.DrawingUnitsChoice != 2) throw new InvalidOperationException("Per-DWG units selection was not restored");
            restored.DrawingUnitsChoice = 0;
            doc.Editor.WriteMessage("\nBOTPLUS_UNITINSERT_PASS: physical insertion size in mm/metres/km/inches/feet, 1:1/1:100; per-DWG unit selection restored\n");
        }
        catch (System.Exception ex) { doc.Editor.WriteMessage("\nBOTPLUS_UNITINSERT_FAIL: " + ex + "\n"); }
        finally { db.Insunits = originalUnits; }
    }

    [CommandMethod("BOTPLUS_UNITPLOTTEST")]
    public void Run()
    {
        var doc = A.DocumentManager.MdiActiveDocument;
        var db = doc.Database;
        var background = A.GetSystemVariable("BACKGROUNDPLOT");
        try
        {
            string folder = Path.Combine(Path.GetDirectoryName(doc.Name), "units15-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            A.SetSystemVariable("BACKGROUNDPLOT", 0);
            A.SetSystemVariable("PLOTTRANSPARENCYOVERRIDE", 0);
            // Each case is a physically identical frame expressed in different coordinates.
            foreach (var code in new[] { 4, 5, 14, 6, 7, 1, 2, 10, 13, 21, 0, -2 })
            foreach (int scale in new[] { 1, 100 })
            foreach (int measurement in new[] { 0, 1 })
            {
                int physicalCode = code == 0 ? 6 : Math.Abs(code);
                var unit = PlotUnits.Get(physicalCode);
                db.Insunits = (UnitsValue)(code < 0 ? 4 : code);
                db.Measurement = (MeasurementValue)measurement;
                bool wide = scale == 100;
                double width = wide ? 1783 : 594, height = wide ? 841 : 420;
                var frame = Frame(db, width, height, scale / unit.MillimetersPerUnit, physicalCode);
                var service = new BatchPlotService { PlotScale = scale, BlockName = frame.Name, DrawingUnitsOverride = code < 0 ? physicalCode : null };
                var items = service.GetFormatBlocks().Where(i => i.BlockId == frame.Id).ToList();
                if (items.Count != 1 || items[0].FormatDefinition?.Name != (wide ? "A1x3" : "A2"))
                    throw new InvalidOperationException("Paper recognition failed for unit " + code + ", scale " + scale);
                var item = items[0]; item.SheetNumber = "units"; item.Name = "units";
                string stem = $"u{code}-s{scale}-m{measurement}";
                string dsd = Path.Combine(folder, stem + ".dsd"), pdf = Path.Combine(folder, stem + ".pdf");
                // Both new and reused page setups start with imperial paper units.
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var model = (Layout)tr.GetObject(LayoutManager.Current.GetLayoutId("Model"), OpenMode.ForWrite);
                    PlotSettingsValidator.Current.SetPlotPaperUnits(model, PlotPaperUnit.Inches);
                    tr.Commit();
                }
                if (!BlockExportStage1Service.Run(doc, dsd, items, pdf, false)) throw new InvalidOperationException("Unit-aware preparation failed");
                using (var tr = db.TransactionManager.StartTransaction())
                using (var data = new DsdData())
                {
                    data.ReadDsd(dsd);
                    var dict = (DBDictionary)tr.GetObject(db.PlotSettingsDictionaryId, OpenMode.ForRead);
                    var ps = (PlotSettings)tr.GetObject(dict.GetAt(data.GetDsdEntryCollection()[0].Nps), OpenMode.ForRead);
                    double effective = ps.CustomPrintScale.Numerator / ps.CustomPrintScale.Denominator;
                    if (ps.PlotPaperUnits != PlotPaperUnit.Millimeters || Math.Abs(effective / (unit.MillimetersPerUnit / scale) - 1) > 1e-9 ||
                        Math.Abs(item.PlotWidthMm - width) > .01 || Math.Abs(item.PlotHeightMm - height) > .01)
                        throw new InvalidOperationException("Wrong units/physical scale for " + stem);
                }
                doc.Editor.Command("_.QSAVE");
                using (var data = new DsdData())
                {
                    data.ReadDsd(dsd); data.SheetType = SheetType.MultiPdf;
                    A.Publisher.PublishExecute(data, PlotConfigManager.SetCurrentConfig(PdfPlotterService.PlotterName));
                }
                if (!PdfPostProcessingService.ProcessPdf(pdf, items, doc.Editor)) throw new InvalidOperationException("Unit PDF processing failed");
                File.AppendAllText(Path.Combine(folder, "frames.tsv"), $"{stem}\t{width}\t{height}\n");
                doc.Editor.WriteMessage("\nUNIT_CASE_PASS: " + stem + "\n");
            }
            doc.Editor.WriteMessage("\nBOTPLUS_UNITS_PASS: 48 metric/imperial, unitless and override cases; " + folder + "\n");
        }
        catch (System.Exception ex) { doc.Editor.WriteMessage("\nBOTPLUS_UNITS_FAIL: " + ex + "\n"); }
        finally { A.SetSystemVariable("BACKGROUNDPLOT", background); }
    }

    private static (ObjectId Id, string Name) Frame(Database db, double width, double height, double factor, int units)
    {
        using var tr = db.TransactionManager.StartTransaction();
        var table = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForWrite);
        var model = (BlockTableRecord)tr.GetObject(table[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
        foreach (ObjectId entity in model) tr.GetObject(entity, OpenMode.ForWrite).Erase();
        var definition = new BlockTableRecord { Name = "BOTPLUS_UNIT_FRAME_" + Guid.NewGuid().ToString("N"), Units = (UnitsValue)units };
        table.Add(definition); tr.AddNewlyCreatedDBObject(definition, true);
        var outline = new Polyline();
        foreach (var p in new[] { new Point2d(0, 0), new Point2d(width * factor, 0), new Point2d(width * factor, height * factor), new Point2d(0, height * factor) })
            outline.AddVertexAt(outline.NumberOfVertices, p, 0, 0, 0);
        outline.Closed = true; definition.AppendEntity(outline); tr.AddNewlyCreatedDBObject(outline, true);
        foreach (var p in new[] { ("LL", 8.0, 8.0), ("LR", width - 25, 8.0), ("UL", 8.0, height - 15), ("UR", width - 25, height - 15), ("CENTER", width / 2, height / 2) })
        {
            var text = new DBText { TextString = p.Item1, Position = new Point3d(p.Item2 * factor, p.Item3 * factor, 0), Height = 4 * factor };
            definition.AppendEntity(text); tr.AddNewlyCreatedDBObject(text, true);
        }
        var block = new BlockReference(Point3d.Origin, definition.ObjectId);
        var layers = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
        if (!layers.Has("WM_FORMAT")) { layers.UpgradeOpen(); var layer = new LayerTableRecord { Name = "WM_FORMAT" }; layers.Add(layer); tr.AddNewlyCreatedDBObject(layer, true); }
        block.Layer = "WM_FORMAT";
        model.AppendEntity(block); tr.AddNewlyCreatedDBObject(block, true);
        var outlier = new AttributeReference { Tag = "OUTSIDE", TextString = "OUTSIDE", Position = new Point3d(width * factor * 5, 0, 0), Height = 10 * factor, Invisible = true };
        block.AttributeCollection.AppendAttribute(outlier); tr.AddNewlyCreatedDBObject(outlier, true);
        tr.Commit();
        // Filter on the exact definition name while leaving the previous definitions reusable.
        return (block.ObjectId, definition.Name);
    }
}
