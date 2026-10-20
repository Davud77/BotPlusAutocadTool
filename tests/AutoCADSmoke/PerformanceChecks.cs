using System;
using System.Collections.Generic;
using System.Diagnostics;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using BotPlusAutocadTool.Models;
using BotPlusAutocadTool.Services;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

public sealed class PerformanceChecks
{
    [CommandMethod("BOTPLUS_BENCHMARK")]
    public void Run()
    {
        var editor = AcadApp.DocumentManager.MdiActiveDocument.Editor;
        try
        {
            BenchmarkTables(editor);
            using var db = new Database(true, true);
            using var tr = db.TransactionManager.StartTransaction();
            var layers = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForWrite);
            var layerIds = new List<ObjectId>();
            for (int i = 0; i < 500; i++)
            {
                var layer = new LayerTableRecord { Name = "BENCH_" + i };
                layerIds.Add(layers.Add(layer));
                tr.AddNewlyCreatedDBObject(layer, true);
            }
            var blocks = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var space = (BlockTableRecord)tr.GetObject(blocks[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            var sources = new List<Entity>();
            for (int i = 0; i < 12000; i++)
            {
                var line = new Line(new Point3d(i, 0, 0), new Point3d(i, 100, 0)) { LayerId = layerIds[i % 500] };
                space.AppendEntity(line);
                tr.AddNewlyCreatedDBObject(line, true);
                if (i < 250) sources.Add(line);
            }
            var service = new SelectSimilarService(new SelectSimilarOptions { AddSourceObjects = false });
            service.FindSimilar(tr, space, sources); // JIT/cache warmup outside measurement.
            var timings = new List<double>();
            for (int i = 0; i < 3; i++)
            {
                var watch = Stopwatch.StartNew();
                var result = service.FindSimilar(tr, space, sources);
                watch.Stop();
                if (result.Count != 5750) throw new InvalidOperationException("Selection semantics changed: " + result.Count);
                timings.Add(watch.Elapsed.TotalMilliseconds);
            }
            timings.Sort();
            editor.WriteMessage("\nBOTPLUS_PERF_PASS: selected=5750; entities=12000; sources=250; median_ms=" +
                timings[1].ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "\n");
        }
        catch (System.Exception ex) { editor.WriteMessage("\nBOTPLUS_PERF_FAIL: " + ex + "\n"); }
    }
    private static void BenchmarkTables(Autodesk.AutoCAD.EditorInput.Editor editor)
    {
        using var db = new Database(true, true);
        var ids = new List<ObjectId>();
        using (var tr = db.TransactionManager.StartTransaction())
        {
            var model = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);
            for (int index = 0; index < 80; index++)
            {
                var table = new Table { TableStyle = db.Tablestyle, Position = new Point3d(index * 100, 0, 0) };
                table.SetSize(12, 6); table.SetRowHeight(8); table.SetColumnWidth(25);
                table.Cells[2, 1].TextString = "old"; table.Cells[4, 4].TextString = "preserve";
                table.GenerateLayout(); ids.Add(model.AppendEntity(table)); tr.AddNewlyCreatedDBObject(table, true);
            }
            tr.Commit();
        }
        var settings = new AutoNumSettings { Mode = AutoNumSettings.NumberingMode.Table, TableRow = 3, TableColumn = 2 };
        var service = new NumberingService(); service.Number(db, editor, ids, settings);
        var before = new List<double>(); var after = new List<double>();
        for (int run = 0; run < 3; run++)
        {
            var watch = Stopwatch.StartNew();
            using (var tr = db.TransactionManager.StartTransaction())
            {
                for (int index = 0; index < ids.Count; index++)
                {
                    var table = (Table)tr.GetObject(ids[index], OpenMode.ForWrite);
                    table.Cells[2, 1].TextString = (index + 100 * run + 1).ToString();
                    table.GenerateLayout();
                }
                tr.Commit();
            }
            before.Add(watch.Elapsed.TotalMilliseconds);
            settings.StartNumber = 100 * run + 501;
            watch.Restart(); service.Number(db, editor, ids, settings); after.Add(watch.Elapsed.TotalMilliseconds);
        }
        before.Sort(); after.Sort();
        using (var tr = db.TransactionManager.StartTransaction())
            foreach (var id in ids)
                if (((Table)tr.GetObject(id, OpenMode.ForRead)).Cells[4, 4].TextString != "preserve") throw new InvalidOperationException("Benchmark changed unrelated cells");
        editor.WriteMessage($"\nTABLE_PERF_PASS: 80 tables x 12 rows x 6 cols; previous_layout_median_ms={before[1]:F2}; one_cell_median_ms={after[1]:F2}\n");
    }
}
