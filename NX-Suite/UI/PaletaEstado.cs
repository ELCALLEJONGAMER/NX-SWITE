using System.Windows.Media;

namespace NX_Swite.UI
{
    /// <summary>
    /// Pinceles de estado (éxito, error, aviso, neutro, en curso) usados desde código.
    /// Congelados para poder compartirse sin coste.
    /// </summary>
    internal static class PaletaEstado
    {
        public static readonly SolidColorBrush Verde   = Crear(0x4C, 0xAF, 0x50);
        public static readonly SolidColorBrush Rojo    = Crear(0xFF, 0x55, 0x55);
        public static readonly SolidColorBrush Ambar   = Crear(0xFF, 0xD5, 0x4A);
        public static readonly SolidColorBrush Gris    = Crear(0x70, 0x70, 0x80);
        public static readonly SolidColorBrush Cian    = Crear(0x00, 0xD2, 0xFF);
        public static readonly SolidColorBrush Apagado = Crear(0x50, 0x50, 0x60);

        private static SolidColorBrush Crear(byte r, byte g, byte b)
        {
            var pincel = new SolidColorBrush(Color.FromRgb(r, g, b));
            pincel.Freeze();
            return pincel;
        }
    }
}
