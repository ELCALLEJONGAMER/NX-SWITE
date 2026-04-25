using NX_Suite.Core;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace NX_Suite
{
    /// <summary>
    /// MainWindow — Cromo de la ventana sin bordes: drag de la barra superior,
    /// minimizar, cerrar y ajuste inicial de tamaño al monitor.
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>
        /// Ajusta la ventana al 90 % del monitor de trabajo, respetando los mínimos 1280×720.
        /// </summary>
        private void AjustarTamañoVentana()
        {
            var area = SystemParameters.WorkArea;
            Width    = Math.Max(MinWidth,  area.Width  * 0.90);
            Height   = Math.Max(MinHeight, area.Height * 0.90);
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
            Servicios.Sonidos.Reproducir(EventoSonido.Cerrar);
            await Task.Delay(600);
            Application.Current.Shutdown();
        }

        private async void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Servicios.Sonidos.Reproducir(EventoSonido.Cerrar);
            await Task.Delay(600);
            Application.Current.Shutdown();
        }
    }
}
