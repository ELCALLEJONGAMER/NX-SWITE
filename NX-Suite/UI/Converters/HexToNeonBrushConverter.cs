using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace NX_Swite.UI.Converters
{
    /// <summary>
    /// Convierte un color hex (ej. "#00D2FF") en un <see cref="LinearGradientBrush"/>
    /// rotatorio de 3 paradas, con el mismo patrón que <c>PincelNeonRotatorio</c>
    /// (ColoresGlobales.xaml) pero parametrizado por color. Permite que cada
    /// mundo del menú use su propio <c>ColorNeon</c> (definido en el Gist) para
    /// el halo/borde neon, en vez de un color fijo compartido.
    ///
    /// El brush devuelto NO se congela (no se llama Freeze()) para poder animar
    /// su <c>RelativeTransform</c> (RotateTransform.Angle) en runtime — un brush
    /// congelado no admite animaciones de sus propiedades.
    /// </summary>
    public class HexToNeonBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Color color = ParsearColor(value as string);

            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint   = new Point(1, 1),
                RelativeTransform = new RotateTransform(0, 0.5, 0.5),
            };

            brush.GradientStops.Add(new GradientStop(color, 0.0));
            brush.GradientStops.Add(new GradientStop(MezclarConPurpura(color), 0.5));
            brush.GradientStops.Add(new GradientStop(color, 1.0));

            return brush;
        }

        private static Color ParsearColor(string? hex)
        {
            try
            {
                return (Color)ColorConverter.ConvertFromString(string.IsNullOrWhiteSpace(hex) ? "#00D2FF" : hex);
            }
            catch
            {
                return (Color)ColorConverter.ConvertFromString("#00D2FF");
            }
        }

        /// <summary>Mezcla ligera hacia #BD00FF para lograr el mismo efecto "duotono" que PincelNeonRotatorio.</summary>
        private static Color MezclarConPurpura(Color c)
        {
            byte r = (byte)((c.R + 0xBD) / 2);
            byte g = (byte)(c.G / 2);
            byte b = (byte)((c.B + 0xFF) / 2);
            return Color.FromRgb(r, g, b);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
