// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;

namespace BotPlusAutocadTool.Services
{
    public sealed class PlotStyleChoice
    {
        // null = current Model layout, empty = no plot style.
        public string FileName { get; }
        public string Name { get; }
        public PlotStyleChoice(string fileName, string name) { FileName = fileName; Name = name; }
        public override string ToString() => Name;
    }

    public static class PlotStyleService
    {
        public sealed record Resolved(string FileName, bool Enabled, bool Lineweights, string Source);
        public static string Extension(Database db) => db.PlotStyleMode ? ".ctb" : ".stb";

        public static List<PlotStyleChoice> GetChoices(Database db)
        {
            using var settings = new PlotSettings(true);
            PlotSettingsValidator.Current.RefreshLists(settings);
            var choices = new List<PlotStyleChoice>
            {
                new PlotStyleChoice(null, "Из DWG"), new PlotStyleChoice("", "Без стиля")
            };
            choices.AddRange(PlotSettingsValidator.Current.GetPlotStyleSheetList().Cast<string>()
                .Where(name => name.EndsWith(Extension(db), StringComparison.OrdinalIgnoreCase))
                .Select(Path.GetFileName).Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                .Select(name => new PlotStyleChoice(name, name)));
            return choices;
        }

        public static Resolved Resolve(Database db, string selected)
        {
            string name = selected;
            bool enabled = !string.IsNullOrEmpty(selected), lineweights = true;
            if (selected == null)
            {
                using var tr = db.TransactionManager.StartTransaction();
                var layouts = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
                var model = (Layout)tr.GetObject(layouts.GetAt("Model"), OpenMode.ForRead);
                name = model.CurrentStyleSheet ?? "";
                enabled = model.PlotPlotStyles; lineweights = model.PrintLineweights;
            }
            if (!enabled) name = "";
            if (!string.IsNullOrEmpty(name))
            {
                if (Path.GetFileName(name) != name || !name.EndsWith(Extension(db), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Для этого DWG нужен стиль {Extension(db).ToUpperInvariant()}: {name}.");
                if (!GetChoices(db).Any(choice => string.Equals(choice.FileName, name, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException($"Стиль печати «{name}» не найден. Добавьте его в папку стилей AutoCAD или выберите другой в окне печати.");
            }
            return new Resolved(name, enabled && !string.IsNullOrEmpty(name), lineweights,
                selected == null ? "Из DWG" : selected.Length == 0 ? "Без стиля" : "Выбран в печати");
        }

        public static void Apply(PlotSettings settings, Resolved style)
        {
            PlotSettingsValidator.Current.SetCurrentStyleSheet(settings, style.FileName);
            settings.PlotPlotStyles = style.Enabled;
            settings.PrintLineweights = style.Lineweights;
        }
    }
}
