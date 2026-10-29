// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using BotPlusAutocadTool.Services;
using Color = Autodesk.AutoCAD.Colors.Color;

public sealed class TableStyleResetChecks
{
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    [CommandMethod("BOTPLUS_TABLESTYLE_TEST")]
    public void Command()
    {
        var ed = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument.Editor;
        try { Run(ed); ed.WriteMessage("\nBOTPLUS_TABLESTYLE_PASS\n"); }
        catch (System.Exception ex) { ed.WriteMessage("\nBOTPLUS_TABLESTYLE_FAIL: " + ex + "\n"); }
    }
    internal static void Run(Editor ed)
    {
        using var db = new Database(true, true);
        ObjectId id, lockedId, lineId, fieldId, styleId;
        double baselineHeight; CellAlignment baselineAlignment; string baselineColor;
        string fieldCode, formula;
        double[] rowHeights, columnWidths;
        using (var tr = db.TransactionManager.StartTransaction())
        {
            var styles = (DBDictionary)tr.GetObject(db.TableStyleDictionaryId, OpenMode.ForWrite);
            var style = new TableStyle(); style.CopyFrom(tr.GetObject(db.Tablestyle, OpenMode.ForRead));
            styleId = styles.SetAt("BotPlus_Reset_Style", style); tr.AddNewlyCreatedDBObject(style, true);
            var model = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);
            var table = new Table { TableStyle = styleId, Position = new Point3d(30, 40, 0) };
            table.SetSize(6, 3); table.SetRowHeight(20); table.SetColumnWidth(65);
            style.SetTextHeight(3.5, (int)RowType.DataRow);
            baselineHeight = table.Cells[2, 2].TextHeight.Value;
            baselineAlignment = table.Cells[2, 2].Alignment.Value;
            baselineColor = table.Cells[2, 2].ContentColor.ToString();
            table.Cells[0, 0].TextString = "Title";
            table.Cells[2, 0].TextString = "12"; table.Cells[3, 0].TextString = "7";
            table.Cells[3, 1].TextString = "0";
            table.Cells[3, 1].Contents[0].Formula = "=A3+A4";
            table.Cells[2, 2].TextString = @"{\fArial|b1;\C1;\H2x;\W2;\KFirst\k}\PSecond \S1/2; \\K";
            table.Cells[2, 2].Contents.Add();
            table.Cells[2, 2].Contents[1].TextString = @"{\C2;Extra}";
            table.MergeCells(CellRange.Create(table, 4, 1, 4, 2));
            table.Cells[4, 1].TextString = "Merged";
            table.GenerateLayout();
            id = model.AppendEntity(table); tr.AddNewlyCreatedDBObject(table, true);
            var field = new Field("%<\\AcVar Filename>%");
            fieldId = db.AddDBObject(field); tr.AddNewlyCreatedDBObject(field, true);
            fieldCode = field.GetFieldCode(FieldCodeFlags.AddMarkers);
            table.Cells[1, 0].FieldId = fieldId;
            foreach (var coordinate in new[] { (2, 2), (1, 0), (3, 1) })
            {
                var cell = table.Cells[coordinate.Item1, coordinate.Item2];
                cell.TextHeight = 8; cell.Alignment = CellAlignment.BottomRight;
                cell.ContentColor = Color.FromRgb(220, 30, 50);
                cell.BackgroundColor = Color.FromRgb(100, 120, 140);
                cell.Borders.Left.Color = Color.FromRgb(20, 60, 200);
                foreach (CellContent content in cell.Contents) content.TextHeight = 7;
            }
            table.Cells[5, 2].TextString = "Protected"; table.Cells[5, 2].TextHeight = 9;
            table.Cells[5, 2].State |= CellStates.FormatLocked;
            var layers = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForWrite);
            var layer = new LayerTableRecord { Name = "RESET_LOCKED" }; layers.Add(layer); tr.AddNewlyCreatedDBObject(layer, true);
            var locked = new Table { TableStyle = styleId, LayerId = layer.ObjectId };
            locked.SetSize(3, 2); locked.Cells[2, 0].TextString = "Locked"; locked.Cells[2, 0].TextHeight = 9;
            lockedId = model.AppendEntity(locked); tr.AddNewlyCreatedDBObject(locked, true); layer.IsLocked = true;
            var line = new Line(Point3d.Origin, new Point3d(10, 10, 0)); lineId = model.AppendEntity(line); tr.AddNewlyCreatedDBObject(line, true);
            rowHeights = table.Rows.Cast<Row>().Select(row => row.Height).ToArray();
            columnWidths = table.Columns.Cast<Column>().Select(column => column.Width).ToArray();
            tr.Commit();
        }
        using (var tr = db.TransactionManager.StartTransaction())
        {
            var table = (Table)tr.GetObject(id, OpenMode.ForRead);
            rowHeights = table.Rows.Cast<Row>().Select(row => row.Height).ToArray();
            columnWidths = table.Columns.Cast<Column>().Select(column => column.Width).ToArray();
            Check(table.Cells[3, 1].Contents[0].HasFormula, "Fixture formula was not created");
            formula = table.Cells[3, 1].Contents[0].Formula;
        }
        var result = TableStyleResetService.Reset(db, new[] { id, id, lockedId, lineId });
        Check(result.Tables == 1 && result.SkippedTables == 2 && result.SkippedCells == 1, "Selection/locked objects were not respected: " + result);
        using (var tr = db.TransactionManager.StartTransaction())
        {
            var table = (Table)tr.GetObject(id, OpenMode.ForRead);
            Check(table.TableStyle == styleId && table.Position == new Point3d(30, 40, 0) && table.Rows.Count == 6 && table.Columns.Count == 3, "Table identity/style/geometry changed");
            Check(table.Rows.Cast<Row>().Select((row, i) => Math.Abs(row.Height - rowHeights[i]) < .001).All(value => value) &&
                table.Columns.Cast<Column>().Select((column, i) => Math.Abs(column.Width - columnWidths[i]) < .001).All(value => value), "Row heights or column widths changed: before=" + string.Join(",", rowHeights) + "; after=" + string.Join(",", table.Rows.Cast<Row>().Select(row => row.Height)));
            var cell = table.Cells[2, 2];
            Check(Math.Abs(cell.TextHeight.GetValueOrDefault(double.NaN) - baselineHeight) < .001 && cell.Alignment == baselineAlignment && cell.ContentColor.ToString() == baselineColor, "Cell did not inherit the table style");
            Check(cell.Contents.Count == 2 && cell.Contents[0].TextString == @"First\PSecond \S1/2; \\K" && cell.Contents[1].TextString == "Extra", "Inline formatting/content count changed incorrectly");
            Check(cell.Contents.Cast<CellContent>().All(content => Math.Abs(content.TextHeight - baselineHeight) < .001), "Content text-height override survived reset");
            Check(table.Cells[1, 0].FieldId == fieldId && ((Field)tr.GetObject(fieldId, OpenMode.ForRead)).GetFieldCode(FieldCodeFlags.AddMarkers) == fieldCode, "Field identity/code changed");
            Check(table.Cells[3, 1].Contents[0].HasFormula && table.Cells[3, 1].Contents[0].Formula == formula, "Formula changed: " + formula + " -> " + table.Cells[3, 1].Contents[0].Formula);
            Check(table.Cells[2, 0].TextString == "12" && table.Cells[3, 0].TextString == "7", "Numeric values changed");
            Check(table.Cells[4, 1].IsMerged == true && table.Cells[4, 1].GetMergeRange().RightColumn == 2, "Merge changed");
            Check(table.Cells[5, 2].TextHeight == 9 && ((Table)tr.GetObject(lockedId, OpenMode.ForRead)).Cells[2, 0].TextHeight == 9, "Protected format was modified");
        }
        ed.WriteMessage("\nFEATURE_PASS: table style reset; inline and per-content formatting, fields/formulas, multi-content, numbers, merges, geometry, locked cells/layers and mixed selection\n");
    }
}
