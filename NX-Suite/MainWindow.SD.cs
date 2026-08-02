using NX_Swite.Hardware;
using NX_Swite.Core.Configuracion;
using NX_Swite.Models;
using NX_Swite.UI;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NX_Swite
{
    /// <summary>
    /// MainWindow — Gestión de la SD (lista de unidades, panel de info, refresco
    /// al cambiar de unidad). Suscrito al combo de unidades y al evento
    /// plug &amp; play de <see cref="NotificadorDiscos"/>.
    /// </summary>
    public partial class MainWindow
    {
        // ── Control de la detección asíncrona de firmware de emuMMC ─────────
        // Se cancela y recrea cada vez que cambia la unidad seleccionada, se
        // desconecta la SD o se cierra la ventana, para evitar que un resultado
        // obsoleto (de una unidad ya no seleccionada) actualice la UI.
        private CancellationTokenSource? _ctsFirmwareEmummc;
        private int _idOperacionFirmwareEmummc;

        /// <summary>
        /// Ultima letra de SD vista como conectada (ej. "G:\"). Se usa tras una
        /// desconexion para saber que letra intentar cerrar en los dialogos
        /// nativos de Windows ("Ubicacion no disponible", Explorer), ya que en
        /// ese momento el combo ya no la contiene.
        /// </summary>
        private string? _ultimaLetraSdConocida;

        /// <summary>
        /// Indica si actualmente hay una microSD (removible) conectada y
        /// seleccionada en el panel derecho. Usado por los distintos flujos
        /// (asistido parcial, completo, instalacion desde PC, formato,
        /// particionado) para bloquear la operacion si no hay SD.
        /// </summary>
        public bool HayMicroSdConectada()
        {
            var sd = InfoSD.ComboDrives.SelectedItem as SDInfo;
            return sd != null && sd.DiscoFisico >= 0;
        }

        private async Task ActualizarListaUnidadesAsync()
        {
            try
            {
                string? letraPrevia = (InfoSD.ComboDrives.SelectedItem as SDInfo)?.Letra;
                var unidades = await _cerebro.ObtenerUnidadesRemoviblesAsync();

                InfoSD.ComboDrives.ItemsSource       = unidades;
                InfoSD.ComboDrives.DisplayMemberPath = "FullName";

                if (unidades != null && unidades.Any())
                {
                    var unidadPrevia = unidades.FirstOrDefault(u => u.Letra == letraPrevia);
                    InfoSD.ComboDrives.SelectedItem = unidadPrevia ?? unidades.First();
                    _ultimaLetraSdConocida = (InfoSD.ComboDrives.SelectedItem as SDInfo)?.Letra ?? letraPrevia;
                }
                else
                {
                    _ultimaLetraSdConocida = letraPrevia ?? _ultimaLetraSdConocida;
                    LimpiarInterfazSD();
                    // No hay SD: recalcular estados localmente sin llamar a la red
                    if (!_cargandoCatalogoInicial && _catalogoModulos != null)
                    {
                        await _cerebro.RefrescarEstadosSinRedAsync(_catalogoModulos, string.Empty);
                        RefrescarVistaActual();
                    }

                    if (VistaNews.Visibility == Visibility.Visible)
                        ActualizarDiagnosticoSD();
                }
            }
            catch (Exception ex)
            {
                Dialogos.Advertencia($"Error detectando la SD: {ex.Message}", "Diagnóstico");
            }
        }

        private void LimpiarInterfazSD()
        {
            CancelarDeteccionFirmwareEmummc();

            InfoSD.TxtTotalSize.Text  = "0 GB";
            InfoSD.TxtFileSystem.Text = "--";
            InfoSD.TxtSDSerial.Text   = "Desconocido";
            InfoSD.TxtAtmosVer.Text   = "N/A";
            InfoSD.TxtFirmwareEmummc.Text = "--";
            InfoSD.TxtSDModelo.Visibility    = System.Windows.Visibility.Collapsed;
            InfoSD.LblSDModelo.Visibility    = System.Windows.Visibility.Collapsed;
            InfoSD.TxtSDRegion.Visibility    = System.Windows.Visibility.Collapsed;
            InfoSD.LblSDRegion.Visibility    = System.Windows.Visibility.Collapsed;
            InfoSD.SepConsola.Visibility     = System.Windows.Visibility.Collapsed;
            InfoSD.LblConsola.Visibility     = System.Windows.Visibility.Collapsed;
            OcultarSeccionLlaves();
        }

        /// <summary>Cancela cualquier detección de firmware de emuMMC en curso.</summary>
        internal void CancelarDeteccionFirmwareEmummc()
        {
            _ctsFirmwareEmummc?.Cancel();
            _ctsFirmwareEmummc?.Dispose();
            _ctsFirmwareEmummc = null;
        }

        /// <summary>
        /// Traduce un <see cref="EstadoFirmwareEmummc"/> al texto que se muestra
        /// en el campo «FIRMWARE EMUMMC» del panel derecho.
        /// </summary>
        private static string TextoEstadoFirmwareEmummc(ResultadoFirmwareEmummc resultado) => resultado.Estado switch
        {
            EstadoFirmwareEmummc.Detected             => resultado.Version ?? "--",
            EstadoFirmwareEmummc.FirmwareNotDetected   => "emuMMC detectada, pero no fue posible identificar su firmware.",
            EstadoFirmwareEmummc.EmuMmcNotFound         => "No se detectó una emuMMC en esta microSD.",
            EstadoFirmwareEmummc.KeysMissing            => "Sin prod.keys",
            EstadoFirmwareEmummc.KeysInvalid            => "prod.keys inválido",
            EstadoFirmwareEmummc.ToolValidationFailed   => "Herramienta no disponible",
            EstadoFirmwareEmummc.AccessDenied           => "Requiere permisos de administrador",
            EstadoFirmwareEmummc.TimedOut               => "La detección tardó demasiado y fue cancelada.",
            _                                            => "No se pudo leer la NAND",
        };

        /// <summary>
        /// Inicia (sin bloquear) la detección de firmware de la emuMMC RAW para
        /// la unidad representada por <paramref name="info"/>. Cancela cualquier
        /// detección anterior y descarta resultados obsoletos comparando el
        /// identificador incremental de operación.
        /// </summary>
        private void IniciarDeteccionFirmwareEmummcAsync(InfoPanelDerecho info)
        {
            CancelarDeteccionFirmwareEmummc();

            if (!info.HayProdkeys)
            {
                InfoSD.TxtFirmwareEmummc.Text = "Sin prod.keys";
                return;
            }

            InfoSD.TxtFirmwareEmummc.Text = "Detectando firmware de emuMMC...";

            _ctsFirmwareEmummc = new CancellationTokenSource();
            var ct = _ctsFirmwareEmummc.Token;
            int idOperacion = ++_idOperacionFirmwareEmummc;

            _ = EjecutarDeteccionFirmwareEmummcAsync(info, idOperacion, ct);
        }

        private async Task EjecutarDeteccionFirmwareEmummcAsync(InfoPanelDerecho info, int idOperacion, System.Threading.CancellationToken ct)
        {
            ResultadoFirmwareEmummc resultado;
            try
            {
                resultado = await _cerebro.ObtenerFirmwareEmummcAsync(info, ct);
            }
            catch (OperationCanceledException)
            {
                // Cancelación por cambio de unidad o cierre de ventana: no es un error, no se toca la UI.
                return;
            }
            catch (Exception ex)
            {
                resultado = ResultadoFirmwareEmummc.De(EstadoFirmwareEmummc.Failed, ex.Message);
            }

            // Descartar resultados obsoletos: la unidad pudo cambiar mientras se detectaba.
            if (idOperacion != _idOperacionFirmwareEmummc) return;
            if (InfoSD.ComboDrives.SelectedItem is not SDInfo unidadActual) return;
            if (unidadActual.DiscoFisico != info.DiscoFisico) return;

            InfoSD.TxtFirmwareEmummc.Text = TextoEstadoFirmwareEmummc(resultado);
        }

        /// <summary>
        /// Refresca únicamente la versión de Atmosphere en el panel derecho
        /// sin necesidad de cambiar la unidad seleccionada. Útil tras instalar
        /// o desinstalar un módulo con etiquetas "atmosphere" / "atmosphere_mod".
        /// </summary>
        private void OcultarSeccionLlaves()
        {
            InfoSD.SepLlaves.Visibility        = System.Windows.Visibility.Collapsed;
            InfoSD.LblSeccionLlaves.Visibility  = System.Windows.Visibility.Collapsed;
            InfoSD.LblMasterKey.Visibility      = System.Windows.Visibility.Collapsed;
            InfoSD.TxtMasterKey.Visibility      = System.Windows.Visibility.Collapsed;
            InfoSD.LblFirmware.Visibility       = System.Windows.Visibility.Collapsed;
            InfoSD.TxtFirmware.Visibility       = System.Windows.Visibility.Collapsed;
            InfoSD.LblAtmosMinima.Visibility    = System.Windows.Visibility.Collapsed;
            InfoSD.TxtAtmosMinima.Visibility    = System.Windows.Visibility.Collapsed;
        }

        private void MostrarSeccionLlaves(NX_Swite.Models.InfoPanelDerecho info)
        {
            InfoSD.SepLlaves.Visibility        = System.Windows.Visibility.Visible;
            InfoSD.LblSeccionLlaves.Visibility  = System.Windows.Visibility.Visible;
            InfoSD.TxtMasterKey.Text            = info.MasterKeyMaxima;
            InfoSD.LblMasterKey.Visibility      = System.Windows.Visibility.Visible;
            InfoSD.TxtMasterKey.Visibility      = System.Windows.Visibility.Visible;
            InfoSD.TxtFirmware.Text             = info.FirmwareCompatible;
            InfoSD.LblFirmware.Visibility       = System.Windows.Visibility.Visible;
            InfoSD.TxtFirmware.Visibility       = System.Windows.Visibility.Visible;
            InfoSD.TxtAtmosMinima.Text          = info.AtmosphereDesde;
            InfoSD.LblAtmosMinima.Visibility    = System.Windows.Visibility.Visible;
            InfoSD.TxtAtmosMinima.Visibility    = System.Windows.Visibility.Visible;
        }

        internal void RefrescarVersionAtmos()
        {
            if (InfoSD.ComboDrives.SelectedItem is not SDInfo unidad) return;
            if (_catalogoModulos == null) return;

            var info = _cerebro.ObtenerInfoPanel(unidad, _catalogoModulos.ToList());
            InfoSD.TxtAtmosVer.Text = info.VersionAtmos;
        }

        /// <summary>
        /// Refresca todos los campos del panel derecho SD sin sincronizar con la red.
        /// Útil para reflejar cambios en archivos de la SD (p. ej. tras restaurar llaves)
        /// sin necesidad de expulsar y volver a insertar la tarjeta.
        /// </summary>
        internal void RefrescarPanelInfoSD()
        {
            if (InfoSD.ComboDrives.SelectedItem is not SDInfo unidad) return;
            if (_catalogoModulos == null) return;

            var info = _cerebro.ObtenerInfoPanel(unidad, _catalogoModulos.ToList());
            InfoSD.TxtTotalSize.Text  = info.Capacidad;
            InfoSD.TxtFileSystem.Text = info.Formato;
            InfoSD.TxtAtmosVer.Text   = info.VersionAtmos;
            InfoSD.TxtSDSerial.Text   = info.Serial;
            InfoSD.SepConsola.Visibility = System.Windows.Visibility.Visible;
            InfoSD.LblConsola.Visibility = System.Windows.Visibility.Visible;

            var entradaModelo = NX_Swite.Core.ModeloSwitchTable.ResolverSoloRemota(info.Serial);
            InfoSD.TxtSDModelo.Text       = entradaModelo?.Modelo ?? string.Empty;
            InfoSD.TxtSDModelo.Visibility = entradaModelo != null ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            InfoSD.LblSDModelo.Visibility = InfoSD.TxtSDModelo.Visibility;
            InfoSD.TxtSDRegion.Text       = entradaModelo?.Region ?? string.Empty;
            InfoSD.TxtSDRegion.Visibility = entradaModelo != null ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            InfoSD.LblSDRegion.Visibility = InfoSD.TxtSDRegion.Visibility;

            InfoSD.TxtFileSystem.Foreground = info.Formato == "FAT32"
                ? (SolidColorBrush)FindResource("AcentoCian")
                : (SolidColorBrush)FindResource("AcentoRojo");

            if (info.HayProdkeys)
                MostrarSeccionLlaves(info);
            else
                OcultarSeccionLlaves();

            IniciarDeteccionFirmwareEmummcAsync(info);
        }



        private async void ComboDrives_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (InfoSD.ComboDrives.SelectedItem is not SDInfo unidad)
                return;

            if (_catalogoModulos == null || _datosGist == null)
                return;

            // Siempre actualizar el panel de info SD
            var info = _cerebro.ObtenerInfoPanel(unidad, _catalogoModulos.ToList());
            InfoSD.TxtTotalSize.Text  = info.Capacidad;
            InfoSD.TxtFileSystem.Text = info.Formato;
            InfoSD.TxtAtmosVer.Text   = info.VersionAtmos;
            InfoSD.TxtSDSerial.Text   = info.Serial;
            InfoSD.SepConsola.Visibility = System.Windows.Visibility.Visible;
            InfoSD.LblConsola.Visibility = System.Windows.Visibility.Visible;

            var entradaModelo = NX_Swite.Core.ModeloSwitchTable.ResolverSoloRemota(info.Serial);
            InfoSD.TxtSDModelo.Text       = entradaModelo?.Modelo ?? string.Empty;
            InfoSD.TxtSDModelo.Visibility = entradaModelo != null ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            InfoSD.LblSDModelo.Visibility = InfoSD.TxtSDModelo.Visibility;
            InfoSD.TxtSDRegion.Text       = entradaModelo?.Region ?? string.Empty;
            InfoSD.TxtSDRegion.Visibility = entradaModelo != null ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            InfoSD.LblSDRegion.Visibility = InfoSD.TxtSDRegion.Visibility;

            InfoSD.TxtFileSystem.Foreground = info.Formato == "FAT32"
                ? (SolidColorBrush)FindResource("AcentoCian")
                : (SolidColorBrush)FindResource("AcentoRojo");

            if (info.HayProdkeys)
                MostrarSeccionLlaves(info);
            else
                OcultarSeccionLlaves();

            IniciarDeteccionFirmwareEmummcAsync(info);

            // Solo re-sincronizar si la carga inicial ya termino
            if (_cargandoCatalogoInicial) return;

            try
            {
                _datosGist = await _cerebro.SincronizarTodoAsync(ConfiguracionLocal.UrlGistPrincipal, unidad.Letra);

                if (_datosGist == null) return;

                // Re-aplicar tablas remotas para que modelo/región/llaves usen datos frescos del Gist
                AplicarConfiguracionRemota(_datosGist);

                _catalogoModulos = new ObservableCollection<ModuloConfig>(_datosGist.Modulos ?? new System.Collections.Generic.List<ModuloConfig>());

                // Repoblar campos del panel que dependen de las tablas remotas
                var entradaModelo2 = NX_Swite.Core.ModeloSwitchTable.ResolverSoloRemota(InfoSD.TxtSDSerial.Text);
                InfoSD.TxtSDModelo.Text       = entradaModelo2?.Modelo ?? string.Empty;
                InfoSD.TxtSDModelo.Visibility = entradaModelo2 != null ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
                InfoSD.LblSDModelo.Visibility = InfoSD.TxtSDModelo.Visibility;
                InfoSD.TxtSDRegion.Text       = entradaModelo2?.Region ?? string.Empty;
                InfoSD.TxtSDRegion.Visibility = entradaModelo2 != null ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
                InfoSD.LblSDRegion.Visibility = InfoSD.TxtSDRegion.Visibility;

                if (InfoSD.TxtMasterKey.Visibility == System.Windows.Visibility.Visible &&
                    !string.IsNullOrEmpty(InfoSD.TxtMasterKey.Text))
                {
                    var mk = NX_Swite.Core.MasterKeyTable.BuscarSoloRemota(InfoSD.TxtMasterKey.Text);
                    InfoSD.TxtFirmware.Text    = mk?.RangoHosCompatible ?? "--";
                    InfoSD.TxtAtmosMinima.Text = mk?.AtmosphereDesde    ?? "--";
                }

                // Si el detalle está activo no tocamos los paneles del catálogo:
                // BtnInstalar/BtnBorrar ya restauran la vista correctamente.
                if (VistaDetalle.Visibility == Visibility.Visible) return;

                if (_mundoSeleccionado != null)
                    ActualizarFiltrosDelMundo(_mundoSeleccionado.Id);

                RefrescarVistaActual();

                if (VistaNews.Visibility == Visibility.Visible)
                    ActualizarDiagnosticoSD();
            }
            catch (Exception ex)
            {
                Dialogos.Advertencia($"Error al sincronizar con la SD: {ex.Message}", "Error");
            }
        }

        private async void BtnExpulsarSD_Click(object? sender, EventArgs e)
        {
            if (InfoSD.ComboDrives.SelectedItem is not SDInfo unidad)
            {
                Dialogos.Advertencia("No hay ninguna microSD seleccionada.", "Expulsar SD");
                return;
            }

            InfoSD.IsEnabled = false;
            try
            {
                bool ok = await Task.Run(() => EscanerDiscos.ExpulsarUnidad(unidad.Letra));

                if (ok)
                    Dialogos.Advertencia(
                        $"La microSD \"{unidad.Etiqueta}\" fue expulsada correctamente.\nYa puedes retirarla con seguridad.",
                        "Expulsar SD ✓");
                else
                    Dialogos.Advertencia(
                        "Windows no pudo expulsar la SD en este momento.\nCierra los programas que puedan estar accediendo a ella e inténtalo de nuevo.",
                        "Expulsar SD");
            }
            catch (Exception ex)
            {
                Dialogos.Advertencia($"Error al intentar expulsar la SD: {ex.Message}", "Expulsar SD");
            }
            finally
            {
                InfoSD.IsEnabled = true;
            }
        }
    }
}
