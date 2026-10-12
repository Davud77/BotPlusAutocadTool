using Autodesk.AutoCAD.Runtime;
using BotPlusAutocadTool.Services;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace BotPlusAutocadTool.Commands
{
    public sealed class BlockExportCommand
    {
        [CommandMethod("WEAM_BLOCKS_STAGE1", CommandFlags.Modal)]
        public void Run()
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            try
            {
                LegacyBlockExportService.Run(doc, PluginPaths.CreateDsdPath());
            }
            catch (System.Exception ex)
            {
                doc.Editor.WriteMessage($"\n[BotPlusAutocadTool] Ошибка подготовки блоков: {ex.Message}\n");
            }
        }
    }
}
