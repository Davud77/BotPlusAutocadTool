// SPDX-License-Identifier: AGPL-3.0-only
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using BotPlusAutocadTool.Services;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace BotPlusAutocadTool.Commands
{
    public sealed class InsertFormatCommand
    {
        [CommandMethod("BOTPLUS_INSERTFORMAT", CommandFlags.Modal)]
        public void Run()
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var point = doc.Editor.GetPoint("\nУкажите точку вставки форматки WM_FORMAT: ");
            if (point.Status != PromptStatus.OK) return;
            try
            {
                using (doc.LockDocument())
                {
                    var definition = FormatInsertService.EnsureDefinition(doc.Database);
                    FormatInsertService.Insert(doc.Database, definition, point.Value);
                }
                doc.Editor.Regen();
                doc.Editor.WriteMessage("\nФорматка WM_FORMAT вставлена на слой WM_FORMAT.\n");
            }
            catch (System.Exception ex) { doc.Editor.WriteMessage("\n[BotPlus AT] Не удалось вставить форматку: " + ex.Message + "\n"); }
        }
    }
}
