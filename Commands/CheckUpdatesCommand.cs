// SPDX-License-Identifier: AGPL-3.0-only
using Autodesk.AutoCAD.Runtime;
using BotPlusAutocadTool.Services;
namespace BotPlusAutocadTool.Commands
{
    public sealed class CheckUpdatesCommand
    {
        [CommandMethod("BOTPLUS_CHECKUPDATES")]
        public void Run() => UpdateCheckService.Check(true);
    }
}
