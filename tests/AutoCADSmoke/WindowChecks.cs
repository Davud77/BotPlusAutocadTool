using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BotPlusAutocadTool.Services;
using BotPlusAutocadTool.ViewModels;
using BotPlusAutocadTool.Views;

internal static class WindowChecks
{
    internal static void Run()
    {
        var factories = new Func<Window>[]
        {
            () => new AboutWindow(), () => new UpdateDownloadWindow(ReleaseVersion.FindUpdate("[{\"tag_name\":\"v1.4\",\"html_url\":\"https://github.com/Davud77/BotPlusAutocadTool/releases/tag/v1.4\"}]", new Version(1,3,3))),
            () => new AutoNumSettingsWindow(new AutoNumSettingsViewModel()),
            () => new CleanDwgWindow(), () => new CustomerOrderWindow(), () => new PlanPrepWindow(),
            () => new SelectSimilarWindow(), () => new StripTextWindow(), () => new SumLengthWindow { DataContext = new SumLengthViewModel() },
            () => new BatchPlotWindow(), () => new StampWindow(new StampPropsViewModel(new AutoCADService())),
            () => new SettingsWindow(), () => new AutoNumSettingsWindow(new AutoNumSettingsViewModel { ModeIndex = 3 }),
            () => new StampTransferWindow(new StampPropsViewModel(new AutoCADService()), new[] { new BotPlusAutocadTool.Models.StampTransferTarget { Name = "Первый чертёж.dwg", Location = @"C:\Проекты\Первый чертёж.dwg" } }, false)
        };
        var results = Path.Combine(Path.GetDirectoryName(typeof(WindowChecks).Assembly.Location), "results");
        Directory.CreateDirectory(results);
        foreach (var factory in factories)
        {
            var window = factory();
            try
            {
                var content = window.Content as FrameworkElement ?? throw new InvalidOperationException("Missing window content");
                if (window.ResizeMode != ResizeMode.CanResize) throw new InvalidOperationException("Window is not resizable: " + window.Title);
                var width = double.IsNaN(window.Width) ? 800 : window.Width;
                var height = double.IsNaN(window.Height) ? 700 : window.Height;
                content.Measure(new Size(width, height));
                content.Arrange(new Rect(0, 0, width, height));
                content.UpdateLayout();
                double printHeaderHeight = window is BatchPlotWindow initialPrinting ? ((FrameworkElement)initialPrinting.FindName("PrintToolbar")).ActualHeight : 0;
                double printListHeight = window is BatchPlotWindow initialList ? ((DataGrid)initialList.FindName("FramesGrid")).ActualHeight : 0;
                var bitmap = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(content);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                var suffix = window.DataContext is AutoNumSettingsViewModel vm && vm.ModeIndex == 3 ? "-tables" : "";
                using var stream = File.Create(Path.Combine(results, window.GetType().Name + suffix + ".png"));
                encoder.Save(stream);
                // Check the complete form remains reachable on a smaller working area.
                int smallWidth = window is BatchPlotWindow ? (int)window.MinWidth : 400;
                int smallHeight = window is BatchPlotWindow ? (int)window.MinHeight : 320;
                content.Measure(new Size(smallWidth, smallHeight)); content.Arrange(new Rect(0, 0, smallWidth, smallHeight)); content.UpdateLayout();
                var narrow = new RenderTargetBitmap(smallWidth, smallHeight, 96, 96, PixelFormats.Pbgra32); narrow.Render(content);
                var narrowEncoder = new PngBitmapEncoder(); narrowEncoder.Frames.Add(BitmapFrame.Create(narrow));
                using (var narrowFile = File.Create(Path.Combine(results, window.GetType().Name + suffix + "-narrow.png"))) narrowEncoder.Save(narrowFile);
                var scroll = (window.Content as DockPanel)?.Children.OfType<ScrollViewer>().SingleOrDefault();
                if (window is BatchPlotWindow printing)
                {
                    var header = (FrameworkElement)printing.FindName("PrintToolbar");
                    var grid = (DataGrid)printing.FindName("FramesGrid");
                    if (header is ScrollViewer || Math.Abs(header.ActualHeight - printHeaderHeight) > .1 || header.ActualHeight > 115)
                        throw new InvalidOperationException("Print toolbar scrolls, resizes or is not compact");
                    if (Math.Abs((printListHeight - grid.ActualHeight) - (height - smallHeight)) > 1)
                        throw new InvalidOperationException("Window height reduction was not confined to the sheet list");
                    if (grid.ActualHeight <= 30) throw new InvalidOperationException("Print list has no scrolling viewport");
                    var position = header.TranslatePoint(new Point(0, 0), content);
                    if (grid.Items.Count > 0) grid.ScrollIntoView(grid.Items[grid.Items.Count - 1]);
                    content.UpdateLayout();
                    if ((header.TranslatePoint(new Point(0, 0), content) - position).Length > 0.1)
                        throw new InvalidOperationException("Print toolbar moved with the sheet list");
                    continue;
                }
                if (window is StampTransferWindow)
                {
                    var table = ((DockPanel)window.Content).Children.OfType<DataGrid>().Single();
                    if (table.ActualHeight <= 0 || table.ActualWidth <= 0) throw new InvalidOperationException("Target document list is not reachable");
                    continue; // DataGrid owns its horizontal and vertical scrolling.
                }
                if (scroll == null || scroll.VerticalScrollBarVisibility != ScrollBarVisibility.Auto)
                    throw new InvalidOperationException("Missing automatic window scrolling: " + window.Title);
                if (scroll.ExtentWidth > scroll.ViewportWidth + 1)
                    throw new InvalidOperationException("Window content exceeds viewport width: " + window.Title);
                if (scroll.ScrollableHeight > 1)
                {
                    scroll.ScrollToVerticalOffset(scroll.ScrollableHeight);
                    content.UpdateLayout();
                    if (scroll.VerticalOffset <= 0) throw new InvalidOperationException("Window scrolling did not move: " + window.Title);
                }
            }
            finally
            {
                if (window.DataContext is IDisposable viewModel) viewModel.Dispose();
                window.Close();
            }
        }
    }
}
