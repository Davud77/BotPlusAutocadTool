// ============================================================================
// OOXGN - Copyright (c) 2026. All rights reserved.
// Author: OOXGN | Project: BotPlusAutocadTool
// ============================================================================

// File: Services/PdfPostProcessingService.cs

using Autodesk.AutoCAD.EditorInput;
using System;
using System.Collections.Generic;
using System.IO;
using BotPlusAutocadTool.Models;

// aliases (должны быть тут, до namespace)
using IOPath = System.IO.Path;
using ITextPdfReader = iText.Kernel.Pdf.PdfReader;
using ITextPdfWriter = iText.Kernel.Pdf.PdfWriter;
using ITextPdfDocument = iText.Kernel.Pdf.PdfDocument;
using ITextPdfPage = iText.Kernel.Pdf.PdfPage;
using ITextRect = iText.Kernel.Geom.Rectangle;
using ITextPdfLinkAnnotation = iText.Kernel.Pdf.Annot.PdfLinkAnnotation;
using ITextPdfAction = iText.Kernel.Pdf.Action.PdfAction;
using ITextPdfArray = iText.Kernel.Pdf.PdfArray;

namespace BotPlusAutocadTool.Services
{
    /// <summary>
    /// Постобработка многостраничного PDF (iText7):
    /// - Размер видимой и физической страницы по окну рамки, от начала печати (0,0).
    /// - Добавление гиперссылки на весь лист.
    /// </summary>
    public static class PdfPostProcessingService
    {
        private const string HomePageUrl = "https://weam.ru/";

        /// <summary>
        /// Важно: itemsInDsdOrder должен быть в том же порядке, что и страницы в итоговом PDF.
        /// </summary>
        public static bool ProcessPdf(
            string pdfPath,
            IReadOnlyList<BatchPlotItem> itemsInDsdOrder,
            Editor ed)
        {
            if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
            {
                ed?.WriteMessage($"\n[BotPlus AT] PDF файл не найден: {pdfPath}\n");
                return false;
            }

            if (itemsInDsdOrder == null || itemsInDsdOrder.Count == 0)
            {
                ed?.WriteMessage("\n[BotPlus AT] Нет листов для постобработки.\n");
                return false;
            }
            string tempPdfPath = pdfPath + ".tmp";

            try
            {
                using (var reader = new ITextPdfReader(pdfPath))
                using (var writer = new ITextPdfWriter(tempPdfPath))
                using (var pdfDoc = new ITextPdfDocument(reader, writer))
                {
                    int totalPages = pdfDoc.GetNumberOfPages();
                    if (totalPages != itemsInDsdOrder.Count)
                        throw new InvalidDataException($"Число страниц PDF ({totalPages}) не совпало с числом листов ({itemsInDsdOrder.Count}).");
                    int pagesToProcess = totalPages;

                    for (int pageIdx = 1; pageIdx <= pagesToProcess; pageIdx++)
                    {
                        BatchPlotItem item = itemsInDsdOrder[pageIdx - 1];
                        ITextPdfPage page = pdfDoc.GetPage(pageIdx);

                        if (item != null && (item.PlotWidthMm > 0 || item.FormatDefinition != null))
                        {
                            TryCropPageToFormat(page, item);
                        }

                        // 2) Гиперссылка на весь лист
                        AddFullPageLink(page, HomePageUrl);
                    }
                }

                // заменить исходный файл на обработанный
                File.Move(tempPdfPath, pdfPath, overwrite: true);

                ed?.WriteMessage($"\n[BotPlus AT] ✓ PDF постобработан: {IOPath.GetFileName(pdfPath)}\n");
                return true;
            }
            catch (System.Exception ex)
            {
                ed?.WriteMessage($"\n[BotPlus AT] Ошибка постобработки PDF: {ex.Message}\n");
                try { if (File.Exists(tempPdfPath)) File.Delete(tempPdfPath); } catch { }
                return false;
            }
        }

        /// <summary>
        /// Обрезка страницы PDF по размерам FormatDefinition (мм) от нижнего левого угла.
        /// </summary>
        private static void TryCropPageToFormat(ITextPdfPage page, BatchPlotItem item)
        {
            // мм -> pt (1 mm = 2.834645669 pt)
            bool hasFrameSize = item.PlotWidthMm > 0 && item.PlotHeightMm > 0;
            double widthPt = (hasFrameSize ? item.PlotWidthMm : item.FormatDefinition.Width) * 72.0 / 25.4;
            double heightPt = (hasFrameSize ? item.PlotHeightMm : item.FormatDefinition.Height) * 72.0 / 25.4;
            if (!hasFrameSize && item.Format?.EndsWith(" v", StringComparison.OrdinalIgnoreCase) == true)
                (widthPt, heightPt) = (heightPt, widthPt);

            ITextRect current = page.GetCropBox();
            if (current == null || current.GetWidth() <= 0 || current.GetHeight() <= 0)
                current = page.GetMediaBox();

            float llx = current.GetLeft();
            float lly = current.GetBottom();

            float urx = (float)(llx + widthPt);
            float ury = (float)(lly + heightPt);

            // не выходить за границы текущего бокса
            urx = Math.Min(urx, current.GetRight());
            ury = Math.Min(ury, current.GetTop());

            if (urx <= llx || ury <= lly)
                return;

            if (hasFrameSize && (urx - llx < widthPt - 1 || ury - lly < heightPt - 1))
                throw new InvalidDataException($"Страница PDF меньше рамки листа {item.SheetNumber}.");
            // Native PDFs round paper bounds to points. Keep the exact frame size
            // when the discrepancy is only that rounding, rather than shortening it.
            var bounds = new ITextRect(llx, lly, hasFrameSize ? (float)widthPt : urx - llx,
                hasFrameSize ? (float)heightPt : ury - lly);
            page.SetMediaBox(bounds);
            page.SetCropBox(bounds);
            page.SetTrimBox(bounds);
            page.SetBleedBox(bounds);
            page.SetArtBox(bounds);
        }

        /// <summary>
        /// Добавляет гиперссылку на всю видимую область (CropBox/MediaBox).
        /// </summary>
        private static void AddFullPageLink(ITextPdfPage page, string linkUrl)
        {
            ITextRect box = page.GetCropBox();
            if (box == null || box.GetWidth() <= 0 || box.GetHeight() <= 0)
                box = page.GetMediaBox();

            var linkAnnot = new ITextPdfLinkAnnotation(
                new ITextRect(box.GetLeft(), box.GetBottom(), box.GetWidth(), box.GetHeight())
            );

            linkAnnot.SetAction(ITextPdfAction.CreateURI(linkUrl));

            // скрыть рамку
            linkAnnot.SetBorder(new ITextPdfArray(new int[] { 0, 0, 0 }));

            page.AddAnnotation(linkAnnot);
        }

        public static void CombinePages(IReadOnlyList<string> sourcePaths, string destination)
        {
            string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var target = new ITextPdfDocument(new ITextPdfWriter(temporary)))
                    foreach (var path in sourcePaths)
                    {
                        using var source = new ITextPdfDocument(new ITextPdfReader(path));
                        if (source.GetNumberOfPages() != 1) throw new InvalidDataException("Ожидался один лист в промежуточном PDF: " + path);
                        source.CopyPagesTo(1, 1, target);
                    }
                File.Move(temporary, destination, overwrite: true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        public static string[] DeliverPdf(string sourcePath, string outputPath,
            IReadOnlyList<BatchPlotItem> items, bool separateSheets, bool keepExisting = false)
        {
            if (!separateSheets)
            {
                outputPath = Path.GetFullPath(outputPath);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                var temporary = outputPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try { File.Copy(sourcePath, temporary); return new[] { PdfOutputPaths.CompleteDelivery(temporary, outputPath, keepExisting) }; }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
            Directory.CreateDirectory(outputPath);
            var delivered = new List<string>();
            using (var source = new ITextPdfDocument(new ITextPdfReader(sourcePath)))
            {
                if (source.GetNumberOfPages() != items.Count) throw new InvalidDataException("Неверное количество листов PDF.");
                for (int i = 0; i < items.Count; i++)
                {
                    string destination = Path.Combine(outputPath, PdfOutputPaths.SheetFileName(i, items[i].SheetNumber));
                    string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    try
                    {
                        using (var target = new ITextPdfDocument(new ITextPdfWriter(temporary)))
                            source.CopyPagesTo(i + 1, i + 1, target);
                        delivered.Add(PdfOutputPaths.CompleteDelivery(temporary, destination, keepExisting));
                    }
                    finally { if (File.Exists(temporary)) File.Delete(temporary); }
                }
            }
            return delivered.ToArray();
        }
    }
}
