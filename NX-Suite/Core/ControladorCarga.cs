using NX_Suite.Models;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NX_Suite.Core
{
    public class ControladorCarga
    {
        private static readonly Brush BrushGris   = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#707080"));
        private static readonly Brush BrushCyan   = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00D2FF"));
        private static readonly Brush BrushMorado = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BD00FF"));

        private const double AnchoMaximoBarra = 466;

        private readonly UIElement _overlayCarga;
        private readonly TextBlock _txtSubtitulo;
        private readonly TextBlock _txtDetalle;
        private readonly TextBlock _txtPorcentaje;
        private readonly FrameworkElement _barraProgreso;
        private readonly TextBlock _paso1;
        private readonly TextBlock _paso2;
        private readonly TextBlock _paso3;
        private readonly TextBlock _paso4;

        public ControladorCarga(
            UIElement overlay,
            TextBlock subtitulo,
            TextBlock detalle,
            TextBlock porcentaje,
            FrameworkElement barra,
            TextBlock p1, TextBlock p2, TextBlock p3, TextBlock p4)
        {
            _overlayCarga   = overlay;
            _txtSubtitulo   = subtitulo;
            _txtDetalle     = detalle;
            _txtPorcentaje  = porcentaje;
            _barraProgreso  = barra;
            _paso1 = p1; _paso2 = p2; _paso3 = p3; _paso4 = p4;
        }

        public void Mostrar(string tituloPrincipal)
        {
            _txtSubtitulo.Text  = tituloPrincipal.ToUpper();
            _txtDetalle.Text    = "Preparando...";
            _txtPorcentaje.Text = "0%";
            _barraProgreso.Width = 0;

            ActualizarColoresPasos(0);
            _overlayCarga.Visibility = Visibility.Visible;
        }

        public void Ocultar()
        {
            _overlayCarga.Visibility = Visibility.Collapsed;
        }

        public IProgress<EstadoProgreso> ObtenerReportador()
        {
            return new Progress<EstadoProgreso>(estado =>
            {
                _txtDetalle.Text    = estado.TareaActual;
                _txtPorcentaje.Text = $"{estado.Porcentaje:F0}%";

                double nuevoAncho = estado.Porcentaje / 100.0 * AnchoMaximoBarra;
                _barraProgreso.BeginAnimation(
                    FrameworkElement.WidthProperty,
                    new DoubleAnimation(nuevoAncho, TimeSpan.FromMilliseconds(150)));

                ActualizarColoresPasos(estado.PasoActual);
            });
        }

        private void ActualizarColoresPasos(int pasoActivo)
        {
            _paso1.Foreground = pasoActivo == 1 ? BrushCyan : pasoActivo > 1 ? BrushMorado : BrushGris;
            _paso1.Text = pasoActivo > 1 ? "✅ 1. Descarga completa" : "1. Descargando paquete";

            _paso2.Foreground = pasoActivo == 2 ? BrushCyan : pasoActivo > 2 ? BrushMorado : BrushGris;
            _paso2.Text = pasoActivo > 2 ? "✅ 2. Archivos extraídos" : "2. Extrayendo archivos";

            _paso3.Foreground = pasoActivo == 3 ? BrushCyan : pasoActivo > 3 ? BrushMorado : BrushGris;
            _paso3.Text = pasoActivo > 3 ? "✅ 3. SD Actualizada" : "3. Aplicando en SD";

            _paso4.Foreground = pasoActivo == 4 ? BrushCyan : pasoActivo > 4 ? BrushMorado : BrushGris;
            _paso4.Text = pasoActivo > 4 ? "✅ 4. Limpieza terminada" : "4. Limpiando temporales";
        }
    }
}