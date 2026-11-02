using NX_Swite.Core;
using NX_Swite.Core.Configuracion;
using NX_Swite.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace NX_Swite
{
    /// <summary>
    /// MainWindow — Lógica del panel de actualización de la app:
    /// muestra/oculta el overlay, descarga y lanza el actualizador externo.
    /// </summary>
    public partial class MainWindow
    {
        private bool _panelActualizacionAbierto;

        // ?? Abrir / cerrar overlay ????????????????????????????????????????

        private void BtnActualizaciones_Click(object sender, RoutedEventArgs e)
        {
            if (_panelActualizacionAbierto)
                CerrarPanelActualizacion();
            else
                AbrirPanelActualizacion();
        }

        private void AbrirPanelActualizacion()
        {
            if (!Servicios.Actualizacion.HayActualizacion) return;

            TxtVersionActualOverlay.Text = $"v{Servicios.Actualizacion.VersionActual}";
            TxtVersionNuevaOverlay.Text  = $"v{Servicios.Actualizacion.VersionRemota}";
            TxtNotasActualizacion.Text   = string.IsNullOrWhiteSpace(Servicios.Actualizacion.NotasVersion)
                                           ? "Sin notas de versión disponibles."
                                           : Servicios.Actualizacion.NotasVersion;

            PanelProgresoActualizacion.Visibility           = Visibility.Collapsed;
            BtnActualizarAhora.IsEnabled                    = true;
            BtnRecordarMasTardeActualizacion.IsEnabled      = true;
            BarraProgresoActualizacion.Width                 = 0;
            TxtEstadoActualizacion.Text                      = "Preparando descarga...";

            PanelActualizacionOverlay.Visibility = Visibility.Visible;
            _panelActualizacionAbierto           = true;
        }

        private void CerrarPanelActualizacion()
        {
            PanelActualizacionOverlay.Visibility = Visibility.Collapsed;
            _panelActualizacionAbierto           = false;
        }

        /// <summary>
        /// Al arrancar, abre automáticamente la ventana de actualización si hay
        /// una versión nueva. Si hay procesos en cola, espera a que terminen.
        /// "Más tarde" la cierra hasta el siguiente arranque.
        /// </summary>
        private void MostrarAvisoActualizacionAlArrancar()
        {
            if (!Servicios.Actualizacion.HayActualizacion) return;

            if (!Servicios.Cola.TieneActivos)
            {
                AbrirPanelActualizacion();
                return;
            }

            System.ComponentModel.PropertyChangedEventHandler? handler = null;
            handler = (_, e) =>
            {
                if (e.PropertyName != nameof(Servicios.Cola.TieneActivos) || Servicios.Cola.TieneActivos) return;
                Servicios.Cola.PropertyChanged -= handler;
                Dispatcher.InvokeAsync(AbrirPanelActualizacion);
            };
            Servicios.Cola.PropertyChanged += handler;
        }

        private void BtnCerrarActualizacion_Click(object sender, RoutedEventArgs e)
            => CerrarPanelActualizacion();

        private void BtnRecordarMasTarde_Click(object sender, RoutedEventArgs e)
            => CerrarPanelActualizacion();

        // ?? Descarga + actualización ??????????????????????????????????????

        private async void BtnActualizarAhora_Click(object sender, RoutedEventArgs e)
        {
            BtnActualizarAhora.IsEnabled               = false;
            BtnRecordarMasTardeActualizacion.IsEnabled = false;
            PanelProgresoActualizacion.Visibility      = Visibility.Visible;

            string versionRemota = Servicios.Actualizacion.VersionRemota;

            try
            {
                Logger.ActualizacionDescargaIniciada(versionRemota);

                var progreso = new Progress<(double pct, string msg)>(info =>
                    Dispatcher.Invoke(() =>
                    {
                        TxtEstadoActualizacion.Text = info.msg;
                        double containerW = ContenedorBarraActualizacion.ActualWidth;
                        BarraProgresoActualizacion.Width = Math.Max(0, containerW * info.pct / 100.0);
                    }));

                string zipPath = await GestorActualizacion.DescargarActualizacionAsync(
                    Servicios.Actualizacion.UrlDescarga, progreso);

                Logger.ActualizacionDescargaCompletada(versionRemota);

                TxtEstadoActualizacion.Text = "Lanzando actualizador...";

                string appDir      = AppContext.BaseDirectory;
                string exePath     = Process.GetCurrentProcess().MainModule?.FileName
                                     ?? Path.Combine(appDir, "NX-Swite.exe");
                string updaterPath = Path.Combine(appDir, ConfiguracionLocal.NombreUpdater);

                Logger.ActualizacionActualizadorIniciado(versionRemota);
                string? logoUrl  = _datosGist?.GlobalBranding?.LogoUrl;
                string? logoPath = string.IsNullOrWhiteSpace(logoUrl)
                    ? null
                    : Servicios.Iconos.ObtenerRutaLocal(logoUrl);
                GestorActualizacion.LanzarActualizador(updaterPath, zipPath, appDir, exePath, logoPath);
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                Logger.ActualizacionFallida("descargar/iniciar la actualización", ex.Message);
                TxtEstadoActualizacion.Text                = $"Error: {ex.Message}";
                BtnActualizarAhora.IsEnabled               = true;
                BtnRecordarMasTardeActualizacion.IsEnabled = true;
            }
        }
    }
}
