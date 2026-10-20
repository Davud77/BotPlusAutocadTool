using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using BotPlusAutocadTool.Models;
using BotPlusAutocadTool.Services;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

public sealed class PublishCheck
{
    private static string _directory, _report;
    private static Document _document;
    private static List<BatchPlotItem> _items;
    private static int _stage, _oldFileDia, _oldBackground;
    private static DateTime _deadline;
    private static string Pdf(int stage) => Path.Combine(_directory, "temporary", "Result-" + stage + ".pdf");
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
            _oldFileDia = Convert.ToInt32(AcadApp.GetSystemVariable("FILEDIA"));
            _oldBackground = Convert.ToInt32(AcadApp.GetSystemVariable("BACKGROUNDPLOT"));
            var definition = FormatInsertService.EnsureDefinition(_document.Database);
            var first = FormatInsertService.Insert(_document.Database, definition, new Point3d(1000, 1000, 0));
            var second = FormatInsertService.Insert(_document.Database, definition, new Point3d(2000, 1000, 0));
            _items = new BatchPlotService { PlotScale = 1 }.GetFormatBlocks().Where(i => i.BlockId == first || i.BlockId == second).ToList();
            if (_items.Count != 2) throw new InvalidOperationException("Two test frames were not found");
            foreach (var item in _items) item.IsSelected = true;
            Directory.CreateDirectory(Path.Combine(_directory, "temporary"));
            for (int stage = 0; stage < 2; stage++)
                if (!BlockExportStage1Service.Run(_document, Dsd(stage), _items, Pdf(stage))) throw new InvalidOperationException("DSD creation failed");
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
        _deadline = DateTime.UtcNow.AddMinutes(2);
        if (!DsdPublishService.Publish(Dsd(_stage), Pdf(_stage), Output(_stage), _items, _stage == 1, false))
            throw new InvalidOperationException("Queue did not start");
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
                if (!File.Exists(Output(0)) || new FileInfo(Output(0)).Length == 0) throw new InvalidOperationException("Binder was not delivered next to DWG");
            }
            else if (!Directory.Exists(Output(1)) || Directory.GetFiles(Output(1), "*.pdf").Length != 2)
                throw new InvalidOperationException("Two individual sheets were not delivered to custom folder");
            if (Convert.ToInt32(AcadApp.GetSystemVariable("FILEDIA")) != _oldFileDia || Convert.ToInt32(AcadApp.GetSystemVariable("BACKGROUNDPLOT")) != _oldBackground)
                throw new InvalidOperationException("Publication did not restore system variables");
            Log("PASS: stage " + _stage + ", PDF postprocessed/delivered, variables restored");
            if (++_stage < 2) StartStage();
            else
            {
                Stop();
                Log("BOTPLUS_PIPELINE_PASS: real queued -PUBLISH; foreground binder, individual sheets, missing-event fallback");
                _document.Editor.WriteMessage("\nBOTPLUS_PIPELINE_PASS: " + _directory + "\n");
            }
        }
        catch (System.Exception ex) { Fail(ex); }
    }
    private static void Stop()
    {
        AcadApp.Idle -= Poll;
        if (_document != null) _document.CommandWillStart -= TraceCommand;
        AcadApp.Publisher.EndPublish -= TracePublish;
        DsdPublishService.Shutdown();
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
