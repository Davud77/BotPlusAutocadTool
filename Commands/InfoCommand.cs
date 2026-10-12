// ============================================================================
// OOXGN - Copyright (c) 2026. All rights reserved.
// Author: OOXGN | Project: BotPlusAutocadTool
// SPDX-License-Identifier: AGPL-3.0-only
// ============================================================================

using Autodesk.AutoCAD.Runtime;
using BotPlusAutocadTool.Views;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace BotPlusAutocadTool.Commands
{
    public class InfoCommand
    {
        [CommandMethod("WEAM_INFO")]
        public void Execute()
        {
            var window = new AboutWindow();
            // Показываем как модальное окно поверх AutoCAD
            AcadApp.ShowModalWindow(window);
        }
    }
}