// ============================================================================
// OOXGN - Copyright (c) 2026. All rights reserved.
// Author: OOXGN | Project: BotPlusAutocadTool
// SPDX-License-Identifier: AGPL-3.0-only
// ============================================================================

// File: BotPlusAutocadTool/Views/StampWindow.xaml.cs
using System.Windows;
using System;
using System.Linq;
using System.IO;
using BotPlusAutocadTool.Models;
using BotPlusAutocadTool.Services;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace BotPlusAutocadTool.Views
{
    public partial class StampWindow : Window
    {
        public StampWindow(ViewModels.StampPropsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void WriteDrawing(object sender, RoutedEventArgs args)
        {
            var targets = AcadApp.DocumentManager.Cast<Autodesk.AutoCAD.ApplicationServices.Document>()
                .Select(document => new StampTransferTarget
                {
                    Name = Path.GetFileName(document.Name), Location = document.Name,
                    Write = properties => StampDrawingTransfer.Write(document, properties)
                }).ToList();
            new StampTransferWindow((ViewModels.StampPropsViewModel)DataContext, targets, false) { Owner = this }.ShowDialog();
        }

        private void WriteWord(object sender, RoutedEventArgs args)
        {
            try
            {
                var targets = WordStampTransfer.GetOpenDocuments();
                new StampTransferWindow((ViewModels.StampPropsViewModel)DataContext, targets, true) { Owner = this }.ShowDialog();
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Записать в Word"); }
        }
    }
}
