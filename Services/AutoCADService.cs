// ============================================================================
// OOXGN - Copyright (c) 2026. All rights reserved.
// Author: OOXGN | Project: BotPlusAutocadTool
// SPDX-License-Identifier: AGPL-3.0-only
// ============================================================================

// File: BotPlusAutocadTool/Services/AutoCADService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using BotPlusAutocadTool.Models;

// Псевдоним для Application
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace BotPlusAutocadTool.Services
{
    public class AutoCADService : IAutoCADService
    {
        public StampProperties LoadStampProperties()
        {
            var properties = new StampProperties();
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return properties;

            Database db = doc.Database;
            try
            {
                // Чтение SummaryInfo не требует транзакции, но требует блокировки, если БД занята?
                // Обычно чтение безопасно, но для порядка
                DatabaseSummaryInfoBuilder infoBuilder = new DatabaseSummaryInfoBuilder(db.SummaryInfo);
                var customProps = infoBuilder.CustomPropertyTable;

                foreach (System.Collections.DictionaryEntry entry in customProps)
                {
                    string key = entry.Key.ToString();
                    string value = entry.Value?.ToString();
                    properties.SetValue(key, value);
                }
            }
            catch (System.Exception ex)
            {
                doc.Editor.WriteMessage($"\n[WEAM Error] Ошибка загрузки свойств: {ex.Message}\n");
            }
            return properties;
        }

        public void SaveStampProperties(StampProperties properties)
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            // СТРОГОЕ ПРАВИЛО: Блокировка документа перед изменением БД
            using (doc.LockDocument())
            {
                Database db = doc.Database;
                try
                {
                    DatabaseSummaryInfoBuilder infoBuilder = new DatabaseSummaryInfoBuilder(db.SummaryInfo);
                    var customProps = infoBuilder.CustomPropertyTable;

                    int changesCount = 0;

                    foreach (var kvp in properties.Properties)
                    {
                        string key = kvp.Key;
                        string newValue = kvp.Value ?? " "; // Заменяем null на пробел, чтобы поля не слетали

                        if (string.IsNullOrWhiteSpace(key)) continue;

                        if (customProps.Contains(key))
                        {
                            // Обновляем существующее
                            string oldValue = customProps[key]?.ToString();
                            if (oldValue != newValue)
                            {
                                customProps[key] = newValue;
                                changesCount++;
                            }
                        }
                        else
                        {
                            // Добавляем новое
                            customProps.Add(key, newValue);
                            changesCount++;
                        }
                    }

                    if (changesCount > 0)
                    {
                        // Применяем изменения
                        db.SummaryInfo = infoBuilder.ToDatabaseSummaryInfo();

                        // Регенерация, чтобы поля (Fields) обновились визуально
                        doc.Editor.Regen();
                        doc.Editor.WriteMessage($"\n[WEAM] Штамп обновлен. Изменено полей: {changesCount}.\n");
                    }
                    else
                    {
                        doc.Editor.WriteMessage("\n[WEAM] Изменений нет.\n");
                    }
                }
                catch (System.Exception ex)
                {
                    doc.Editor.WriteMessage($"\n[WEAM Error] Ошибка сохранения штампа: {ex.Message}\n");
                }
            }
        }
    }
}