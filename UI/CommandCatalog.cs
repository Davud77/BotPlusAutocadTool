// SPDX-License-Identifier: AGPL-3.0-only
namespace BotPlusAutocadTool.UI
{
    internal sealed record ButtonDefinition(string Title, string Icon, string Command, string Description);
    internal sealed record PanelDefinition(string Title, ButtonDefinition[] Buttons);

    internal static class CommandCatalog
    {
        internal const string TabId = "BOTPLUS_AUTOCAD_TOOL_TAB";
        internal const string TabTitle = "BotPlus AT";

        internal static readonly PanelDefinition[] Panels =
        {
            new("Инструменты", new[]
            {
                new ButtonDefinition("Аксонометрия", "axon.png", "Align_Den", "Преобразовать выбранные объекты в аксонометрическую проекцию."),
                new ButtonDefinition("Автонумерация", "autonum.png", "AUTONUM", "Пронумеровать текст, атрибуты или выноски в выбранном порядке."),
                new ButtonDefinition("Выбор по\nобразцу", "selectsim.png", "WSELECT", "Найти объекты по свойствам выбранного образца."),
                new ButtonDefinition("Сумма\nдлин", "sumlen.png", "SUMLENGTH", "Длина, площадь и состав выделения с настройками единиц и масштаба."),
                new ButtonDefinition("Мульти-\nкопирование", "multicopy.png", "MULTICOPY", "Создать несколько копий выбранных объектов."),
                new ButtonDefinition("Подготовка\nпланов", "planprep.png", "PLANPREP", "Подготовить выбранные объекты плана по заданным параметрам."),
                new ButtonDefinition("Таблица\nExcel", "tableedit.png", "WEAM_TABLEEXCEL", "Отредактировать таблицу в XLSX и вернуть изменения в чертёж."),
                new ButtonDefinition("Экспорт\nExcel", "tableedit.png", "BOTPLUS_EXPORTEXCEL", "Сохранить выбранную TABLE в XLSX."),
                new ButtonDefinition("Импорт\nExcel", "tableedit.png", "BOTPLUS_IMPORTEXCEL", "Создать TABLE из XLSX или обновить существующую таблицу."),
                new ButtonDefinition("Дополнительные\nфункции ▾", "info.png", "BOTPLUS_ADDITIONAL", "Дополнительные функции по категориям из встроенной библиотеки.")
            }),
            new("Очистка и сброс", new[]
            {
                new ButtonDefinition("Очистка\nчертежа", "clean.png", "CLEANDWG", "Удалить выбранные категории лишних данных чертежа."),
                new ButtonDefinition("Очистить\nневыбранные", "clean_invert.png", "CLEANUNSELECTED", "Сохранить выбранные объекты, удалив остальные из заданных пространств."),
                new ButtonDefinition("Сброс\nтекста", "srtimtext.png", "STRIPTEXT", "Снять выбранные переопределения текстового оформления.")
            }),
            new("Выпуск", new[]
            {
                new ButtonDefinition("Штамп", "stamp.png", "WEAM_STAMP", "Заполнить пользовательские свойства чертежа для штампа."),
                new ButtonDefinition("Вставить\nформатку", "wplot.png", "BOTPLUS_INSERTFORMAT", "Вставить динамический блок WM_FORMAT с атрибутами."),
                new ButtonDefinition("Печать", "wplot.png", "WEAM_BATCHPLOT", "Выбрать форматки и выпустить PDF со сшивкой или по листам."),
                new ButtonDefinition("Сформировать\nзаказчику", "dwgexport.png", "WEAM_CUSTOMERORDER", "Подготовить чертёж или отдельную копию для передачи заказчику.")
            }),
            new("Сервис", new[]
            {
                new ButtonDefinition("Сервис", "info.png", "WEAM_INFO", "Настройки, проверка обновлений и справка о плагине.")
            })
        };
    }
}
