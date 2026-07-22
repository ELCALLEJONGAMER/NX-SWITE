using NX_Swite.Core;
using NX_Swite.Core;
using NX_Swite.Core.Configuracion;
using NX_Swite.Hardware;
using NX_Swite.Models;
using NX_Swite.UI;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace NX_Swite
{
    /// <summary>
    /// MainWindow � Overlay "Limpiar Micro SD":
    /// muestra qu� se borrar� vs. qu� se proteger� y ejecuta la limpieza
    /// con hold-to-confirm usando <see cref="NX_Swite.UI.Controles.SafeButton"/>.
    /// </summary>
    public partial class MainWindow
    {
        // ?? Abrir / cerrar overlay ????????????????????????????????????????

        private async void AbrirOverlayLimpiezaSD()
        {
            try
            {
                string? letraSD = (InfoSD.ComboDrives.SelectedItem as SDInfo)?.Letra;

                if (string.IsNullOrEmpty(letraSD))
                {
                    Dialogos.Advertencia(
                        "No hay ninguna Micro SD seleccionada.\nConecta una SD antes de usar esta funci�n.",
                        "Sin SD");
                    return;
                }

                await RefrescarOverlayLimpiezaSD(letraSD);
                MostrarOverlayConAnimacion(PanelLimpiezaSDOverlay);
            }
            catch (Exception ex)
            {
                Dialogos.Error($"Error al abrir el panel de limpieza:\n{ex.Message}");
            }
        }

        private void CerrarOverlayLimpiezaSD()
        {
            var fade = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(200)));
            fade.Completed += (_, _) =>
            {
                PanelLimpiezaSDOverlay.Visibility = Visibility.Collapsed;
                AplicarBlurFondo(false);
            };
            PanelLimpiezaSDOverlay.BeginAnimation(UIElement.OpacityProperty, fade);
        }

        private async Task RefrescarOverlayLimpiezaSD(string letraSD)
        {
            var prefs    = await Servicios.Preferencias.CargarAsync();
            var analisis = _cerebro.AnalizarLimpiezaSD(letraSD, prefs.LimpiezaSD.EntradasProtegidas);

            ListaBorrar.ItemsSource    = analisis.ABorrar;
            ListaProtegido.ItemsSource = analisis.AConservar;

            TxtContadorBorrar.Text    = $" ({analisis.ABorrar.Count})";
            TxtContadorProtegido.Text = $" ({analisis.AConservar.Count})";

            TxtSinContenidoBorrar.Visibility =
                analisis.ABorrar.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            TxtSinSD.Visibility = Visibility.Collapsed;

            // Advertencia de carpetas cr�ticas en la lista de borrado
            var criticos = analisis.ABorrar
                .Where(e => e.EsCritico)
                .Select(e => e.Nombre)
                .ToList();

            if (criticos.Count > 0)
            {
                TxtAdvertenciaCriticos.Text =
                    $"\u26A0 Carpetas cr\u00edticas detectadas para borrar: {string.Join(", ", criticos)}. " +
                    "\u00bfEst\u00e1s seguro de lo que haces?";
                TxtAdvertenciaCriticos.Visibility = Visibility.Visible;
            }
            else
            {
                TxtAdvertenciaCriticos.Visibility = Visibility.Collapsed;
            }

            BtnConfirmarLimpiezaSD.IsEnabled = analisis.ABorrar.Count > 0;
        }

        // ?? Handlers del overlay ??????????????????????????????????????????

        private void LimpiezaSD_BackdropClick(object sender, MouseButtonEventArgs e)
            => CerrarOverlayLimpiezaSD();

        private void LimpiezaSD_Cerrar_Click(object sender, RoutedEventArgs e)
            => CerrarOverlayLimpiezaSD();

        private async void LimpiezaSD_AbrirAjustes_Click(object sender, RoutedEventArgs e)
        {
            try { await AbrirAjustesEnTabCarpetasAsync(); }
            catch (Exception ex) { Dialogos.Error(ex.Message); }
        }

        /// <summary>
        /// Bot�n ?? en la fila de "SE BORRAR�": a�ade esa entrada a protegidos
        /// y refresca el overlay sin cerrar nada.
        /// </summary>
        private async void BtnProtegerEntrada_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as System.Windows.Controls.Button)?.Tag is not string nombre) return;

            try
            {
                var prefs = await Servicios.Preferencias.CargarAsync();
                if (!prefs.LimpiezaSD.EntradasProtegidas
                        .Any(s => string.Equals(s, nombre, StringComparison.OrdinalIgnoreCase)))
                {
                    prefs.LimpiezaSD.EntradasProtegidas.Add(nombre);
                    await Servicios.Preferencias.GuardarAsync(prefs);
                }

                string? letraSD = (InfoSD.ComboDrives.SelectedItem as SDInfo)?.Letra;
                if (!string.IsNullOrEmpty(letraSD))
                    await RefrescarOverlayLimpiezaSD(letraSD);
            }
            catch (Exception ex) { Dialogos.Error(ex.Message); }
        }

        /// <summary>
        /// Bot�n ? en la fila de "PROTEGIDO": quita esa entrada de protegidos
        /// y refresca el overlay.
        /// </summary>
        private async void BtnDesprotegerEntrada_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as System.Windows.Controls.Button)?.Tag is not string nombre) return;

            try
            {
                var prefs = await Servicios.Preferencias.CargarAsync();
                prefs.LimpiezaSD.EntradasProtegidas.RemoveAll(
                    s => string.Equals(s, nombre, StringComparison.OrdinalIgnoreCase));
                await Servicios.Preferencias.GuardarAsync(prefs);

                string? letraSD = (InfoSD.ComboDrives.SelectedItem as SDInfo)?.Letra;
                if (!string.IsNullOrEmpty(letraSD))
                    await RefrescarOverlayLimpiezaSD(letraSD);
            }
            catch (Exception ex) { Dialogos.Error(ex.Message); }
        }

        private async void LimpiezaSD_Confirmar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string? letraSD = (InfoSD.ComboDrives.SelectedItem as SDInfo)?.Letra;
                if (string.IsNullOrEmpty(letraSD)) return;

                CerrarOverlayLimpiezaSD();
                await Task.Delay(250);

                var prefs     = await Servicios.Preferencias.CargarAsync();
                var itemQueue = Servicios.Cola.AgregarItem("Limpiar Micro SD");
                PanelQueueOverlay.Visibility = Visibility.Visible;

                Servicios.Sonidos.Reproducir(EventoSonido.Instalar);
                _pantallaCarga.Mostrar("LIMPIAR MICRO SD");

                // Capturar el reportador UNA sola vez antes de usarlo en el callback
                var reportador = _pantallaCarga.ObtenerReportador();
                var progreso   = new Progress<EstadoProgreso>(estado =>
                {
                    reportador.Report(estado);
                    Servicios.Cola.ActualizarItem(itemQueue, estado.Porcentaje, estado.TareaActual);
                });

                var resultado = await _cerebro.LimpiarMicroSDAsync(
                    letraSD,
                    prefs.LimpiezaSD.EntradasProtegidas,
                    progreso,
                    CancellationToken.None);

                await Task.Delay(400);
                _pantallaCarga.Ocultar();

                if (resultado.Exito)
                {
                    Servicios.Cola.CompletarItem(itemQueue);
                    Servicios.Sonidos.Reproducir(EventoSonido.Exito);
                }
                else
                {
                    Servicios.Cola.ErrorItem(itemQueue, resultado.MensajeError);
                    Servicios.Sonidos.Reproducir(EventoSonido.Error);
                    Dialogos.Advertencia(resultado.MensajeError, "Limpieza con errores");
                }

                await ActualizarListaUnidadesAsync();
            }
            catch (Exception ex)
            {
                _pantallaCarga.Ocultar();
                Dialogos.Error($"Error durante la limpieza:\n{ex.Message}");
            }
        }

        // ?? Evento del RetractilDer ???????????????????????????????????????

        private void ArsenalRetractil_LimpiezaMicroSDSolicitada(object? sender, EventArgs e)
            => AbrirOverlayLimpiezaSD();
    }
}
