// SPDX-License-Identifier: AGPL-3.0-only
using BotPlusAutocadTool.Services;
namespace BotPlusAutocadTool.Views
{
    // Compatibility entry point; updates now use the service window.
    public sealed class UpdateDownloadWindow : AboutWindow
    {
        public UpdateDownloadWindow(ReleaseVersion release) : base(release) { }
    }
}
