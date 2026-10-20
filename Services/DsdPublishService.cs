using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.PlottingServices;
using BotPlusAutocadTool.Models;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace BotPlusAutocadTool.Services
{
    public static class DsdPublishService
    {
        private static PublishJob _job;
        public static bool IsBusy => _job != null;

        public static bool Publish(string dsdPath, string pdfPath, string outputPath,
            IReadOnlyList<BatchPlotItem> items, bool separateSheets, bool background, bool openFolder = false, bool openFile = false)
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null || IsBusy || !File.Exists(dsdPath)) return false;
            var job = new PublishJob(doc, pdfPath, outputPath, items, separateSheets, background, openFolder, openFile);
            _job = job;
            try
            {
                job.Subscribe();
                var path = dsdPath.Replace("\\", "/").Replace("\"", "\\\"");
                // Background publication opens the DWG from disk; persist the generated page setups first.
                doc.SendStringToExecute($"(progn (setvar \"FILEDIA\" 0) (setvar \"BACKGROUNDPLOT\" {(background ? 3 : 0)}) (command \"_.QSAVE\") (command \"_.-PUBLISH\" \"{path}\") (princ)) ", true, false, false);
                return true;
            }
            catch { job.Dispose(); throw; }
        }

        public static void Shutdown() => _job?.Dispose();

        private sealed class PublishJob : IDisposable
        {
            private readonly Document _doc;
            private readonly string _pdfPath, _outputPath;
            private readonly IReadOnlyList<BatchPlotItem> _items;
            private readonly bool _separateSheets;
            private readonly bool _openFolder, _openFile;
            private readonly object _oldFileDia, _oldBackgroundPlot;
            private readonly int _ownedBackgroundPlot;
            private readonly DateTime _created = DateTime.UtcNow;
            private readonly PublishFileReadiness _pdfReadiness = new();
            private DateTime _finishedAt, _nextPollUtc;
            private bool _started, _finished, _failed, _disposed;

            public PublishJob(Document doc, string pdfPath, string outputPath,
                IReadOnlyList<BatchPlotItem> items, bool separateSheets, bool background, bool openFolder, bool openFile)
            {
                _doc = doc; _pdfPath = pdfPath; _outputPath = outputPath;
                // The user can change the print list while background publishing runs.
                _items = items.Select(item => new BatchPlotItem
                {
                    Name = item.Name,
                    SheetNumber = item.SheetNumber,
                    Format = item.Format,
                    FormatDefinition = item.FormatDefinition
                }).ToArray();
                _separateSheets = separateSheets;
                _openFolder = openFolder; _openFile = openFile;
                _ownedBackgroundPlot = background ? 3 : 0;
                _oldFileDia = AcadApp.GetSystemVariable("FILEDIA");
                _oldBackgroundPlot = AcadApp.GetSystemVariable("BACKGROUNDPLOT");
            }

            public void Subscribe()
            {
                _doc.CommandWillStart += OnCommandStart;
                _doc.CommandEnded += OnCommandEnd;
                _doc.CommandCancelled += OnCommandFailed;
                _doc.CommandFailed += OnCommandFailed;
                AcadApp.Publisher.EndPublish += OnPublishEnd;
                AcadApp.Publisher.CancelledOrFailedPublishing += OnPublishFailed;
                AcadApp.DocumentManager.DocumentToBeDestroyed += OnDocumentClosing;
                AcadApp.Idle += OnIdle;
            }

            private static bool IsPublish(string command) =>
                string.Equals(command?.TrimStart('_', '.', '-'), "PUBLISH", StringComparison.OrdinalIgnoreCase);

            private void OnCommandStart(object sender, CommandEventArgs e)
            {
                if (IsPublish(e.GlobalCommandName)) _started = true;
            }
            private void OnCommandFailed(object sender, CommandEventArgs e)
            {
                if (IsPublish(e.GlobalCommandName)) { _started = true; _failed = true; _finished = true; }
            }
            private void OnCommandEnd(object sender, CommandEventArgs e)
            {
                // PDF publication does not consistently raise Publisher.EndPublish.
                // In background mode the command can end before the worker finishes.
                if (IsPublish(e.GlobalCommandName) && _ownedBackgroundPlot == 0)
                {
                    _started = true; _finished = true; _finishedAt = DateTime.UtcNow;
                }
            }
            private void OnPublishEnd(object sender, EventArgs e)
            {
                if (_started) { _finished = true; _finishedAt = DateTime.UtcNow; }
            }
            private void OnPublishFailed(object sender, EventArgs e)
            {
                if (_started) { _failed = true; _finished = true; }
            }
            private void OnDocumentClosing(object sender, DocumentCollectionEventArgs e)
            {
                if (e.Document == _doc) Dispose();
            }

            private void OnIdle(object sender, EventArgs e)
            {
                try { ProcessIdle(); }
                catch (System.Exception ex)
                {
                    Infrastructure.PluginLog.Error("Publish callback", ex);
                    Dispose();
                }
            }

            private void ProcessIdle()
            {
                if (_disposed) return;
                var now = DateTime.UtcNow;
                if (now < _nextPollUtc) return;
                _nextPollUtc = now.AddMilliseconds(500);
                if (PlotFactory.ProcessPlotState != ProcessPlotState.NotPlotting || !_doc.Editor.IsQuiescent)
                {
                    _pdfReadiness.Reset();
                    return;
                }
                bool ready = !_failed && IsPdfReady(now);
                // The job has a unique output path. A stable, unlocked PDF after the
                // host returns to idle is also completion evidence when events are absent.
                if (!_finished && ready) { _started = true; _finished = true; _finishedAt = now; }
                if (!_finished)
                {
                    if (!_started && DateTime.UtcNow - _created > TimeSpan.FromMinutes(2))
                    {
                        _doc.Editor.WriteMessage("\n[BotPlus] Публикация не стартовала.\n");
                        Dispose();
                    }
                    return;
                }
                if (!_failed && !ready && now - _finishedAt < TimeSpan.FromSeconds(30)) return;
                try
                {
                    if (_failed) { _doc.Editor.WriteMessage("\n[BotPlus] Публикация отменена или завершилась с ошибкой.\n"); return; }
                    if (!File.Exists(_pdfPath)) throw new IOException("Публикация завершилась без ожидаемого PDF.");
                    if (!ready) throw new IOException("PDF ещё записывается или недоступен для обработки. Временный файл сохранён: " + _pdfPath);
                    using (var stream = File.Open(_pdfPath, FileMode.Open, FileAccess.Read, FileShare.None)) { }
                    if (!PdfPostProcessingService.ProcessPdf(_pdfPath, _items, _doc.Editor))
                        throw new IOException("Не удалось обработать PDF. Исходный PDF сохранён: " + _pdfPath);
                    PdfPostProcessingService.DeliverPdf(_pdfPath, _outputPath, _items, _separateSheets);
                    _doc.Editor.WriteMessage($"\n[BotPlus] PDF готов: {_outputPath}\n");
                    PublishedOutputService.Open(_outputPath, _separateSheets, _openFolder, _openFile, _items.FirstOrDefault()?.SheetNumber,
                        message => _doc.Editor.WriteMessage("\n[BotPlus] " + message + "\n"));
                }
                catch (System.Exception ex)
                {
                    Infrastructure.PluginLog.Error("Postprocess published PDF", ex);
                    _doc.Editor.WriteMessage($"\n[BotPlus] {ex.Message}\n");
                }
                finally { Dispose(); }
            }

            private bool IsPdfReady(DateTime now)
            {
                try
                {
                    using var stream = File.Open(_pdfPath, FileMode.Open, FileAccess.Read, FileShare.None);
                    return _pdfReadiness.Observe(now, stream.Length, File.GetLastWriteTimeUtc(_pdfPath), true);
                }
                catch (IOException) { _pdfReadiness.Reset(); return false; }
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _doc.CommandWillStart -= OnCommandStart;
                _doc.CommandEnded -= OnCommandEnd;
                _doc.CommandCancelled -= OnCommandFailed;
                _doc.CommandFailed -= OnCommandFailed;
                AcadApp.Publisher.EndPublish -= OnPublishEnd;
                AcadApp.Publisher.CancelledOrFailedPublishing -= OnPublishFailed;
                AcadApp.DocumentManager.DocumentToBeDestroyed -= OnDocumentClosing;
                AcadApp.Idle -= OnIdle;
                try { if (Convert.ToInt32(AcadApp.GetSystemVariable("FILEDIA")) == 0) AcadApp.SetSystemVariable("FILEDIA", _oldFileDia); } catch { }
                try { if (Convert.ToInt32(AcadApp.GetSystemVariable("BACKGROUNDPLOT")) == _ownedBackgroundPlot) AcadApp.SetSystemVariable("BACKGROUNDPLOT", _oldBackgroundPlot); } catch { }
                if (ReferenceEquals(_job, this)) _job = null;
            }
        }
    }
}
