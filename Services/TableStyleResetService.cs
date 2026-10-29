// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using BotPlusAutocadTool.Models;

namespace BotPlusAutocadTool.Services
{
    public static class TableStyleResetService
    {
        public sealed record Result(int Tables, int Cells, int SkippedTables, int SkippedCells);

        public static Result Reset(Database database, IEnumerable<ObjectId> tableIds)
        {
            int tables = 0, cells = 0, skippedTables = 0, skippedCells = 0;
            var textOptions = new StripTextOptions
            {
                LineFeed = false, Tabs = false, NonBreakingSpace = false,
                Stacking = false, Width = true, Strikethrough = true
            };
            using var tr = database.TransactionManager.StartTransaction();
            foreach (var id in tableIds.Distinct())
            {
                if (id.IsNull || id.IsErased || tr.GetObject(id, OpenMode.ForRead) is not Table table)
                { skippedTables++; continue; }
                var layer = (LayerTableRecord)tr.GetObject(table.LayerId, OpenMode.ForRead);
                if (layer.IsLocked) { skippedTables++; continue; }
                table.UpgradeOpen();
                for (int row = 0; row < table.Rows.Count; row++)
                for (int column = 0; column < table.Columns.Count; column++)
                {
                    var cell = table.Cells[row, column];
                    if (cell.IsMerged == true)
                    {
                        var merged = cell.GetMergeRange();
                        if (row != merged.TopRow || column != merged.LeftColumn) continue;
                    }
                    if ((cell.State.GetValueOrDefault(CellStates.None) & (CellStates.FormatLocked | CellStates.FormatReadOnly | CellStates.ContentLocked | CellStates.ContentReadOnly)) != 0)
                    { skippedCells++; continue; }
                    // Use each content separately: assigning Cell.TextString would collapse
                    // multiple contents and could replace a formula, field or block.
                    foreach (CellContent content in cell.Contents)
                    {
                        if (content.HasFormula || !content.FieldId.IsNull || !content.BlockTableRecordId.IsNull) continue;
                        string original = content.TextString;
                        string stripped = MTextFormatting.Strip(original, textOptions);
                        if (stripped != original) content.TextString = stripped;
                    }
                    cell.ClearStyleOverrides();
                    cells++;
                }
                table.RecordGraphicsModified(true);
                tables++;
            }
            tr.Commit();
            return new Result(tables, cells, skippedTables, skippedCells);
        }
    }
}
