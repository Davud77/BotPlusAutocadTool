// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.IO;

namespace BotPlusAutocadTool.Infrastructure
{
    internal static class AppDataPaths
    {
        internal static string UserDirectory
        {
            get
            {
                // Allows an isolated data folder for headless validation/managed deployments.
                var configured = Environment.GetEnvironmentVariable("BOTPLUS_DATA_DIRECTORY");
                var path = string.IsNullOrWhiteSpace(configured)
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BotPlusAutocadTool")
                    : Path.GetFullPath(configured);
                Directory.CreateDirectory(path);
                return path;
            }
        }
    }
}
