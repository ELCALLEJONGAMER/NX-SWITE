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
        private UIElement _overlayCarga;
        private TextBlock _txtSubtitulo, _txtDetalle, _txtPorcentaje;
        private FrameworkElement _barraProgreso;
        private TextBlock _paso1, _paso2, _paso3, _paso4;

        public ControladorCarga(UIElement overlay, TextBlock subtitulo, TextBlock detalle, TextBlock porcentaje,
                                FrameworkElement barra, TextBlock p1, TextBlock p2, TextBlock p3, TextBlock p4)
        {
            _overlayCarga = overlay;
            _txtSubtitulo = subtitulo;
            _txtDetalle = detalle;
            _txtPorcentaje = porcentaje;
            _barraProgreso = barra;
            _paso1 = p1; _paso2 = p2; _paso3 = p3; _paso4 = p4;
        }

        public void Mostrar(string tituloPrincipal)
        {
            _txtSubtitulo.Text = tituloPrincipal.ToUpper();
            _txtDetalle.Text = "Preparando...";
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
                _txtDetalle.Text = estado.TareaActual;
                _txtPorcentaje.Text = $"{estado.Porcentaje:F0}%";

                double anchoMaximo = 466;
                double nuevoAncho = estado.Porcentaje / 100.0 * anchoMaximo;

                DoubleAnimation animacionBarra = new DoubleAnimation(nuevoAncho, TimeSpan.FromMilliseconds(150));
                _barraProgreso.BeginAnimation(FrameworkElement.WidthProperty, animacionBarra);

                ActualizarColoresPasos(estado.PasoActual);
            });
        }

        private void ActualizarColoresPasos(int pasoActivo)
        {
            var gris = (Brush)new BrushConverter().ConvertFrom("#707080");
            var cyan = (Brush)new BrushConverter().ConvertFrom("#00D2FF");
            var morado = (Brush)new BrushConverter().ConvertFrom("#BD00FF");

            _paso1.Foreground = pasoActivo == 1 ? cyan : pasoActivo > 1 ? morado : gris;
            _paso1.Text = pasoActivo > 1 ? "✅ 1. Descarga completa" : "1. Descargando paquete";

            _paso2.Foreground = pasoActivo == 2 ? cyan : pasoActivo > 2 ? morado : gris;
            _paso2.Text = pasoActivo > 2 ? "✅ 2. Archivos extraídos" : "2. Extrayendo archivos";

            _paso3.Foreground = pasoActivo == 3 ? cyan : pasoActivo > 3 ? morado : gris;
            _paso3.Text = pasoActivo > 3 ? "✅ 3. SD Actualizada" : "3. Aplicando en SD";

            _paso4.Foreground = pasoActivo == 4 ? cyan : pasoActivo > 4 ? morado : gris;
            _paso4.Text = pasoActivo > 4 ? "✅ 4. Limpieza terminada" : "4. Limpiando temporales";
        }
    }
}