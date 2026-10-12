using System;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using BotPlusAutocadTool.Services;
using BotPlusAutocadTool.Models;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

public class Smoke
{
    [CommandMethod("BOTPLUS_SMOKETEST")]
    public void Run()
    {
        var editor = AcadApp.DocumentManager.MdiActiveDocument.Editor;
        try
        {
            using (var database = new Database(true, true))
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                var table = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
                var model = (BlockTableRecord)transaction.GetObject(table[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                var line = new Line(Point3d.Origin, new Point3d(300, 400, 0));
                var id = model.AppendEntity(line);
                transaction.AddNewlyCreatedDBObject(line, true);
                var result = new LengthService().CalculateLengths(transaction, new[] { id });
                var length = result.SelectMany(group => group.Items).Sum(item => item.RawLength);
                if (Math.Abs(length - 500) > 0.0001) throw new InvalidOperationException("Length calculation failed");
                var text = new StripTextService(new StripTextOptions());
                if (text.StripMtextFormatting("{\\C1;Test}").Contains("\\C1;")) throw new InvalidOperationException("Text reset failed");
            }
            var commandCount = typeof(BotPlusAutocadTool.App).Assembly.GetTypes()
                .SelectMany(type => type.GetMethods()).SelectMany(method => method.GetCustomAttributes(typeof(CommandMethodAttribute), false)).Count();
            if (commandCount != 17) throw new InvalidOperationException("Missing commands: " + commandCount);
            var stampService = new TestStampService();
            var stamp = new BotPlusAutocadTool.ViewModels.StampPropsViewModel(stampService);
            for (int length = 1; length <= 128; length++)
            {
                foreach (var value in new[] { new string('X', length), string.Join(" ", Enumerable.Repeat("Проект", 22)).Substring(0, length).Trim() })
                {
                    stamp.LoadValues();
                    stamp.НаименованиеОбъекта = value;
                    stamp.UpdateCommand.Execute(null);
                    if (stampService.Properties.GetValue("Наименование_объекта1").Length > 64 || stampService.Properties.GetValue("Наименование_объекта2").Length > 64)
                        throw new InvalidOperationException("Stamp part exceeded 64 characters");
                    stamp.LoadValues();
                    if (stamp.НаименованиеОбъекта != value) throw new InvalidOperationException("Stamp round-trip failed at " + length);
                }
            }
            editor.WriteMessage("\nBOTPLUS_SMOKE_PASS: plugin loaded, 17 commands, length 500, text reset, stamp round-trip 1-128 characters.\n");
        }
        catch (System.Exception ex) { editor.WriteMessage("\nBOTPLUS_SMOKE_FAIL: " + ex + "\n"); }
    }

    private sealed class TestStampService : IAutoCADService
    {
        public StampProperties Properties = new StampProperties();
        public StampProperties LoadStampProperties() => Properties;
        public void SaveStampProperties(StampProperties properties) => Properties = properties;
    }
}
