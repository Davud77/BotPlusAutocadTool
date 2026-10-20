// ============================================================================
// OOXGN - Copyright (c) 2026. All rights reserved.
// Author: OOXGN | Project: BotPlusAutocadTool
// SPDX-License-Identifier: AGPL-3.0-only
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using BotPlusAutocadTool.Models;
using BotPlusAutocadTool.ViewModels;
using BotPlusAutocadTool.Views;

namespace BotPlusAutocadTool.Commands
{
    /// <summary>
    /// Команда автонумерации для текстов, мультитекстов, атрибутов блоков и мультивыносок
    /// </summary>
    public sealed class AutoNumCommand
    {
        #region Constants

        private const string DxfText = "TEXT";
        private const string DxfMText = "MTEXT";
        private const string DxfMLeader = "MULTILEADER";
        private const string DxfBlock = "INSERT";

        #endregion

        #region Command Entry Point

        [CommandMethod("AUTONUM", CommandFlags.UsePickSet | CommandFlags.Modal)]
        [CommandMethod("НУМ", CommandFlags.UsePickSet | CommandFlags.Modal)]
        public void ExecuteAutoNum()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var ed = doc.Editor;
            var db = doc.Database;

            try
            {
                // Загружаем настройки
                var settings = LoadSettings(ed);

                // Показываем диалог настроек
                var viewModel = new AutoNumSettingsViewModel();
                bool dialogAccepted = ShowSettingsDialog(doc, viewModel, ed, settings);

                if (!dialogAccepted)
                {
                    ed.WriteMessage("\nОтменено пользователем.");
                    return;
                }

                settings = viewModel.Settings;
                settings.Save();

                // Получаем выборку объектов
                var selectedObjects = GetSelection(ed, settings);
                if (selectedObjects == null || selectedObjects.Count == 0)
                {
                    ed.WriteMessage("\nОбъекты не выбраны.");
                    return;
                }

                // Выполняем нумерацию
                using var selection = selectedObjects;
                int numberedCount = new Services.NumberingService().Number(db, ed, selection.GetObjectIds(), settings);
                ed.WriteMessage($"\nПронумеровано объектов: {numberedCount}");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\nОшибка: {ex.Message}");
            }
        }

        #endregion

        #region Settings

        private AutoNumSettings LoadSettings(Editor ed)
        {
            try
            {
                return AutoNumSettings.Load();
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\nПредупреждение: не удалось загрузить настройки. {ex.Message}");
                return new AutoNumSettings();
            }
        }

        private bool ShowSettingsDialog(Document doc, AutoNumSettingsViewModel viewModel, Editor ed, AutoNumSettings fallbackSettings)
        {
            try
            {
                using (doc.LockDocument())
                {
                    var window = new AutoNumSettingsWindow(viewModel);
                    return Application.ShowModalWindow(window) == true;
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\nОшибка открытия окна настроек: {ex.Message}");
                // Fallback: получаем настройки через командную строку
                if (GetSettingsFromConsole(ed, fallbackSettings))
                {
                    viewModel.Settings = fallbackSettings;
                    fallbackSettings.Save();
                    return true;
                }
                return false;
            }
        }

        #endregion

        #region Selection

        private SelectionSet GetSelection(Editor ed, AutoNumSettings settings)
        {
            // Проверяем предварительную выборку
            var psr = ed.SelectImplied();
            if (psr.Status == PromptStatus.OK && psr.Value.Count > 0)
                return psr.Value;

            // Запрашиваем выбор у пользователя
            var pso = new PromptSelectionOptions
            {
                MessageForAdding = GetSelectionPrompt(settings)
            };

            var filter = CreateSelectionFilter(settings);
            psr = ed.GetSelection(pso, filter);

            return psr.Status == PromptStatus.OK ? psr.Value : null;
        }

        private string GetSelectionPrompt(AutoNumSettings settings)
        {
            return settings.Mode switch
            {
                AutoNumSettings.NumberingMode.Text => "\nВыберите тексты (TEXT/MTEXT) для нумерации: ",
                AutoNumSettings.NumberingMode.MLeader => "\nВыберите мультивыноски (MLEADER) для нумерации: ",
                AutoNumSettings.NumberingMode.Attribute => $"\nВыберите блоки с атрибутом '{settings.AttributeTag}' для нумерации: ",
                AutoNumSettings.NumberingMode.Table => $"\nВыберите таблицы: строка {settings.TableRow}, столбец {settings.TableColumn}: ",
                _ => "\nВыберите объекты для нумерации: "
            };
        }

        private SelectionFilter CreateSelectionFilter(AutoNumSettings settings)
        {
            TypedValue[] filterValues = settings.Mode switch
            {
                AutoNumSettings.NumberingMode.Table => new[] { new TypedValue((int)DxfCode.Start, "ACAD_TABLE") },
                AutoNumSettings.NumberingMode.Text => new[]
                {
                    new TypedValue((int)DxfCode.Operator, "<OR"),
                    new TypedValue((int)DxfCode.Start, DxfText),
                    new TypedValue((int)DxfCode.Start, DxfMText),
                    new TypedValue((int)DxfCode.Operator, "OR>")
                },
                AutoNumSettings.NumberingMode.MLeader => new[]
                {
                    new TypedValue((int)DxfCode.Start, DxfMLeader)
                },
                AutoNumSettings.NumberingMode.Attribute => new[]
                {
                    new TypedValue((int)DxfCode.Start, DxfBlock)
                },
                _ => Array.Empty<TypedValue>()
            };

            return new SelectionFilter(filterValues);
        }

        #endregion

        #region Console Fallback

        private bool GetSettingsFromConsole(Editor ed, AutoNumSettings settings)
        {
            // Prefix
            var pr = ed.GetString(new PromptStringOptions("\nПрефикс [<none>]: ")
            {
                AllowSpaces = true,
                DefaultValue = settings.Prefix ?? string.Empty,
                UseDefaultValue = true
            });
            if (pr.Status != PromptStatus.OK) return false;
            settings.Prefix = pr.StringResult ?? string.Empty;

            // Suffix
            pr = ed.GetString(new PromptStringOptions("\nСуффикс [<none>]: ")
            {
                AllowSpaces = true,
                DefaultValue = settings.Suffix ?? string.Empty,
                UseDefaultValue = true
            });
            if (pr.Status != PromptStatus.OK) return false;
            settings.Suffix = pr.StringResult ?? string.Empty;

            // Start number
            var pir = ed.GetInteger(new PromptIntegerOptions($"\nНачальный номер <{settings.StartNumber}>: ")
            {
                DefaultValue = settings.StartNumber,
                UseDefaultValue = true,
                AllowNegative = false
            });
            if (pir.Status != PromptStatus.OK) return false;
            settings.StartNumber = pir.Value;

            // Step
            pir = ed.GetInteger(new PromptIntegerOptions($"\nШаг <{settings.Step}>: ")
            {
                DefaultValue = settings.Step,
                UseDefaultValue = true,
                AllowNegative = false
            });
            if (pir.Status != PromptStatus.OK) return false;
            settings.Step = pir.Value;

            // Digits
            pir = ed.GetInteger(new PromptIntegerOptions($"\nКоличество знаков (0=без заполнения) <{settings.Digits}>: ")
            {
                DefaultValue = settings.Digits,
                UseDefaultValue = true,
                AllowNegative = false
            });
            if (pir.Status != PromptStatus.OK) return false;
            settings.Digits = pir.Value;

            // Order
            var pko = new PromptKeywordOptions($"\nПорядок [Pick/Geometric] <{settings.Order}>: ");
            pko.Keywords.Add("Pick");
            pko.Keywords.Add("Geometric");
            pko.Keywords.Default = settings.Order.ToString();
            pko.AllowNone = true;
            pr = ed.GetKeywords(pko);
            if (pr.Status == PromptStatus.OK)
            {
                settings.Order = pr.StringResult == "Geometric"
                    ? AutoNumSettings.NumberingOrder.Geometric
                    : AutoNumSettings.NumberingOrder.Pick;
            }

            // Mode
            pko = new PromptKeywordOptions($"\nРежим [Text/MLeader/Attribute/Table] <{settings.Mode}>: ");
            pko.Keywords.Add("Text");
            pko.Keywords.Add("MLeader");
            pko.Keywords.Add("Attribute");
            pko.Keywords.Add("Table");
            pko.Keywords.Default = settings.Mode.ToString();
            pko.AllowNone = true;
            pr = ed.GetKeywords(pko);
            if (pr.Status == PromptStatus.OK)
            {
                settings.Mode = pr.StringResult switch
                {
                    "MLeader" => AutoNumSettings.NumberingMode.MLeader,
                    "Attribute" => AutoNumSettings.NumberingMode.Attribute,
                    "Table" => AutoNumSettings.NumberingMode.Table,
                    _ => AutoNumSettings.NumberingMode.Text
                };
            }

            // Attribute tag
            if (settings.Mode == AutoNumSettings.NumberingMode.Attribute)
            {
                pr = ed.GetString(new PromptStringOptions($"\nТег атрибута <{settings.AttributeTag}>: ")
                {
                    DefaultValue = settings.AttributeTag,
                    UseDefaultValue = true,
                    AllowSpaces = false
                });
                if (pr.Status != PromptStatus.OK) return false;
                settings.AttributeTag = pr.StringResult;
            }

            if (settings.Mode == AutoNumSettings.NumberingMode.Table)
            {
                pir = ed.GetInteger(new PromptIntegerOptions("\nСтрока ячейки (от 1): ") { DefaultValue = settings.TableRow, UseDefaultValue = true, AllowNegative = false, AllowZero = false });
                if (pir.Status != PromptStatus.OK) return false;
                settings.TableRow = pir.Value;
                pir = ed.GetInteger(new PromptIntegerOptions("\nСтолбец ячейки (от 1): ") { DefaultValue = settings.TableColumn, UseDefaultValue = true, AllowNegative = false, AllowZero = false });
                if (pir.Status != PromptStatus.OK) return false;
                settings.TableColumn = pir.Value;
            }
            settings.Normalize();
            return true;
        }

        #endregion

    }
}
