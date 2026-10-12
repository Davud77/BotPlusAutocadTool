// ============================================================================
// OOXGN - Copyright (c) 2026. All rights reserved.
// Author: OOXGN | Project: BotPlusAutocadTool
// SPDX-License-Identifier: AGPL-3.0-only
// ============================================================================

using System;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace BotPlusAutocadTool.Services
{
    /// <summary>
    /// Сервис для сохранения настроек плагина
    /// </summary>
    public static class SettingsService
    {
        private static readonly string SettingsPath;
        private static PluginSettings _settings;

        static SettingsService()
        {
            SettingsPath = Path.Combine(PluginPaths.UserDataDirectory, "settings.json");
            Load();
        }

        public static PluginSettings Settings => _settings;

        private static void Load()
        {
            try
            {
                var legacyPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "wm_settings.json");
                var loadPath = File.Exists(SettingsPath) ? SettingsPath : legacyPath;
                if (File.Exists(loadPath))
                {
                    var json = File.ReadAllText(loadPath);
                    _settings = JsonSerializer.Deserialize<PluginSettings>(json) ?? new PluginSettings();
                }
                else
                {
                    _settings = new PluginSettings();
                }
            }
            catch
            {
                _settings = new PluginSettings();
            }
        }

        public static void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch { }
        }
    }

    public class PluginSettings
    {
        // SumLength settings
        public int ScaleNumerator { get; set; } = 1;
        public int ScaleDenominator { get; set; } = 1;
        public int LengthUnit { get; set; } = 0; // 0=mm, 1=m, 2=km
        public int AreaUnit { get; set; } = 0;   // 0=mm², 1=m², 2=km²
    }
}
