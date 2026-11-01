using NX_Swite.Core;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace NX_Swite
{
    /// <summary>
    /// MainWindow — Cromo de la ventana sin bordes: drag de la barra superior,
    /// minimizar, cerrar y ajuste inicial de tamaño al monitor.
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>
        /// Ajusta la ventana al 90 % del monitor de trabajo, respetando los mínimos 1280×720
        /// (o el tamaño del monitor si es menor) y activa el escalado según el tamaño.
        /// </summary>
        private void AjustarTamañoVentana()
        {
            var area = SystemParameters.WorkArea;
            MinWidth  = Math.Min(MinWidth,  area.Width);
            MinHeight = Math.Min(MinHeight, area.Height);
            Width    = Math.Max(MinWidth,  area.Width  * 0.90);
            Height   = Math.Max(MinHeight, area.Height * 0.90);
            SizeChanged += (_, _) => AplicarEscalaUi();
        }

        private const double AnchoDiseño = 1280, AltoDiseño = 720;

        /// <summary>
        /// Escala toda la interfaz en proporción al tamaño de la ventana respecto al diseño base 1280×720.
        /// </summary>
        private void AplicarEscalaUi()
        {
            double escala = Math.Clamp(Math.Min(ActualWidth / AnchoDiseño, ActualHeight / AltoDiseño), 0.7, 1.5);
            if (MainGrid.LayoutTransform is ScaleTransform actual && Math.Abs(actual.ScaleX - escala) < 0.01) return;
            MainGrid.LayoutTransform = new ScaleTransform(escala, escala);
        }

        private void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void BtnMinimizar_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private async void BtnCerrar_Click(object sender, RoutedEventArgs e)
        {
            CancelarDeteccionFirmwareEmummc();
            Servicios.Sonidos.Reproducir(EventoSonido.Cerrar);
            await Task.Delay(600);
            Application.Current.Shutdown();
        }

        private async void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            CancelarDeteccionFirmwareEmummc();
            Servicios.Sonidos.Reproducir(EventoSonido.Cerrar);
            await Task.Delay(600);
            Application.Current.Shutdown();
        }
    }
}
