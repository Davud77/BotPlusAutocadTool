// SPDX-License-Identifier: AGPL-3.0-only
using Autodesk.AutoCAD.Runtime;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
namespace BotPlusAutocadTool.Commands
{
    public sealed class SettingsCommand
    {
        [CommandMethod("BOTPLUS_SETTINGS", CommandFlags.Modal)]
        public void Run() => AcadApp.ShowModalWindow(new Views.SettingsWindow());
    }
}
