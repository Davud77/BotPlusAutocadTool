using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.PlottingServices;
using BotPlusAutocadTool.Models;
using BotPlusAutocadTool.Services;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

public sealed class PublishCheck
{
    private static string _directory, _report;
    private static Document _document;
    private static List<BatchPlotItem> _items;
    private static int _stage, _oldFileDia, _oldBackground, _stageCount;
    private static bool _transparency141;
    private static int _stageTransparencyOriginal;
    private static object _initialTransparencyOverride;
    private static DateTime _deadline;
    private static string Pdf(int stage) => Path.Combine(_directory, "temporary", "stage-" + stage, "Result.pdf");
    private static string Dsd(int stage) => Path.Combine(_directory, "publish-" + stage + ".dsd");
    private static string Output(int stage) => Path.Combine(_directory, stage == 0 ? "drawing_Binder.pdf" : "Выбранная папка PDF");
    private static void Log(string text) => File.AppendAllText(_report, DateTime.UtcNow.ToString("O") + " " + text + "\n");

    [CommandMethod("BOTPLUS_PUBLISHPIPELINETEST")]
    public void Run()
    {
        try
        {
            _document = AcadApp.DocumentManager.MdiActiveDocument;
            _directory = Path.GetDirectoryName(_document.Database.Filename);
            if (!_directory.Contains("artifacts\\validation\\publish-desktop-", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Open only the dedicated copied fixture in artifacts/validation/publish-desktop-*.");
            _report = Path.Combine(_directory, "pipeline.log");
            Log("CANDIDATE: " + typeof(DsdPublishService).Assembly.Location);
            Log("PDF API: " + string.Join("; ", typeof(PdfPostProcessingService).GetMethods().Where(m => m.Name == "DeliverPdf").Select(m => m.ToString())));
            _oldFileDia = Convert.ToInt32(AcadApp.GetSystemVariable("FILEDIA"));
            _oldBackground = Convert.ToInt32(AcadApp.GetSystemVariable("BACKGROUNDPLOT"));
            _initialTransparencyOverride = AcadApp.GetSystemVariable("PLOTTRANSPARENCYOVERRIDE");
            bool matrix = Environment.GetEnvironmentVariable("BOTPLUS_GOST_DRAWING") == "matrix";
            _transparency141 = Environment.GetEnvironmentVariable("BOTPLUS_GOST_DRAWING") == "transparency";
            _stageCount = matrix ? 1 : 2;
            if (_transparency141)
            {
                _items = new List<BatchPlotItem> { GostPaperFixture.BuildTransparencyA2(_document.Database) };
                using var view = _document.Editor.GetCurrentView();
                view.ViewDirection = Vector3d.ZAxis; view.Target = new Point3d(10000,20000,0);
                view.ViewTwist = Math.PI/2; view.CenterPoint = Point2d.Origin; view.Width = 2000; view.Height = 1500;
                _document.Editor.SetCurrentView(view);
            }
            else if (matrix) _items = GostPaperFixture.Build(_document.Database);
            else if (int.TryParse(Environment.GetEnvironmentVariable("BOTPLUS_TEST_UNIT_CODE"), out int unitCode) && unitCode > 0)
            {
                var db = _document.Database;
                var all = GostPaperFixture.Build(db);
                double factor = PlotUnits.Get(unitCode).MillimetersPerUnit;
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    foreach (var item in all)
                    {
                        var block = (BlockReference)tr.GetObject(item.BlockId, OpenMode.ForWrite);
                        block.TransformBy(Matrix3d.Scaling(1 / factor, Point3d.Origin));
                        item.Extents = block.GeometricExtents;
                    }
                    var model = (Layout)tr.GetObject(LayoutManager.Current.GetLayoutId("Model"), OpenMode.ForWrite);
                    PlotSettingsValidator.Current.SetPlotPaperUnits(model, PlotPaperUnit.Inches);
                    tr.Commit();
                }
                db.Insunits = (UnitsValue)unitCode;
                db.Measurement = (MeasurementValue)(unitCode == 1 ? 1 : 0);
                _items = all.Where(i => i.FormatDefinition.Name == "A2" || i.FormatDefinition.Name == "A0x3").ToList();
            }
            else if (Environment.GetEnvironmentVariable("BOTPLUS_GOST_DRAWING") == "1")
            {
                _items = new BatchPlotService { PlotScale = 1, SearchOrder = PlotSearchOrder.LeftToRight }.GetFormatBlocks()
                    .Where(i => i.FormatDefinition != null).ToList();
                if (_items.Count != 29) throw new InvalidOperationException("29 reference GOST frames expected");
            }
            else
            {
            var definition = FormatInsertService.EnsureDefinition(_document.Database);
            var first = FormatInsertService.Insert(_document.Database, definition, new Point3d(1000, 1000, 0));
            var second = FormatInsertService.Insert(_document.Database, definition, new Point3d(2000, 1000, 0));
            _items = new BatchPlotService { PlotScale = 1 }.GetFormatBlocks().Where(i => i.BlockId == first || i.BlockId == second).ToList();
            if (_items.Count != 2) throw new InvalidOperationException("Two test frames were not found");
            }
            foreach (var item in _items) item.IsSelected = true;
            Directory.CreateDirectory(Path.Combine(_directory, "temporary"));
            for (int stage = 0; stage < _stageCount; stage++)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Pdf(stage)));
                if (!BlockExportStage1Service.Run(_document, Dsd(stage), _items, Pdf(stage), _transparency141 && stage == 1)) throw new InvalidOperationException("DSD creation failed");
            }
            File.WriteAllLines(Path.Combine(_directory, "frames.tsv"), _items.Select(i => string.Join("\t", i.SheetNumber, i.Format,
                i.PlotWidthMm.ToString(System.Globalization.CultureInfo.InvariantCulture), i.PlotHeightMm.ToString(System.Globalization.CultureInfo.InvariantCulture), i.PlotScale)));
            File.WriteAllText(Output(0), "preserve this existing file");
            _document.Editor.Command("_.QSAVE");
            _document.CommandWillStart += TraceCommand;
            AcadApp.Publisher.EndPublish += TracePublish;
            _stage = 0;
            StartStage();
            AcadApp.Idle += Poll;
        }
        catch (System.Exception ex) { Fail(ex); }
    }
    private static void TraceCommand(object sender, CommandEventArgs e) => Log("COMMAND: " + e.GlobalCommandName);
    private static void TracePublish(object sender, EventArgs e) => Log("Publisher.EndPublish received");
    private static void StartStage()
    {
        _deadline = DateTime.UtcNow.AddMinutes(8);
        if (_transparency141) AcadApp.SetSystemVariable("PLOTTRANSPARENCYOVERRIDE", _stage == 0 ? 2 : 0);
        _stageTransparencyOriginal = Convert.ToInt32(AcadApp.GetSystemVariable("PLOTTRANSPARENCYOVERRIDE"));
        if (!DsdPublishService.Publish(Dsd(_stage), Pdf(_stage), Output(_stage), _items, _stage == 1, _stage == 1, keepExisting: _stage == 0, plotTransparency: _transparency141 && _stage == 1))
            throw new InvalidOperationException("Queue did not start");
        var activeJob = typeof(DsdPublishService).GetField("_job", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        var snapshot = (IReadOnlyList<BatchPlotItem>)activeJob.GetType().GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(activeJob);
        for (int i = 0; i < snapshot.Count; i++)
            if (ReferenceEquals(snapshot[i], _items[i]) || snapshot[i].PlotWidthMm != _items[i].PlotWidthMm || snapshot[i].PlotHeightMm != _items[i].PlotHeightMm ||
                snapshot[i].MillimetersPerDrawingUnit != _items[i].MillimetersPerDrawingUnit || snapshot[i].DrawingUnitsOverride != _items[i].DrawingUnitsOverride)
                throw new InvalidOperationException("Publication snapshot lost frame dimensions");
        if (_stage == 1)
        {
            // Fault injection: output must arrive even when both completion notifications are lost.
            var job = typeof(DsdPublishService).GetField("_job", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            RemoveHandler(_document, "CommandEnded", job, "OnCommandEnd");
            RemoveHandler(AcadApp.Publisher, "EndPublish", job, "OnPublishEnd");
            Log("Completion notifications deliberately removed for file-readiness fallback");
        }
        Log("START: " + _stage + "; final=" + Output(_stage));
    }
    private static void RemoveHandler(object source, string eventName, object target, string methodName)
    {
        var ev = source.GetType().GetEvent(eventName);
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        ev.RemoveEventHandler(source, Delegate.CreateDelegate(ev.EventHandlerType, target, method));
    }
    private static void Poll(object sender, EventArgs e)
    {
        try
        {
            if (DateTime.UtcNow > _deadline) throw new TimeoutException("Publication pipeline timed out");
            if (DsdPublishService.IsBusy) return;
            if (_stage == 0)
            {
                string indexed = Path.Combine(_directory, "drawing_Binder_2.pdf");
                if (!File.Exists(indexed) || new FileInfo(indexed).Length == 0) throw new InvalidOperationException("Indexed binder was not delivered next to DWG");
                if (File.ReadAllText(Output(0)) != "preserve this existing file") throw new InvalidOperationException("Existing binder was overwritten");
            }
            else if (!Directory.Exists(Output(1)) || Directory.GetFiles(Output(1), "*.pdf").Length != _items.Count)
                throw new InvalidOperationException("Individual sheets were not delivered to custom folder");
            if (Convert.ToInt32(AcadApp.GetSystemVariable("FILEDIA")) != _oldFileDia || Convert.ToInt32(AcadApp.GetSystemVariable("BACKGROUNDPLOT")) != _oldBackground)
                throw new InvalidOperationException("Publication did not restore system variables");
            if (Convert.ToInt32(AcadApp.GetSystemVariable("PLOTTRANSPARENCYOVERRIDE")) != _stageTransparencyOriginal)
                throw new InvalidOperationException("Transparency override not restored");
            Log("PASS: stage " + _stage + ", PDF postprocessed/delivered, variables restored, transparency=" + (_transparency141 && _stage == 1));
            if (++_stage < _stageCount) { Stop(); Log("BOTPLUS_PIPELINE_NEXT"); }
            else
            {
                Stop();
                Log(_stageCount == 1 ? "BOTPLUS_PIPELINE_PASS: real queued -PUBLISH; 100 GOST pages, indexed binder, snapshot geometry" :
                    "BOTPLUS_PIPELINE_PASS: real queued -PUBLISH; foreground binder, background individual sheets, missing-event fallback");
                _document.Editor.WriteMessage("\nBOTPLUS_PIPELINE_PASS: " + _directory + "\n");
            }
        }
        catch (System.Exception ex) { Fail(ex); }
    }
    [CommandMethod("BOTPLUS_PUBLISHPIPELINECONTINUE")]
    public void Continue()
    {
        try
        {
            _document.CommandWillStart += TraceCommand;
            AcadApp.Publisher.EndPublish += TracePublish;
            StartStage();
            AcadApp.Idle += Poll;
        }
        catch (System.Exception ex) { Fail(ex); }
    }
    private static void Stop()
    {
        AcadApp.Idle -= Poll;
        if (_document != null) _document.CommandWillStart -= TraceCommand;
        AcadApp.Publisher.EndPublish -= TracePublish;
        DsdPublishService.Shutdown();
        if (_initialTransparencyOverride != null) AcadApp.SetSystemVariable("PLOTTRANSPARENCYOVERRIDE", _initialTransparencyOverride);
    }
    private static void Fail(System.Exception ex)
    {
        Stop();
        if (_report != null) Log("BOTPLUS_PIPELINE_FAIL: " + ex);
        _document?.Editor.WriteMessage("\nBOTPLUS_PIPELINE_FAIL: " + ex.Message + "\n");
    }
}
namespace BotPlusAutocadTool.Infrastructure
{
    internal static class PluginLog
    {
        internal static void Error(string operation, System.Exception ex) => File.AppendAllText(Path.Combine(Path.GetTempPath(), "botplus-pipeline-errors.log"), operation + ": " + ex + "\n");
    }
}
