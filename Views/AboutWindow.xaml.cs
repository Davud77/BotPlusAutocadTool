// ============================================================================
// OOXGN - Copyright (c) 2026. All rights reserved.
// Author: OOXGN | Project: BotPlusAutocadTool
// SPDX-License-Identifier: AGPL-3.0-only
// ============================================================================

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace BotPlusAutocadTool.Views
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            LoadVersion();
            LoadLogo();
        }

        private void LoadVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            VersionText.Text = $"BotPlusAutocadTool v{version?.Major}.{version?.Minor}.{version?.Build}";
        }

        private void LoadLogo()
        {
            try
            {
                // Получаем путь к DLL плагина
                var assemblyPath = Assembly.GetExecutingAssembly().Location;
                var assemblyDir = Path.GetDirectoryName(assemblyPath);
                
                // Ищем logo.png рядом с DLL или в папке Resources
                var logoPath = Path.Combine(assemblyDir, "Resources", "logo.png");
                
                if (!File.Exists(logoPath))
                {
                    // Попробуем найти в той же папке
                    logoPath = Path.Combine(assemblyDir, "logo.png");
                }

                if (File.Exists(logoPath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(logoPath, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    LogoImage.Source = bitmap;
                }
            }
            catch
            {
                // Игнорируем ошибки загрузки логотипа
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
                e.Handled = true;
            }
            catch { }
        }
    }
}
