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
                return Infrastructure.AppDataPaths.UserDirectory;
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
