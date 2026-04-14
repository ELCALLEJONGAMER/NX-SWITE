using System;
using System.Windows.Data;
using System.Windows.Media;

namespace NX_Suite
{
    public class HexToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is string hex && !string.IsNullOrEmpty(hex))
            {
                try { return (SolidColorBrush)new BrushConverter().ConvertFrom(hex); }
                catch { return Brushes.Transparent; }
            }
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => null;
    }
}