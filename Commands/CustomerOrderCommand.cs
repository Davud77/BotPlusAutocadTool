using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using System;
using System.Windows.Interop;
using BotPlusAutocadTool.Services;
using BotPlusAutocadTool.ViewModels;
using BotPlusAutocadTool.Views;

// Псевдоним
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace BotPlusAutocadTool.Commands
{
    public sealed class CustomerOrderCommand
    {
        [CommandMethod("WEAM_CUSTOMERORDER")]
        public void Run()
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            try
            {
                var vm = new CustomerOrderViewModel();
                var win = new CustomerOrderWindow { DataContext = vm };

                // Привязка Owner к окну AutoCAD (чтобы модалка была нормальной)
                new WindowInteropHelper(win).Owner = AcadApp.MainWindow.Handle;

                AcadApp.ShowModalWindow(win);

                if (win.DialogResult != true)
                    return;

                var options = vm.BuildOptions();
                var service = new CustomerOrderService();
                service.Execute(doc, options);
            }
            catch (System.Exception ex)
            {
                doc.Editor.WriteMessage($"\n[WEAM CustomerOrder] Ошибка: {ex}\n");
            }
        }
    }
}
