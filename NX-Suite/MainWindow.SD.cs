using NX_Swite.Core;
using NX_Swite.Hardware;
using NX_Swite.Core.Configuracion;
using NX_Swite.Models;
using NX_Swite.Services;
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
        /// Version real del firmware detectado en la emuMMC (via NxNandManager),
        /// o null si aun no se detecto / no aplica. Usado por el escaneo de
        /// compatibilidades rotas (Fuente D) para comparar contra el Gist,
        /// sin depender de parsear el texto mostrado en la UI.
        /// </summary>
        internal string? FirmwareEmummcRealDetectado { get; private set; }

        /// <summary>
        /// Version real de Atmosphere detectada en la SD (catalogo o fallback
        /// package3), o null si es desconocida. Ver <see cref="FirmwareEmummcRealDetectado"/>.
        /// </summary>
        internal string? AtmosRealDetectado { get; private set; }

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
                    // Solo se autoselecciona si hay una única unidad o si la que el usuario
                    // eligió sigue conectada; con varias, debe elegir manualmente.
                    var unidadPrevia = unidades.FirstOrDefault(u => u.Letra == letraPrevia);
                    InfoSD.ComboDrives.SelectedItem = unidadPrevia ?? (unidades.Count() == 1 ? unidades.First() : null);
                    if (InfoSD.ComboDrives.SelectedItem == null)
                        LimpiarInterfazSD();
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

        /// <summary>
        /// Confirmación explícita antes de borrar una unidad, mostrando sus datos para
        /// evitar formatear por error otro dispositivo extraíble (p. ej. un USB).
        /// </summary>
        private bool ConfirmarBorradoUnidad(string? letra, int disco)
        {
            var unidades = (InfoSD.ComboDrives.ItemsSource as IEnumerable<SDInfo>)?.ToList() ?? new List<SDInfo>();
            var sd = unidades.FirstOrDefault(u => u.Letra == letra);
            string descripcion = sd != null
                ? $"Unidad: {sd.Letra}\nNombre: {(string.IsNullOrWhiteSpace(sd.Etiqueta) ? "(sin nombre)" : sd.Etiqueta)}\nCapacidad: {sd.CapacidadTotal} GB\nFormato: {sd.Formato}\nDisco físico: {disco}"
                : $"Unidad: {letra}\nDisco físico: {disco}";
            string aviso = unidades.Count > 1
                ? $"\n\n⚠ Hay {unidades.Count} unidades extraíbles conectadas. Comprueba que es la Micro SD correcta."
                : string.Empty;

            return Dialogos.Confirmar(
                $"Se BORRARÁ TODO el contenido de esta unidad:\n\n{descripcion}{aviso}\n\n¿Es la Micro SD correcta y quieres continuar?",
                "Confirmar Micro SD", MessageBoxImage.Warning);
        }

        private void LimpiarInterfazSD()
        {
            CancelarDeteccionFirmwareEmummc();

            InfoSD.TxtTotalSize.Text  = "0 GB";
            InfoSD.TxtFileSystem.Text = "--";
            InfoSD.TxtSDSerial.Text   = "Desconocido";
            InfoSD.TxtAtmosVer.Text   = "N/A";
            InfoSD.TxtFirmwareEmummc.Text = "--";
            InfoSD.BtnVerificarFirmwareAlterno.Visibility = System.Windows.Visibility.Collapsed;
            AtmosRealDetectado = null;
            FirmwareEmummcRealDetectado = null;
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
            InfoSD.BtnVerificarFirmwareAlterno.Visibility = Visibility.Collapsed;

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

            // Si la lectura normal falló, usar la versión verificada antes si la
            // huella de la emuMMC no ha cambiado (lee ~1 MB, menos de un segundo).
            bool desdeRecuerdo = false;
            if (resultado.Estado is EstadoFirmwareEmummc.FirmwareNotDetected
                    or EstadoFirmwareEmummc.ToolValidationFailed
                    or EstadoFirmwareEmummc.TimedOut
                    or EstadoFirmwareEmummc.Failed
                && !string.IsNullOrEmpty(info.RutaProdKeys))
            {
                string rutaKeys = info.RutaProdKeys;
                string? huella = await Task.Run(() => NxNandManagerLogic.CalcularHuellaEmummc(info.DiscoFisico, rutaKeys));
                string? recordada = huella != null ? NxNandManagerLogic.ObtenerFirmwareRecordado(huella) : null;
                if (recordada != null)
                {
                    resultado = ResultadoFirmwareEmummc.Ok(recordada);
                    desdeRecuerdo = true;
                }
            }

            // Descartar resultados obsoletos: la unidad pudo cambiar mientras se detectaba.
            if (idOperacion != _idOperacionFirmwareEmummc) return;
            if (InfoSD.ComboDrives.SelectedItem is not SDInfo unidadActual) return;
            if (unidadActual.DiscoFisico != info.DiscoFisico) return;

            InfoSD.TxtFirmwareEmummc.Text = TextoEstadoFirmwareEmummc(resultado);
            FirmwareEmummcRealDetectado = resultado.Estado == EstadoFirmwareEmummc.Detected
                ? resultado.Version
                : null;
            _infoFirmwareAlterno = info;
            InfoSD.TxtFirmwareEmummc.ToolTip = desdeRecuerdo
                ? "Versión verificada anteriormente. La emuMMC no ha cambiado desde entonces."
                : null;
            InfoSD.BtnVerificarFirmwareAlterno.Visibility =
                resultado.Estado is EstadoFirmwareEmummc.FirmwareNotDetected
                    or EstadoFirmwareEmummc.ToolValidationFailed
                    or EstadoFirmwareEmummc.TimedOut
                    or EstadoFirmwareEmummc.Failed
                    ? Visibility.Visible : Visibility.Collapsed;
        }

        private InfoPanelDerecho? _infoFirmwareAlterno;

        /// <summary>
        /// Verificación alterna del firmware de la emuMMC, solo a petición del usuario,
        /// con pantalla de progreso que bloquea la app mientras se lee la microSD.
        /// </summary>
        private async Task VerificarFirmwareAlternoAsync()
        {
            var info = _infoFirmwareAlterno;
            if (info == null || string.IsNullOrEmpty(info.RutaProdKeys)) return;

            if (!Dialogos.Confirmar(
                    "Se leerá una copia de la emuMMC (unos 2,6 GB) para identificar su firmware.\n\n" +
                    "Tarda alrededor de 1 minuto. No se modifica nada en la microSD, pero no la retires " +
                    "ni la uses mientras dure.\n\n¿Continuar?",
                    "Verificar firmware de forma alterna", MessageBoxImage.Information))
                return;

            CancelarDeteccionFirmwareEmummc();
            _pantallaCarga.MostrarFase("Verificando firmware emuMMC", "Preparando…");
            ResultadoFirmwareEmummc resultado;
            try
            {
                var progreso = new Progress<(int Pct, string Msg)>(p => _pantallaCarga.ActualizarFase(p.Pct, p.Msg));
                resultado = await NxNandManagerLogic.ObtenerFirmwareAlternoAsync(
                    info.DiscoFisico, info.RutaProdKeys, progreso, CancellationToken.None);
            }
            catch (Exception ex)
            {
                resultado = ResultadoFirmwareEmummc.De(EstadoFirmwareEmummc.Failed, ex.Message);
            }
            finally
            {
                _pantallaCarga.Ocultar();
            }

            if (InfoSD.ComboDrives.SelectedItem is not SDInfo actual || actual.DiscoFisico != info.DiscoFisico)
                return;

            InfoSD.TxtFirmwareEmummc.Text = TextoEstadoFirmwareEmummc(resultado);
            if (resultado.Estado == EstadoFirmwareEmummc.Detected)
            {
                FirmwareEmummcRealDetectado = resultado.Version;
                InfoSD.BtnVerificarFirmwareAlterno.Visibility = Visibility.Collapsed;
            }
            else
            {
                Dialogos.Advertencia(
                    "Tampoco fue posible identificar el firmware con la verificación alterna.",
                    "Firmware emuMMC");
            }
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
            AtmosRealDetectado = info.VersionAtmos != "Desconocido" ? info.VersionAtmos : null;
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
            AtmosRealDetectado = info.VersionAtmos != "Desconocido" ? info.VersionAtmos : null;
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
            AtmosRealDetectado = info.VersionAtmos != "Desconocido" ? info.VersionAtmos : null;
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

            _ultimaLetraSdConocida = unidad.Letra;

            try
            {
                // Cambio/reinsercion de microSD: solo refresco LOCAL (modulos,
                // versiones, estados). La configuracion remota (Gist) ya esta
                // cargada en memoria/cache y no necesita revalidarse solo porque
                // cambio la SD — evita revalidaciones HTTP redundantes.
                if (_catalogoModulos == null) return;
                await _cerebro.RefrescarEstadosSinRedAsync(_catalogoModulos, unidad.Letra);

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

                // Solo re-mostrar los chips/búsqueda del catálogo si esa es la
                // vista realmente activa: evita que se "filtren" sobre otras
                // vistas (ej. Alertas y Estado, Hub CFW, Asistido) al cambiar
                // de SD mientras el usuario está en ellas.
                if (VistaCatalogo.Visibility == Visibility.Visible && _mundoSeleccionado != null)
                    ActualizarFiltrosDelMundo(_mundoSeleccionado.Id);

                RefrescarVistaActual();

                if (VistaNews.Visibility == Visibility.Visible || VistaDiagnosticoSD.Visibility == Visibility.Visible)
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
                {
                    InfoSD.ComboDrives.SelectedItem = null;
                    LimpiarInterfazSD();
                    await Task.Delay(800);
                    await ActualizarListaUnidadesAsync();
                    Dialogos.Advertencia(
                        $"La microSD \"{unidad.Etiqueta}\" fue expulsada correctamente.\nYa puedes retirarla con seguridad.",
                        "Expulsar SD ✓");
                }
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

        // ── Recuperar Micro SD corrupta / sin letra ─────────────────────────

        private async void BtnRecuperarSD_Click(object? sender, EventArgs e)
        {
            var discos = await Task.Run(EscanerDiscos.ObtenerDiscosRecuperables);
            if (discos.Count == 0)
            {
                Dialogos.Advertencia(
                    "No se detectó ningún disco extraíble.\nComprueba que la Micro SD está bien insertada en el lector.",
                    "Recuperar Micro SD");
                return;
            }

            var disco = SeleccionarDiscoRecuperable(discos);
            if (disco == null) return;

            if (!Dialogos.Confirmar(
                    $"Se BORRARÁ TODO el contenido de:\n\n{disco.Descripcion}\n\n" +
                    "Quedará como una única partición FAT32 con letra asignada.\n\n¿Es la Micro SD correcta y quieres continuar?",
                    "Confirmar recuperación", MessageBoxImage.Warning))
                return;

            _pantallaCarga.MostrarFase("Recuperando Micro SD", $"Reconstruyendo disco {disco.Numero} ({disco.TamanoGb} GB)…");
            try
            {
                var progreso = new Progress<(int Pct, string Msg)>(p => _pantallaCarga.ActualizarFase(p.Pct, p.Msg));
                string urlFat32 = ConfiguracionRemota.Ui?.UrlFat32Format ?? string.Empty;
                await new ParticionadorDiscos().RecuperarDiscoAsync(
                    disco.Numero, urlFat32, ConfiguracionLocal.EtiquetaSwitchSd, progreso);

                _pantallaCarga.Ocultar();
                await ActualizarListaUnidadesAsync();
                Servicios.Sonidos.Reproducir(EventoSonido.Exito);
                Dialogos.Info("Micro SD recuperada correctamente. Ya puedes seleccionarla y usarla.", "Recuperar Micro SD");
            }
            catch (Exception ex)
            {
                _pantallaCarga.Ocultar();
                Dialogos.Error($"No se pudo recuperar la Micro SD:\n{ex.Message}", "Recuperar Micro SD");
            }
        }

        private DiscoRecuperable? SeleccionarDiscoRecuperable(System.Collections.Generic.List<DiscoRecuperable> discos)
        {
            var lista = new ListBox
            {
                ItemsSource = discos,
                DisplayMemberPath = nameof(DiscoRecuperable.Descripcion),
                Background = new SolidColorBrush(Color.FromRgb(0x15, 0x15, 0x1C)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x35)),
                Height = 160,
                Margin = new Thickness(0, 10, 0, 10),
            };
            var btnOk = new Button { Content = "Recuperar", Width = 110, Height = 30, IsDefault = true, IsEnabled = false, Margin = new Thickness(0, 0, 8, 0) };
            var btnCancel = new Button { Content = "Cancelar", Width = 110, Height = 30, IsCancel = true };
            lista.SelectionChanged += (_, _) => btnOk.IsEnabled = lista.SelectedItem != null;

            var botones = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            botones.Children.Add(btnOk);
            botones.Children.Add(btnCancel);

            var panel = new StackPanel { Margin = new Thickness(16) };
            panel.Children.Add(new TextBlock
            {
                Text = "Selecciona la Micro SD a recuperar. Comprueba el tamaño para no confundirla con otro USB.",
                Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap,
            });
            panel.Children.Add(lista);
            panel.Children.Add(botones);

            var ventana = new Window
            {
                Title = "Recuperar Micro SD",
                Owner = this,
                Content = panel,
                Width = 620,
                SizeToContent = SizeToContent.Height,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = new SolidColorBrush(Color.FromRgb(0x0D, 0x0D, 0x12)),
            };
            btnOk.Click += (_, _) => ventana.DialogResult = true;

            return ventana.ShowDialog() == true ? lista.SelectedItem as DiscoRecuperable : null;
        }
    }
}
