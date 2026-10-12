using BotPlusAutocadTool.Services;
using System.Text.Json;

var releases = JsonSerializer.Serialize(new[] {
    new {tag_name="v1.1.0",draft=false,prerelease=false,html_url="https://github.com/Davud77/BotPlusAutocadTool/releases/tag/v1.1.0"},
    new {tag_name="v1.2.0",draft=false,prerelease=false,html_url="https://github.com/Davud77/BotPlusAutocadTool/releases/tag/v1.2.0"},
    new {tag_name="v2.0.0-beta.1",draft=false,prerelease=true,html_url="https://github.com/Davud77/BotPlusAutocadTool/releases/tag/v2.0.0-beta.1"},
    new {tag_name="v9.0.0",draft=true,prerelease=false,html_url="https://github.com/Davud77/BotPlusAutocadTool/releases/tag/v9.0.0"},
    new {tag_name="v8.0.0",draft=false,prerelease=false,html_url="https://evil.invalid/download"},
    new {tag_name="v7.0.0",draft=false,prerelease=false,html_url="https://github.com/other/repo/releases/tag/v7.0.0"},
    new {tag_name="not-a-version",draft=false,prerelease=false,html_url="https://github.com/Davud77/BotPlusAutocadTool/releases/tag/foo"}
});
if (ReleaseVersion.FindUpdate(releases, new Version(1,1,0)).Tag != "v1.2.0") throw new Exception("Stable update selection failed");
if (ReleaseVersion.FindUpdate(releases, new Version(1,1,0), true).Tag != "v2.0.0-beta.1") throw new Exception("Optional prerelease failed");
if (ReleaseVersion.FindUpdate(releases, new Version(3,0,0)) != null) throw new Exception("Downgrade/unsafe URL accepted");
if (ReleaseVersion.FindUpdate("[]", new Version(1,1,0)) != null) throw new Exception("Empty release list failed");
Console.WriteLine("PASS: versions, drafts, prereleases, no downgrade, safe repository URLs, malformed tags, empty list.");
