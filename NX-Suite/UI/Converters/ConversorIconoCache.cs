using NX_Suite.Core;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace NX_Suite.UI.Converters
{
    /// <summary>
    /// Convierte una URL de icono en un <see cref="BitmapImage"/>.
    /// Si el icono ya está en caché local lo carga desde disco;
    /// de lo contrario lo carga desde la red y lanza una descarga
    /// en background para que la próxima vez se sirva desde disco.
    /// </summary>
    public class ConversorIconoCache : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string url || string.IsNullOrWhiteSpace(url))
                return null;

            try
            {
                string? rutaLocal = Servicios.Iconos.ObtenerRutaLocal(url);

                if (rutaLocal != null)
                {
                    // Existe en caché: cargamos desde disco
                    var bmpLocal = new BitmapImage();
                    bmpLocal.BeginInit();
                    bmpLocal.UriSource    = new Uri(rutaLocal);
                    bmpLocal.CacheOption  = BitmapCacheOption.OnLoad;
                    bmpLocal.EndInit();
                    bmpLocal.Freeze();
                    return bmpLocal;
                }

                // No está en caché: WPF lo descarga desde la URL
                // y lanzamos la descarga a disco en background
                _ = Servicios.Iconos.DescargarSiNoExisteAsync(url);

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(url);
                bmp.EndInit();
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
