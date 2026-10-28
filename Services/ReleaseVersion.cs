// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace BotPlusAutocadTool.Services
{
    public sealed class ReleaseVersion
    {
        public Version Version { get; }
        public string Tag { get; }
        public Uri Url { get; }
        public Uri InstallerUrl { get; private set; }
        public string InstallerName { get; private set; }
        public long InstallerSize { get; private set; }
        public string InstallerSha256 { get; private set; }
        public Uri ChecksumsUrl { get; private set; }
        private ReleaseVersion(Version version, string tag, Uri url) { Version = version; Tag = tag; Url = url; }

        public static ReleaseVersion FindUpdate(string json, Version installed, bool includePrerelease = false)
        {
            ReleaseVersion best = null;
            foreach (var release in JArray.Parse(json))
            {
                if (release.Value<bool>("draft") || (!includePrerelease && release.Value<bool>("prerelease"))) continue;
                string tag = release.Value<string>("tag_name");
                string number = tag?.TrimStart('v', 'V').Split('-', '+')[0];
                if (!Version.TryParse(number, out var version)) continue;
                var normalized = new Version(version.Major, version.Minor, Math.Max(0, version.Build));
                var current = new Version(installed.Major, installed.Minor, Math.Max(0, installed.Build));
                if (normalized <= current || (best != null && normalized <= best.Version)) continue;
                if (!Uri.TryCreate(release.Value<string>("html_url"), UriKind.Absolute, out var url) ||
                    url.Scheme != "https" || url.Host != "github.com" ||
                    !url.AbsolutePath.StartsWith("/Davud77/BotPlusAutocadTool/releases/", StringComparison.Ordinal)) continue;
                best = new ReleaseVersion(normalized, tag, url);
                string installer = "BotPlusAutocadTool-" + number + "-Setup.exe";
                string checksums = "SHA256SUMS-" + number + ".txt";
                foreach (var asset in release["assets"] as JArray ?? new JArray())
                {
                    string name = asset.Value<string>("name");
                    if (name != installer && name != checksums) continue;
                    var expected = new Uri("https://github.com/Davud77/BotPlusAutocadTool/releases/download/" +
                        Uri.EscapeDataString(tag) + "/" + Uri.EscapeDataString(name));
                    if (!Uri.TryCreate(asset.Value<string>("browser_download_url"), UriKind.Absolute, out var download) || download != expected) continue;
                    if (name == checksums) best.ChecksumsUrl = download;
                    else
                    {
                        best.InstallerUrl = download;
                        best.InstallerName = name;
                        best.InstallerSize = asset.Value<long?>("size") ?? 0;
                        string digest = asset.Value<string>("digest");
                        if (digest?.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) == true)
                            best.InstallerSha256 = digest.Substring(7);
                    }
                }
            }
            return best;
        }
    }
}
