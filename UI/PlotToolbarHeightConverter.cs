using System;
using System.Globalization;
using System.Windows.Data;

namespace BotPlusAutocadTool.UI
{
    public sealed class PlotToolbarHeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is double height && height > 0 ? height * 0.4 : double.PositiveInfinity;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }
}
