// ============================================================================
// OOXGN - Copyright (c) 2026. All rights reserved.
// Author: OOXGN | Project: BotPlusAutocadTool
// SPDX-License-Identifier: AGPL-3.0-only
// ============================================================================

using Autodesk.AutoCAD.Runtime;
using BotPlusAutocadTool.ViewModels;
using BotPlusAutocadTool.Views;

namespace BotPlusAutocadTool.Commands
{
    public sealed class SumLengthCommand
    {
        private static SumLengthWindow _window;

        [CommandMethod("SUMLENGTH")]
        public void ShowWindow()
        {
            // Если окно уже открыто - просто активируем его
            if (_window != null && _window.IsLoaded)
            {
                _window.Activate();
                // Обновляем данные при активации
                if (_window.DataContext is SumLengthViewModel vm)
                {
                    vm.CalculateFromCurrentSelection();
                }
                return;
            }

            // Создаем ViewModel и окно
            var viewModel = new SumLengthViewModel();
            _window = new SumLengthWindow { DataContext = viewModel };

            // Обнуляем ссылку при закрытии
            _window.Closed += (s, e) => _window = null;

            _window.Show();
        }
    }
}