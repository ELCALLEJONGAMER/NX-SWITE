using NX_Swite.Core;
using NX_Swite.Models;
using System.Windows;
using System.Windows.Controls;

namespace NX_Swite
{
    /// <summary>
    /// MainWindow � Overlay de cola (queue) global: abrir, cerrar, limpiar
    /// completados y cancelar items individuales.
    /// </summary>
    public partial class MainWindow
    {
        private void BtnAbrirQueue_Click(object sender, RoutedEventArgs e)
            => PanelQueueOverlay.Visibility = Visibility.Visible;

        private void BtnCerrarQueue_Click(object sender, RoutedEventArgs e)
            => PanelQueueOverlay.Visibility = Visibility.Collapsed;

        private void BtnLimpiarQueue_Click(object sender, RoutedEventArgs e)
            => Servicios.Cola.LimpiarCompletados();

        private void BtnCancelarItemQueue_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ItemQueue item)
                Servicios.Cola.CancelarItem(item);
        }
    }
}
