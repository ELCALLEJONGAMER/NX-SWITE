using System;
using System.Windows;
using System.Windows.Media;

namespace NX_Swite
{
    /// <summary>
    /// MainWindow — Adaptación del catálogo a anchos reducidos (portátiles 1366×768, escalado 125–150 %).
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>Ancho de una tarjeta del catálogo (180 + margen 15×2).</summary>
        private const double AnchoTarjetaCatalogo = 210;

        /// <summary>
        /// En áreas estrechas encoge las tarjetas del catálogo (hasta 70 %) para ganar una columna
        /// en lugar de dejar huecos grandes; en pantallas anchas se mantienen a tamaño real.
        /// </summary>
        private void VistaCatalogo_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            double ancho = e.NewSize.Width - SystemParameters.VerticalScrollBarWidth;
            if (ancho <= 0) return;
            int columnasReales = Math.Max(1, (int)(ancho / AnchoTarjetaCatalogo));
            double escala = 1.0;
            if (columnasReales < 5)
                escala = Math.Max(0.7, ancho / ((columnasReales + 1) * AnchoTarjetaCatalogo));
            if (escala > 0.98) escala = 1.0;
            if (CatalogoModulos.LayoutTransform is ScaleTransform actual && Math.Abs(actual.ScaleX - escala) < 0.01) return;
            CatalogoModulos.LayoutTransform = new ScaleTransform(escala, escala);
        }
    }
}
