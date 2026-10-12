// ============================================================================
// OOXGN - Copyright (c) 2026. All rights reserved.
// Author: OOXGN | Project: BotPlusAutocadTool
// ============================================================================

// File: BotPlusAutocadTool/App.cs

using System;
using System.Reflection;
using System.Windows.Input;
using System.Windows.Media.Imaging;

using Autodesk.AutoCAD.Runtime;
using Autodesk.Windows;
using Autodesk.AutoCAD.Windows;

using WpfOrientation = System.Windows.Controls.Orientation;

// Псевдоним для Application
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: ExtensionApplication(typeof(BotPlusAutocadTool.App))]

namespace BotPlusAutocadTool
{
    public class App : IExtensionApplication
    {
        private const string RibbonTabId = "BOTPLUS_AUTOCAD_TOOL_TAB";
        private const string RibbonTabTitle = "BotPlusAutocadTool";

        private static ContextMenuExtension _contextMenu;
        private DateTime _nextUiCheckUtc;

        public void Initialize()
        {
            try { Services.SelectionLengthService.Start(); } catch (System.Exception ex) { LogStartupError(ex); }
            try { Services.UpdateCheckService.Start(); } catch (System.Exception ex) { LogStartupError(ex); }
            AcadApp.Idle += Application_Idle;
        }

        public void Terminate()
        {
            Services.UpdateCheckService.Stop();
            Services.SelectionLengthService.Stop();
            Services.DsdPublishService.Shutdown();
            AcadApp.Idle -= Application_Idle;
            if (_contextMenu != null)
            {
                AcadApp.RemoveDefaultContextMenuExtension(_contextMenu);
                _contextMenu = null;
            }
        }

        private void Application_Idle(object sender, EventArgs e)
        {
            if (DateTime.UtcNow < _nextUiCheckUtc) return;
            _nextUiCheckUtc = DateTime.UtcNow.AddSeconds(1);
            // Keep checking: Ribbon may not exist on the first Idle, and workspaces can rebuild it.
            CreateRibbonSafe();
            if (_contextMenu == null) CreateContextMenuSafe();
        }

        private static void LogStartupError(System.Exception ex)
        {
            AcadApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\n[BotPlus] Ошибка дополнительного сервиса: " + ex.Message + "\n");
        }

        private void CreateRibbonSafe()
        {
            try
            {
                CreateRibbon();
            }
            catch (System.Exception ex)
            {
                AcadApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                    $"\n[WEAM Error] Ошибка создания ленты: {ex.Message}\n");
            }
        }

        private void CreateContextMenuSafe()
        {
            try
            {
                CreateContextMenu();
            }
            catch (System.Exception ex)
            {
                AcadApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                    $"\n[WEAM Error] Ошибка создания контекстного меню: {ex.Message}\n");
            }
        }

        private void CreateContextMenu()
        {
            _contextMenu = new ContextMenuExtension();
            _contextMenu.Title = "BotPlusAutocadTool";

            var menuItem = new MenuItem("BotPlus: выбрать по образцу");
            menuItem.Click += OnSelectSimilarClick;
            _contextMenu.MenuItems.Add(menuItem);

            AcadApp.AddDefaultContextMenuExtension(_contextMenu);
        }

        private void OnSelectSimilarClick(object sender, EventArgs e)
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc != null)
                doc.SendStringToExecute("WSELECT ", true, false, false);
        }

        private void CreateRibbon()
        {
            var ribbon = ComponentManager.Ribbon;
            if (ribbon == null) return;

            var tab = ribbon.FindTab(RibbonTabId);
            if (tab == null)
            {
                tab = new RibbonTab { Id = RibbonTabId, Name = RibbonTabTitle, Title = RibbonTabTitle };
                ribbon.Tabs.Add(tab);
            }
            else if (tab.Panels.Count > 0)
            {
                return;
            }
            tab.IsActive = true;

            // =========================
            // ПАНЕЛЬ: ИНСТРУМЕНТЫ
            // =========================
            var toolsPanelSource = new RibbonPanelSource { Title = "Инструменты" };
            var toolsPanel = new RibbonPanel { Source = toolsPanelSource };
            tab.Panels.Add(toolsPanel);

            AddButton(toolsPanelSource, "Преобразовать\nв аксонометрию", "axon.png", "Align_Den");
            AddButton(toolsPanelSource, "Автонумерация", "autonum.png", "AUTONUM");
            AddButton(toolsPanelSource, "Выбор по\nобразцу", "selectsim.png", "WSELECT");
            AddButton(toolsPanelSource, "Сумма\nдлин", "sumlen.png", "SUMLENGTH");
            AddButton(toolsPanelSource, "Мульти-\nкопирование", "multicopy.png", "MULTICOPY");
            AddButton(toolsPanelSource, "Подготовка\nпланов", "planprep.png", "PLANPREP");
            AddButton(toolsPanelSource, "Таблица\nExcel", "tableedit.png", "WEAM_TABLEEXCEL");


            // =========================
            // ПАНЕЛЬ: ОЧИСТКА И СБРОС
            // =========================
            var cleanPanelSource = new RibbonPanelSource { Title = "Очистка и сброс" };
            var cleanPanel = new RibbonPanel { Source = cleanPanelSource };
            tab.Panels.Add(cleanPanel);

            AddButton(cleanPanelSource, "Очистка\nчертежа", "clean.png", "CLEANDWG");
            AddButton(cleanPanelSource, "Очистить\nневыбранные", "clean_invert.png", "CLEANUNSELECTED");
            AddButton(cleanPanelSource, "Сброс\nтекста", "srtimtext.png", "STRIPTEXT");

            // =========================
            // ПАНЕЛЬ: ВЫПУСК
            // =========================
            var releasePanelSource = new RibbonPanelSource { Title = "Выпуск" };
            var releasePanel = new RibbonPanel { Source = releasePanelSource };
            tab.Panels.Add(releasePanel);

            AddButton(releasePanelSource, "Штамп", "stamp.png", "WEAM_STAMP");
            AddButton(releasePanelSource, "Печать", "wplot.png", "WEAM_BATCHPLOT");
            AddButton(releasePanelSource, "Подготовить\nвсе блоки", "wplot.png", "WEAM_BLOCKS_STAGE1");
            AddButton(releasePanelSource, "Сформировать\nзаказчику", "dwgexport.png", "WEAM_CUSTOMERORDER");

            // =========================
            // ПАНЕЛЬ: СПРАВКА
            // =========================
            var helpPanelSource = new RibbonPanelSource { Title = "Справка" };
            var helpPanel = new RibbonPanel { Source = helpPanelSource };
            tab.Panels.Add(helpPanel);

            AddButton(helpPanelSource, "Инфо", "info.png", "WEAM_INFO");
            AddButton(helpPanelSource, "Проверить\nобновления", "info.png", "BOTPLUS_CHECKUPDATES");
        }

        private void AddButton(RibbonPanelSource source, string text, string iconName, string commandName)
        {
            var button = new RibbonButton
            {
                Text = text,
                ShowText = true,
                ShowImage = true,
                Size = RibbonItemSize.Large,
                Orientation = WpfOrientation.Vertical,
                LargeImage = GetIcon(iconName),
                CommandHandler = new RibbonCommand(commandName)
            };
            source.Items.Add(button);
        }

        private System.Windows.Media.ImageSource GetIcon(string fileName)
        {
            string assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
            string uriPath = $"pack://application:,,,/{assemblyName};component/Resources/{fileName}";

            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(uriPath, UriKind.Absolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch (System.Exception ex)
            {
                AcadApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                    $"\n[WEAM Icon] Не загрузилась иконка '{fileName}' по URI: {uriPath}\nПричина: {ex.Message}\n");
                return null;
            }
        }
    }

    public class RibbonCommand : ICommand
    {
        private readonly string _commandName;
        public RibbonCommand(string commandName) => _commandName = commandName;

        public event EventHandler CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object parameter) => true;

        public void Execute(object parameter)
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc != null && !string.IsNullOrWhiteSpace(_commandName))
                doc.SendStringToExecute($"{_commandName} ", true, false, false);
        }
    }
}
