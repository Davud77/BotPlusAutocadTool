// SPDX-License-Identifier: AGPL-3.0-only
using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using BotPlusAutocadTool.Models;

namespace BotPlusAutocadTool.Services
{
    internal static class StampDrawingTransfer
    {
        internal static void Write(Document document, StampProperties properties)
        {
            if (!string.IsNullOrEmpty(document.CommandInProgress))
                throw new InvalidOperationException("В чертеже выполняется команда. Завершите её и повторите перенос.");
            using (document.LockDocument()) Write(document.Database, properties);
            document.Editor.Regen();
        }

        internal static void Write(Database database, StampProperties properties)
        {
            var builder = new DatabaseSummaryInfoBuilder(database.SummaryInfo);
            foreach (var key in StampSchema.Keys)
                builder.CustomPropertyTable[key] = properties.GetValue(key);
            database.SummaryInfo = builder.ToDatabaseSummaryInfo();
            database.EvaluateFields(FieldEvaluationContext.Demand);
        }
    }
}
