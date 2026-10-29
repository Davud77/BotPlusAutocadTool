using System;
using System.IO;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.PlottingServices;
using Autodesk.AutoCAD.Runtime;
using BotPlusAutocadTool.Models;
using BotPlusAutocadTool.Services;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

public sealed class Plot141Checks
{
    [CommandMethod("BOTPLUS_PLOT141TEST")]
    public void Run()
    {
        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        using var originalView = doc.Editor.GetCurrentView();
        var oldBackground = AcadApp.GetSystemVariable("BACKGROUNDPLOT");
        var oldOverride = AcadApp.GetSystemVariable("PLOTTRANSPARENCYOVERRIDE");
        var oldUcs = doc.Editor.CurrentUserCoordinateSystem;
        try
        {
            string folder = Path.Combine(Path.GetDirectoryName(doc.Name), "plot141-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var item = Frame(doc.Database);
            foreach (bool transparency in new[] { false, true })
            {
                double twist = transparency ? Math.PI / 2 : 0;
                using (var view = doc.Editor.GetCurrentView())
                {
                    view.ViewDirection = Vector3d.ZAxis;
                    view.Target = new Point3d(10000, 20000, 0);
                    view.ViewTwist = twist;
                    view.CenterPoint = Point2d.Origin;
                    view.Width = 2000; view.Height = 1500;
                    doc.Editor.SetCurrentView(view);
                }
                doc.Editor.CurrentUserCoordinateSystem = Matrix3d.Displacement(new Vector3d(500, 700, 0)) * Matrix3d.Rotation(Math.PI/6, Vector3d.ZAxis, Point3d.Origin);
                string stem = transparency ? "on-twist90" : "off-top";
                string dsd = Path.Combine(folder, stem + ".dsd"), pdf = Path.Combine(folder, stem + ".pdf");
                // Cover both a new setup copied from an imperial Model layout and a reused
                // imperial setup. Media selection must not leave the final scale in inches.
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var model = (Layout)tr.GetObject(LayoutManager.Current.GetLayoutId("Model"), OpenMode.ForWrite);
                    PlotSettingsValidator.Current.SetPlotPaperUnits(model, PlotPaperUnit.Inches);
                    var dict = (DBDictionary)tr.GetObject(doc.Database.PlotSettingsDictionaryId, OpenMode.ForRead);
                    if (dict.Contains("WEAM_Sheet1_A2"))
                        PlotSettingsValidator.Current.SetPlotPaperUnits(
                            (PlotSettings)tr.GetObject(dict.GetAt("WEAM_Sheet1_A2"), OpenMode.ForWrite), PlotPaperUnit.Inches);
                    tr.Commit();
                }
                if (!BlockExportStage1Service.Run(doc, dsd, new List<BatchPlotItem> { item }, pdf, transparency)) throw new InvalidOperationException("Preparation failed");
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                using (var data = new DsdData())
                {
                    data.ReadDsd(dsd);
                    using var entries = data.GetDsdEntryCollection();
                    var dict = (DBDictionary)tr.GetObject(doc.Database.PlotSettingsDictionaryId, OpenMode.ForRead);
                    var ps = (PlotSettings)tr.GetObject(dict.GetAt(entries[0].Nps), OpenMode.ForRead);
                    if (ps.PlotPaperUnits != PlotPaperUnit.Millimeters || ps.UseStandardScale ||
                        Math.Abs(ps.CustomPrintScale.Denominator / ps.CustomPrintScale.Numerator - item.PlotScale) > 1e-6)
                        throw new InvalidOperationException("Imperial source setup changed the millimeter print scale");
                    var expectedMin = transparency ? new Point2d(-1420,1000) : new Point2d(1000,1000);
                    var expectedMax = transparency ? new Point2d(-1000,1594) : new Point2d(1594,1420);
                    if (ps.PlotWindowArea.MinPoint.GetDistanceTo(expectedMin) > .001 || ps.PlotWindowArea.MaxPoint.GetDistanceTo(expectedMax) > .001)
                        throw new InvalidOperationException("Shifted A2 window: " + ps.PlotWindowArea);
                    if (ps.PlotTransparency != transparency) throw new InvalidOperationException("Transparency checkbox not applied to page setup");
                }
                doc.Editor.Command("_.QSAVE");
                AcadApp.SetSystemVariable("BACKGROUNDPLOT", 0);
                AcadApp.SetSystemVariable("PLOTTRANSPARENCYOVERRIDE", transparency ? 2 : 0);
                using (var data = new DsdData())
                {
                    data.ReadDsd(dsd); data.SheetType = SheetType.MultiPdf;
                    AcadApp.Publisher.PublishExecute(data, PlotConfigManager.SetCurrentConfig(PdfPlotterService.PlotterName));
                }
                if (!PdfPostProcessingService.ProcessPdf(pdf, new[] { item }, doc.Editor)) throw new InvalidOperationException("PDF processing failed");
                File.AppendAllText(Path.Combine(folder, "frames.tsv"), stem + "\t" + item.PlotWidthMm + "\t" + item.PlotHeightMm + "\n");
            }
            var saved = SettingsService.Settings.PlotTransparency;
            using (var vm = new BotPlusAutocadTool.ViewModels.BatchPlotViewModel()) vm.PlotTransparency = !saved;
            using (var vm = new BotPlusAutocadTool.ViewModels.BatchPlotViewModel())
            {
                if (vm.PlotTransparency != !saved) throw new InvalidOperationException("Checkbox was not restored");
                vm.PlotTransparency = saved;
            }
            WindowChecks.Run();
            doc.Editor.WriteMessage("\nBOTPLUS_PLOT141_PASS: new/reused imperial page setups, A2 window at translated target, 90-degree view twist, rotated UCS, transparency off/on, native PDFs, settings and window; " + folder + "\n");
        }
        catch (System.Exception ex) { doc.Editor.WriteMessage("\nBOTPLUS_PLOT141_FAIL: " + ex + "\n"); }
        finally
        {
            doc.Editor.CurrentUserCoordinateSystem = oldUcs;
            doc.Editor.SetCurrentView(originalView);
            AcadApp.SetSystemVariable("BACKGROUNDPLOT", oldBackground);
            AcadApp.SetSystemVariable("PLOTTRANSPARENCYOVERRIDE", oldOverride);
        }
    }

    public static BatchPlotItem Frame(Database db)
    {
        using var tr = db.TransactionManager.StartTransaction();
        var table = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForWrite);
        var definition = new BlockTableRecord { Name = "BOTPLUS_A2_141_" + Guid.NewGuid().ToString("N") };
        table.Add(definition); tr.AddNewlyCreatedDBObject(definition, true);
        var outline = new Polyline();
        foreach (var point in new[] { new Point2d(0,0),new Point2d(594,0),new Point2d(594,420),new Point2d(0,420) })
            outline.AddVertexAt(outline.NumberOfVertices, point, 0, 0, 0);
        outline.Closed = true;
        definition.AppendEntity(outline); tr.AddNewlyCreatedDBObject(outline,true);
        var solid = new Solid(new Point3d(200,150,0), new Point3d(300,150,0), new Point3d(200,250,0), new Point3d(300,250,0))
        { Color = Color.FromRgb(0,0,0), Transparency = new Transparency((byte)127) };
        definition.AppendEntity(solid); tr.AddNewlyCreatedDBObject(solid,true);
        var model = (BlockTableRecord)tr.GetObject(table[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
        var block = new BlockReference(new Point3d(11000,21000,0), definition.ObjectId);
        model.AppendEntity(block); tr.AddNewlyCreatedDBObject(block,true);
        var item = new BatchPlotItem { BlockId = block.ObjectId, Extents = block.GeometricExtents, PlotScale = 1,
            Format = "A2", FormatDefinition = new PaperFormatDefinition("A2",594,420), Name = "A2", SheetNumber = "141", IsSelected = true };
        tr.Commit(); return item;
    }
}
