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
            () => new AboutWindow(), () => new AutoNumSettingsWindow(new AutoNumSettingsViewModel()),
            () => new CleanDwgWindow(), () => new CustomerOrderWindow(), () => new PlanPrepWindow(),
            () => new SelectSimilarWindow(), () => new StripTextWindow(), () => new SumLengthWindow { DataContext = new SumLengthViewModel() },
            () => new BatchPlotWindow(), () => new StampWindow(new StampPropsViewModel(new AutoCADService())),
            () => new SettingsWindow(), () => new AutoNumSettingsWindow(new AutoNumSettingsViewModel { ModeIndex = 3 })
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
                var bitmap = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(content);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                var suffix = window.DataContext is AutoNumSettingsViewModel vm && vm.ModeIndex == 3 ? "-tables" : "";
                using var stream = File.Create(Path.Combine(results, window.GetType().Name + suffix + ".png"));
                encoder.Save(stream);
                // Check the complete form remains reachable on a smaller working area.
                content.Measure(new Size(400, 320)); content.Arrange(new Rect(0, 0, 400, 320)); content.UpdateLayout();
                var narrow = new RenderTargetBitmap(400, 320, 96, 96, PixelFormats.Pbgra32); narrow.Render(content);
                var narrowEncoder = new PngBitmapEncoder(); narrowEncoder.Frames.Add(BitmapFrame.Create(narrow));
                using (var narrowFile = File.Create(Path.Combine(results, window.GetType().Name + suffix + "-narrow.png"))) narrowEncoder.Save(narrowFile);
                var scroll = (window.Content as DockPanel)?.Children.OfType<ScrollViewer>().SingleOrDefault();
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
