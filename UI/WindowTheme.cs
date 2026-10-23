// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace BotPlusAutocadTool.UI
{
    public static class WindowTheme
    {
        public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(WindowTheme), new PropertyMetadata(false, Changed));
        public static void SetEnabled(DependencyObject target, bool value) => target.SetValue(EnabledProperty, value);
        public static bool GetEnabled(DependencyObject target) => (bool)target.GetValue(EnabledProperty);
        private static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs args)
        {
            if (target is not Window window || !(bool)args.NewValue) return;
            WindowPreferences.Attach(window);
            window.Initialized += (_, _) => Apply(window);
            window.SourceInitialized += (_, _) => Apply(window);
            window.Activated += (_, _) => Apply(window);
        }
        public static void Apply(Window window)
        {
            bool dark = true;
            try { dark = Convert.ToInt32(AcadApp.GetSystemVariable("COLORTHEME")) == 0; } catch { }
            var keys = new[] { "WindowBackground", "ControlBackground", "ControlBorder", "TextPrimary", "TextSecondary", "AccentColor", "HoverBackground", "SelectionBackground" };
            var colors = dark
                ? new[] { "#2B3440", "#3B4453", "#606B7C", "#F0F2F5", "#BBC4D0", "#22A7DD", "#4B596D", "#087EAF" }
                : new[] { "#ECEFF3", "#FFFFFF", "#A9B0BA", "#202830", "#52606F", "#007DB8", "#DCE8F1", "#007DB8" };
            for (int i = 0; i < keys.Length; i++)
            {
                var color = (Color)ColorConverter.ConvertFromString(colors[i]);
                if (window.Resources[keys[i]] is SolidColorBrush existing && existing.Color == color) continue;
                var brush = new SolidColorBrush(color); brush.Freeze(); window.Resources[keys[i]] = brush;
            }
            var handle = new WindowInteropHelper(window).Handle;
            if (handle != IntPtr.Zero)
            {
                int enabled = dark ? 1 : 0;
                try { DwmSetWindowAttribute(handle, 20, ref enabled, sizeof(int)); } catch (DllNotFoundException) { }
            }
        }
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
    }
}
