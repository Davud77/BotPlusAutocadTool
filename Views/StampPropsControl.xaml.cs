// ============================================================================
// OOXGN - Copyright (c) 2026. All rights reserved.
// Author: OOXGN | Project: BotPlusAutocadTool
// SPDX-License-Identifier: AGPL-3.0-only
// ============================================================================

// File: BotPlusAutocadTool/Views/StampWindow.xaml.cs
using System.Windows;

namespace BotPlusAutocadTool.Views
{
    public partial class StampWindow : Window
    {
        public StampWindow(ViewModels.StampPropsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}