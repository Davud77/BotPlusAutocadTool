// SPDX-License-Identifier: AGPL-3.0-only
using Autodesk.AutoCAD.Runtime;

namespace BotPlusAutocadTool.Commands
{
    public sealed class AdditionalFunctionsCommand
    {
        [CommandMethod("BOTPLUS_ADDITIONAL", CommandFlags.Session)]
        public void Run() => System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.ApplicationIdle, new System.Action(UI.AdditionalFunctionsMenu.Show));
    }
}
