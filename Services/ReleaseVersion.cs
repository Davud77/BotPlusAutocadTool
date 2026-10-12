// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Linq;
using System.Text.Json;

namespace BotPlusAutocadTool.Services
{
    public sealed class ReleaseVersion
    {
        public Version Version { get; }
        public string Tag { get; }
        public Uri Url { get; }
        private ReleaseVersion(Version version, string tag, Uri url) { Version = version; Tag = tag; Url = url; }

        public static ReleaseVersion FindUpdate(string json, Version installed, bool includePrerelease = false)
        {
            using (var document = JsonDocument.Parse(json))
            {
                ReleaseVersion best = null;
                foreach (var release in document.RootElement.EnumerateArray())
                {
                    if (release.GetProperty("draft").GetBoolean() || (!includePrerelease && release.GetProperty("prerelease").GetBoolean())) continue;
                    string tag = release.GetProperty("tag_name").GetString();
                    string number = tag?.TrimStart('v', 'V').Split('-', '+')[0];
                    if (!Version.TryParse(number, out var version)) continue;
                    var normalized = new Version(version.Major, version.Minor, Math.Max(0, version.Build));
                    var current = new Version(installed.Major, installed.Minor, Math.Max(0, installed.Build));
                    if (normalized <= current || (best != null && normalized <= best.Version)) continue;
                    if (!Uri.TryCreate(release.GetProperty("html_url").GetString(), UriKind.Absolute, out var url) ||
                        url.Scheme != "https" || url.Host != "github.com" ||
                        !url.AbsolutePath.StartsWith("/Davud77/BotPlusAutocadTool/releases/", StringComparison.Ordinal)) continue;
                    best = new ReleaseVersion(normalized, tag, url);
                }
                return best;
            }
        }
    }
}
