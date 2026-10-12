using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using System;
using BotPlusAutocadTool.Services;

// Core.Application
using AcadCoreApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace BotPlusAutocadTool.Commands
{
    public sealed class TableExcelCommand
    {
        [CommandMethod("WEAM_TABLEEXCEL")]
        public void Run()
        {
            var doc = AcadCoreApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var ed = doc.Editor;

            try
            {
                var peo = new PromptEntityOptions("\nВыберите таблицу AutoCAD: ");
                peo.SetRejectMessage("\nНужен объект TABLE.");
                peo.AddAllowedClass(typeof(Table), exactMatch: false);

                var per = ed.GetEntity(peo);
                if (per.Status != PromptStatus.OK) return;

                var service = new TableExcelService();
                service.ExportOpenAndAutoImport(doc, per.ObjectId);
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n[WEAM TableExcel] Ошибка: {ex}\n");
            }
        }
    }
}
