// SPDX-License-Identifier: AGPL-3.0-only
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace BotPlusAutocadTool.Services
{
    internal static class StampSchema
    {
        internal static IEnumerable<string> Keys
        {
            get
            {
                foreach (var key in new[] { "Взам.инв.№", "Подп.и.дата", "Инв.№", "Стадия", "Листов", "Компания",
                    "Обозначение_документа1", "Обозначение_документа2", "Наименование_объекта1", "Наименование_объекта2", "Наименование_участка1", "Наименование_участка2" }) yield return key;
                for (int i = 1; i <= 4; i++) foreach (var prefix in new[] { "Изм.", "Кол.уч.", "Лист.", "Nдок." }) yield return prefix + i;
                for (int i = 1; i <= 6; i++) { yield return "Должн." + i; yield return "Фам." + i; }
                for (int i = 1; i <= 10; i++) yield return "Дата." + i;
            }
        }

        internal static void EnsureProperties(Database database)
        {
            var builder = new DatabaseSummaryInfoBuilder(database.SummaryInfo);
            var properties = builder.CustomPropertyTable;
            bool changed = false;
            foreach (var key in Keys)
            {
                if (!properties.Contains(key)) { properties.Add(key, " "); changed = true; }
                else if (string.IsNullOrWhiteSpace(properties[key]?.ToString())) { properties[key] = " "; changed = true; }
            }
            if (changed) database.SummaryInfo = builder.ToDatabaseSummaryInfo();
        }
    }
}
