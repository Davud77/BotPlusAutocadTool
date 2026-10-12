using System;
using System.IO;

namespace BotPlusAutocadTool.Services
{
    internal static class PluginPaths
    {
        public static string UserDataDirectory
        {
            get
            {
                var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BotPlusAutocadTool");
                Directory.CreateDirectory(directory);
                return directory;
            }
        }

        public static string CreateDsdPath()
        {
            var directory = Path.Combine(UserDataDirectory, "Publish");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, "Publish-" + Guid.NewGuid().ToString("N") + ".dsd");
        }
    }
}
