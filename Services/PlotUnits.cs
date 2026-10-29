// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;

namespace BotPlusAutocadTool.Services
{
    public sealed class PlotUnitDefinition
    {
        public int Code { get; }
        public string Name { get; }
        public double MillimetersPerUnit { get; }
        public PlotUnitDefinition(int code, string name, double millimetersPerUnit)
        { Code = code; Name = name; MillimetersPerUnit = millimetersPerUnit; }
        public override string ToString() => Name;
    }

    public sealed class PlotUnitContext
    {
        public PlotUnitDefinition Unit { get; }
        public int Measurement { get; }
        public string Source { get; }
        public bool IsAssumed { get; }
        public double MillimetersPerUnit => Unit.MillimetersPerUnit;
        public string Description => $"{(Measurement == 1 ? "Метрическая" : "Имперская")} настройка; 1 ед. = {Unit.Name}; {Source}";
        internal PlotUnitContext(PlotUnitDefinition unit, int measurement, string source, bool assumed = false)
        { Unit = unit; Measurement = measurement; Source = source; IsAssumed = assumed; }
    }

    /// <summary>Physical drawing units are independent of MEASUREMENT and paper units.</summary>
    public static class PlotUnits
    {
        private const double SurveyFootMm = 1200000.0 / 3937.0;
        public static IReadOnlyList<PlotUnitDefinition> Definitions { get; } = Array.AsReadOnly(new[]
        {
            new PlotUnitDefinition(4, "мм", 1), new PlotUnitDefinition(5, "см", 10),
            new PlotUnitDefinition(14, "дм", 100), new PlotUnitDefinition(6, "м", 1000),
            new PlotUnitDefinition(7, "км", 1000000), new PlotUnitDefinition(1, "дюйм", 25.4),
            new PlotUnitDefinition(2, "фут", 304.8), new PlotUnitDefinition(10, "ярд", 914.4),
            new PlotUnitDefinition(3, "миля", 1609344), new PlotUnitDefinition(21, "геодезический фут США", SurveyFootMm),
            new PlotUnitDefinition(8, "микродюйм", .0000254), new PlotUnitDefinition(9, "мил", .0254),
            new PlotUnitDefinition(11, "ангстрем", .0000001), new PlotUnitDefinition(12, "нм", .000001),
            new PlotUnitDefinition(13, "мкм", .001), new PlotUnitDefinition(15, "декаметр", 10000),
            new PlotUnitDefinition(16, "гектометр", 100000), new PlotUnitDefinition(17, "гигаметр", 1e12),
            new PlotUnitDefinition(18, "астрономическая единица", 149597870700000),
            new PlotUnitDefinition(19, "световой год", 9.4607304725808e18),
            new PlotUnitDefinition(20, "парсек", 3.0856775814913673e19),
            new PlotUnitDefinition(22, "геодезический дюйм США", SurveyFootMm / 12),
            new PlotUnitDefinition(23, "геодезический ярд США", SurveyFootMm * 3),
            new PlotUnitDefinition(24, "геодезическая миля США", SurveyFootMm * 5280)
        });

        public static PlotUnitDefinition Get(int code) => Definitions.FirstOrDefault(u => u.Code == code)
            ?? throw new ArgumentOutOfRangeException(nameof(code), $"Неизвестные единицы DWG: {code}. Укажите единицы в окне печати.");

        public static PlotUnitContext Resolve(int declaredUnits, int measurement, int? unitsOverride,
            IEnumerable<(double Width, double Height)> frames, IEnumerable<(double Width, double Height)> formats, int scale)
        {
            if (scale <= 0) throw new ArgumentOutOfRangeException(nameof(scale));
            if (unitsOverride.HasValue) return new PlotUnitContext(Get(unitsOverride.Value), measurement, "выбрано для этого DWG");
            if (declaredUnits != 0) return new PlotUnitContext(Get(declaredUnits), measurement, "из INSUNITS");

            var sizes = frames.ToArray();
            var papers = formats.ToArray();
            // Infer only a unique match to known paper dimensions. MEASUREMENT alone
            // controls hatch/linetype conventions and cannot identify the physical unit.
            // Paper dimensions cannot distinguish survey units from international units
            // (about two ppm apart). Survey units require declared units or an override.
            var scores = Definitions.Where(unit => unit.Code < 21).Select(unit => new
            {
                Unit = unit,
                Count = sizes.Count(size => papers.Any(paper =>
                    Matches(size.Width * unit.MillimetersPerUnit / scale, size.Height * unit.MillimetersPerUnit / scale, paper.Width, paper.Height)))
            }).OrderByDescending(candidate => candidate.Count).ToArray();
            if (scores[0].Count > 0 && scores[0].Count > scores[1].Count)
                return new PlotUnitContext(scores[0].Unit, measurement, "по рамкам; INSUNITS не задан");
            return new PlotUnitContext(Get(4), measurement, "единицы не заданы; принято мм — проверьте выбор", true);
        }

        private static bool Matches(double w, double h, double pw, double ph) =>
            (Math.Abs(w - pw) <= .25 && Math.Abs(h - ph) <= .25) ||
            (Math.Abs(w - ph) <= .25 && Math.Abs(h - pw) <= .25);

        public static double PaperLength(double length, double millimetersPerUnit, int scale)
        {
            double result = length * millimetersPerUnit / scale;
            if (!double.IsFinite(result) || result <= 0 || millimetersPerUnit <= 0 || scale <= 0)
                throw new ArgumentOutOfRangeException(nameof(length), "Некорректный размер рамки или масштаб печати.");
            return result;
        }
    }
}
