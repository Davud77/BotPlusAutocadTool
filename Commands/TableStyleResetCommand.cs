// SPDX-License-Identifier: AGPL-3.0-only
using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using BotPlusAutocadTool.Infrastructure;
using BotPlusAutocadTool.Services;
using A = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace BotPlusAutocadTool.Commands
{
    public sealed class TableStyleResetCommand
    {
        [CommandMethod("BOTPLUS_CLEARTABLESTYLE", CommandFlags.UsePickSet)]
        public void Run()
        {
            var doc = A.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            var selection = ed.SelectImplied();
            if (selection.Status != PromptStatus.OK)
                selection = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = "\nВыберите таблицы для сброса оформления ячеек: " },
                    new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "ACAD_TABLE") }));
            if (selection.Status != PromptStatus.OK) return;
            try
            {
                using (doc.LockDocument())
                {
                    var result = TableStyleResetService.Reset(doc.Database, selection.Value.GetObjectIds());
                    ed.Regen();
                    ed.WriteMessage($"\n[BotPlus AT] Очищено таблиц: {result.Tables}, ячеек: {result.Cells}. Пропущено объектов: {result.SkippedTables}, защищённых ячеек: {result.SkippedCells}.\n");
                }
            }
            catch (System.Exception ex)
            {
                PluginLog.Error("Reset table cell styles", ex);
                ed.WriteMessage($"\n[BotPlus AT] Не удалось очистить оформление; изменения отменены: {ex.Message}\n");
            }
        }
    }
}
