using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Autodesk.AutoCAD.Runtime;
using BotPlusAutocadTool.Models;
using BotPlusAutocadTool.Services;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

public sealed class WordChecks
{
    [CommandMethod("BOTPLUS_WORDTRANSFERTEST")]
    public void Run()
    {
        object application = null, documents = null, first = null, second = null;
        bool own = false;
        var editor = AcadApp.DocumentManager.MdiActiveDocument.Editor;
        try
        {
            var type = Type.GetTypeFromProgID("Word.Application") ?? throw new InvalidOperationException("Word COM is not installed");
            application = Activator.CreateInstance(type);
            documents = ((dynamic)application).Documents;
            if ((int)((dynamic)documents).Count != 0) throw new InvalidOperationException("Word test requires a separate empty instance");
            own = true;
            ((dynamic)application).Visible = false;
            ((dynamic)application).DisplayAlerts = 0;
            first = ((dynamic)documents).Add(); second = ((dynamic)documents).Add();
            var directory = Path.Combine(Path.GetDirectoryName(AcadApp.DocumentManager.MdiActiveDocument.Name), "word-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path1 = Path.Combine(directory, "first.docx"), path2 = Path.Combine(directory, "second.docx");
            ((dynamic)first).SaveAs2(path1); ((dynamic)second).SaveAs2(path2);
            var source = new StampProperties();
            foreach (var key in StampSchema.Keys) source.SetValue(key, " ");
            source.SetValue("Компания", "Компания Проверка");
            var props = ((dynamic)first).CustomDocumentProperties;
            props.Add("Unrelated", false, 4, "Keep");
            var content = ((dynamic)first).Content;
            var field = ((dynamic)first).Fields.Add(content, -1, "DOCPROPERTY Компания", false);
            var headerRange = ((dynamic)first).Sections.Item(1).Headers.Item(1).Range;
            var headerField = headerRange.Fields.Add(headerRange, -1, "DOCPROPERTY Компания", false);
            var targets = WordStampTransfer.GetOpenDocuments();
            try
            {
                var selected = targets.Where(t => t.Location == path1 || t.Location == path2).ToList();
                if (selected.Count != 2) throw new InvalidOperationException("Open Word document enumeration missed test documents: " + selected.Count);
                foreach (var target in selected) target.Write(source);
            }
            finally { foreach (var target in targets) target.Dispose(); }
            if ((string)props.Item("Компания").Value != "Компания Проверка" || !((string)field.Result.Text).Contains("Компания Проверка"))
                throw new InvalidOperationException("Word property/DOCPROPERTY did not update");
            if (!((string)headerField.Result.Text).Contains("Компания Проверка")) throw new InvalidOperationException("Header DOCPROPERTY did not update");
            foreach (var key in StampSchema.Keys) source.SetValue(key, " ");
            WordStampTransfer.Write(first, source);
            if (StampSchema.Keys.Any(key => (string)props.Item(key).Value != " ") || (string)props.Item("Unrelated").Value != "Keep")
                throw new InvalidOperationException("Blank Word transfer changed values or unrelated properties");
            if (!string.IsNullOrWhiteSpace((string)field.Result.Text)) throw new InvalidOperationException("Blank DOCPROPERTY did not update");
            if (!string.IsNullOrWhiteSpace((string)headerField.Result.Text)) throw new InvalidOperationException("Blank header DOCPROPERTY did not update");
            Marshal.ReleaseComObject(headerField); Marshal.ReleaseComObject(headerRange);
            Marshal.ReleaseComObject(field); Marshal.ReleaseComObject(content); Marshal.ReleaseComObject(props);
            editor.WriteMessage("\nWORD_TRANSFER_PASS: two real Word documents, created custom properties, values/blank modes, DOCPROPERTY refreshed, unrelated property preserved\n");
        }
        catch (System.Exception ex) { editor.WriteMessage("\nWORD_TRANSFER_FAIL: " + ex + "\n"); }
        finally
        {
            if (own)
            {
                if (first != null) ((dynamic)first).Close(false);
                if (second != null) ((dynamic)second).Close(false);
                if (application != null) ((dynamic)application).Quit(false);
            }
            foreach (var value in new[] { first, second, documents, application }) if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value);
        }
    }
}
