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
        /// <summary>
        /// Resultado devuelto por el overlay de Limpiar Micro SD a quien lo invoque
        /// (ej. el flujo de Actualizar paquete predefinido).
        /// </summary>
        public enum ResultadoLimpiezaSD
        {
            Confirmada,
            Cancelada,
            Error
        }

        /// <summary>
        /// Cuando no es null, indica que el overlay de Limpiar SD fue abierto desde
        /// un flujo llamador (ej. Actualizar paquete predefinido) que espera un
        /// resultado claro en vez de solo cerrar el overlay.
        /// </summary>
        private TaskCompletionSource<ResultadoLimpiezaSD>? _tcsLimpiezaSD;

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

                PanelLimpiezaSDContextoActualizacion.Visibility = Visibility.Collapsed;
                await RefrescarOverlayLimpiezaSD(letraSD);
                MostrarOverlayConAnimacion(PanelLimpiezaSDOverlay);
            }
            catch (Exception ex)
            {
                Dialogos.Error($"Error al abrir el panel de limpieza:\n{ex.Message}");
            }
        }

        /// <summary>
        /// Abre el overlay de Limpiar Micro SD como paso previo de un flujo llamador
        /// (ej. Actualizar paquete predefinido) y devuelve un resultado claro:
        /// <see cref="ResultadoLimpiezaSD.Confirmada"/> si el usuario confirmó y la
        /// limpieza se ejecutó con éxito, <see cref="ResultadoLimpiezaSD.Cancelada"/>
        /// si el usuario cerró/canceló, o <see cref="ResultadoLimpiezaSD.Error"/> si
        /// la limpieza falló (ej. SD desconectada). No duplica la lógica de limpieza:
        /// reutiliza el mismo overlay y el mismo <c>_cerebro.LimpiarMicroSDAsync</c>.
        /// </summary>
        public async Task<ResultadoLimpiezaSD> AbrirLimpiezaSDComoPasoDeActualizacionAsync(string letraSD)
        {
            if (string.IsNullOrEmpty(letraSD) || !System.IO.Directory.Exists(letraSD))
                return ResultadoLimpiezaSD.Error;

            _tcsLimpiezaSD = new TaskCompletionSource<ResultadoLimpiezaSD>();

            try
            {
                PanelLimpiezaSDContextoActualizacion.Visibility = Visibility.Visible;
                await RefrescarOverlayLimpiezaSD(letraSD);
                MostrarOverlayConAnimacion(PanelLimpiezaSDOverlay);
            }
            catch (Exception ex)
            {
                _tcsLimpiezaSD = null;
                Dialogos.Error($"Error al abrir el panel de limpieza:\n{ex.Message}");
                return ResultadoLimpiezaSD.Error;
            }

            return await _tcsLimpiezaSD.Task;
        }

        private void CerrarOverlayLimpiezaSD()
        {
            // Si el overlay fue abierto desde un flujo llamador (Actualizar paquete
            // predefinido) y se cierra sin pasar por confirmar, es una cancelación.
            if (_tcsLimpiezaSD is { Task.IsCompleted: false } tcsPendiente)
                tcsPendiente.TrySetResult(ResultadoLimpiezaSD.Cancelada);

            var fade = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(200)));
            fade.Completed += (_, _) =>
            {
                PanelLimpiezaSDOverlay.Visibility = Visibility.Collapsed;
                PanelLimpiezaSDContextoActualizacion.Visibility = Visibility.Collapsed;
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
            // Capturamos el TCS del flujo llamador (si existe) ANTES de cerrar el
            // overlay, porque CerrarOverlayLimpiezaSD() resuelve como Cancelada
            // cualquier TCS que siga pendiente al cerrarse.
            var tcsLlamador = _tcsLimpiezaSD;
            _tcsLimpiezaSD = null;

            try
            {
                string? letraSD = (InfoSD.ComboDrives.SelectedItem as SDInfo)?.Letra;
                if (string.IsNullOrEmpty(letraSD))
                {
                    tcsLlamador?.TrySetResult(ResultadoLimpiezaSD.Error);
                    return;
                }

                CerrarOverlayLimpiezaSD();
                await Task.Delay(250);

                var prefs     = await Servicios.Preferencias.CargarAsync();
                var itemQueue = Servicios.Cola.AgregarItem("Limpiar Micro SD");

                Servicios.Sonidos.Reproducir(EventoSonido.Instalar);
                _pantallaCarga.Mostrar("LIMPIAR MICRO SD");

                // Capturar el reportador UNA sola vez antes de usarlo en el callback
                var reportador = _pantallaCarga.ObtenerReportador();
                var progreso   = new Progress<EstadoProgreso>(estado =>
                {
                    reportador.Report(estado);
                    Servicios.Cola.ActualizarItem(itemQueue, estado.Porcentaje, estado.TareaActual);
                });

                Resultado? resultado = null;
                await PreservarYRestaurarLlaves(letraSD, "Limpiar SD", async () =>
                {
                    resultado = await _cerebro.LimpiarMicroSDAsync(
                        letraSD,
                        prefs.LimpiezaSD.EntradasProtegidas,
                        progreso,
                        CancellationToken.None);
                });

                await Task.Delay(400);
                _pantallaCarga.Ocultar();

                if (resultado?.Exito == true)
                {
                    Servicios.Cola.CompletarItem(itemQueue);
                    Servicios.Sonidos.Reproducir(EventoSonido.Exito);
                    tcsLlamador?.TrySetResult(ResultadoLimpiezaSD.Confirmada);
                }
                else
                {
                    string error = resultado?.MensajeError ?? "Error desconocido durante la limpieza.";
                    Servicios.Cola.ErrorItem(itemQueue, error);
                    Servicios.Sonidos.Reproducir(EventoSonido.Error);
                    Dialogos.Advertencia(error, "Limpieza con errores");
                    tcsLlamador?.TrySetResult(ResultadoLimpiezaSD.Error);
                }

                await ActualizarListaUnidadesAsync();
            }
            catch (Exception ex)
            {
                _pantallaCarga.Ocultar();
                Dialogos.Error($"Error durante la limpieza:\n{ex.Message}");
                tcsLlamador?.TrySetResult(ResultadoLimpiezaSD.Error);
            }
        }

        // ?? Evento del RetractilDer ???????????????????????????????????????

        private void ArsenalRetractil_LimpiezaMicroSDSolicitada(object? sender, EventArgs e)
            => AbrirOverlayLimpiezaSD();
    }
}
