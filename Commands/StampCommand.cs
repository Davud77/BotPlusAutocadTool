// ============================================================================
// OOXGN - Copyright (c) 2026. All rights reserved.
// Author: OOXGN | Project: BotPlusAutocadTool
// SPDX-License-Identifier: AGPL-3.0-only
// ============================================================================

using System;
using System.Windows.Interop; // Необходимо для связи WPF окна с окном AutoCAD
using Autodesk.AutoCAD.Runtime;
using BotPlusAutocadTool.Services;
using BotPlusAutocadTool.ViewModels;
using BotPlusAutocadTool.Views;

// Псевдоним для Application
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace BotPlusAutocadTool.Commands
{
    public class StampCommand
    {
        // Статическая ссылка для реализации Singleton (одно окно)
        private static StampWindow _stampWindow;

        [CommandMethod("WEAM_STAMP", CommandFlags.Session)]
        // CommandFlags.Session важно для Modeless Window
        public void ShowStampWindow()
        {
            // Если окно уже открыто - активируем его и выходим
            if (_stampWindow != null && _stampWindow.IsLoaded)
            {
                _stampWindow.Activate();
                return;
            }

            // Инициализация сервисов и VM
            var autoCadService = new AutoCADService();
            var viewModel = new StampPropsViewModel(autoCadService);

            // Загрузка данных при старте
            viewModel.LoadValues();

            _stampWindow = new StampWindow(viewModel);

            // --- ИСПРАВЛЕНИЕ CS0117 ---
            // Правильная привязка владельца окна (Owner) к основному окну AutoCAD
            // Это позволяет окну сворачиваться вместе с CAD и всегда быть поверх него
            if (AcadApp.MainWindow != null)
            {
                var helper = new WindowInteropHelper(_stampWindow);
                helper.Owner = AcadApp.MainWindow.Handle;
            }

            // Обработка закрытия, чтобы сбросить ссылку
            _stampWindow.Closed += (s, e) => _stampWindow = null;

            _stampWindow.Show();
        }
    }
}