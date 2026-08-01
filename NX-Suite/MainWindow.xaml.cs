using NX_Swite.Core;
using NX_Swite.Core.Configuracion;
using NX_Swite.Hardware;
using NX_Swite.Models;
using NX_Swite.UI.Controles;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace NX_Swite
{
    /// <summary>
    /// Ventana principal de NX-Swite.
    ///
    /// La code-behind está dividida en archivos parciales por área temática
    /// (todos en este mismo directorio) para mantener cada uno por debajo de
    /// ~300 líneas y hacer obvio dónde vive cada handler:
    /// <list type="bullet">
    ///   <item><c>MainWindow.SD.cs</c>          — Combo de unidades, info SD y refresco.</item>
    ///   <item><c>MainWindow.Paneles.cs</c>     — Paneles laterales retráctiles (Mando / Arsenal).</item>
    ///   <item><c>MainWindow.Navegacion.cs</c>  — Mundos, filtros y selección de vista.</item>
    ///   <item><c>MainWindow.Catalogo.cs</c>    — Tarjetas y acciones rápidas.</item>
    ///   <item><c>MainWindow.Detalle.cs</c>     — Vista de detalle y botones (instalar/borrar/web/cache).</item>
    ///   <item><c>MainWindow.Queue.cs</c>       — Overlay de cola.</item>
    ///   <item><c>MainWindow.Asistido.cs</c>    — Handlers de la <see cref="VistaAsistida"/>.</item>
    ///   <item><c>MainWindow.Ventana.cs</c>     — Drag, minimizar, cerrar y ajuste de tamaño.</item>
    /// </list>
    ///
    /// Este archivo conserva únicamente la composición: campos compartidos,
    /// constructor, suscripción de eventos y la carga inicial del catálogo.
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ISuiteController  _cerebro;
        private readonly NotificadorDiscos _notificadorDiscos = new NotificadorDiscos();
        private readonly ControladorCarga  _pantallaCarga;

        private ModuloConfig?                       _moduloActual;
        private bool                                _panelDerechoAbierto;
        private bool                                _detalleDesdeAsistido;
        private GistData?                           _datosGist;
        private ObservableCollection<ModuloConfig>? _catalogoModulos;

        private List<MundoMenuConfig>   _mundosMenu         = new();
        private List<FiltroMandoConfig> _filtrosCentroMando = new();
        private MundoMenuConfig?        _mundoSeleccionado;
        private FiltroMandoConfig?      _filtroSeleccionado;

        private bool _cargandoCatalogoInicial;

        public MainWindow()
        {
            InitializeComponent();

            AjustarTamañoVentana();

            var gestorCache = new GestorCache();
            _cerebro = new SuiteControllerFacade(new SuiteController(gestorCache));

            // Cuando el Gist cambia en background, re-aplicar las tablas remotas
            // y refrescar los campos del panel derecho que dependen de ellas.
            _cerebro.GistActualizadoEnBackground += datos =>
                Dispatcher.InvokeAsync(() => OnGistActualizadoEnBackground(datos));

            _pantallaCarga = new ControladorCarga(
                OverlayCarga, TxtCargaSubtitulo, TxtCargaDetalle, TxtCargaPorcentaje,
                BarraProgresoNeon, TxtPaso1, TxtPaso2, TxtPaso3, TxtPaso4);

            // Cuando OverlayCarga aparezca/desaparezca, aplicar/quitar blur al fondo
            // automáticamente. Garantiza coherencia visual sin que cada caller
            // tenga que recordar invocarlo manualmente.
            _pantallaCarga.AntesDeMostrar = () => AplicarBlurFondo(true);
            _pantallaCarga.DespuesDeOcultar = () => AplicarBlurFondo(false);

            ConfigurarEventos();

            _notificadorDiscos.IniciarEscucha(this);
            _notificadorDiscos.UnidadConectada += (s, e) =>
                Dispatcher.InvokeAsync(async () =>
                {
                    await ActualizarListaUnidadesAsync();
                    await RefrescarCarpetasProtegidasSiVisibleAsync();
                    ComprobarRp2040Conectado();
                });

            // Auto-cerrar overlays activos cuando se desconecta una SD: evita
            // que el usuario intente formatear/particionar/asistir sobre una
            // unidad que ya no existe.
            _notificadorDiscos.UnidadDesconectada += (s, e) =>
                Dispatcher.InvokeAsync(async () =>
                {
                    CerrarOverlaysPorDesconexionSD();
                    await RefrescarCarpetasProtegidasSiVisibleAsync();
                });
        }

        private void ConfigurarEventos()
        {
            MenuMundos.ListaMundos.SelectionChanged += ListaMundos_SelectionChanged;
            MenuMundos.LogoInicioSolicitado          += (_, _) => MostrarVistaInicio();
            ChipsFiltro.SelectionChanged            += ListaCategorias_SelectionChanged;

            ArsenalRetractil.RielGris.MouseLeftButtonDown += RielGris_Click;
            ArsenalRetractil.RielGris.MouseEnter += (s, e) => CambiarColorRiel(ArsenalRetractil.RielGris, !_panelDerechoAbierto, "#3E3E4F");
            ArsenalRetractil.RielGris.MouseLeave += (s, e) => CambiarColorRiel(ArsenalRetractil.RielGris, !_panelDerechoAbierto, "#2A2A35");

            // Apertura del overlay de formateo FAT32
            ArsenalRetractil.FormatFAT32Solicitado  += (_, __) => AbrirOverlayFormatoFAT32();
            // Apertura del overlay de particionado (sin módulos)
            ArsenalRetractil.ParticionadoSolicitado += (_, __) => AbrirOverlayParticionado();
            // Apertura del overlay de limpieza de Micro SD
            ArsenalRetractil.LimpiezaMicroSDSolicitada += ArsenalRetractil_LimpiezaMicroSDSolicitada;
            // Apertura del overlay de respaldo de llaves
            ArsenalRetractil.RespaldoLlavesSolicitado  += (_, __) => AbrirOverlayRespaldoLlaves();

            InfoSD.ComboDrives.SelectionChanged += ComboDrives_SelectionChanged;
            InfoSD.ComboDrives.SelectionChanged += async (_, _) =>
                await RefrescarCarpetasProtegidasSiVisibleAsync();
            InfoSD.ExpulsarSolicitado           += BtnExpulsarSD_Click;
            Loaded += MainWindow_Loaded;

            VistaAsistida.InstalacionSolicitada      += VistaAsistida_InstalacionSolicitada;
            VistaAsistida.ProcesarCompletoSolicitado += VistaAsistida_ProcesarCompletoSolicitado;
            VistaAsistida.DetalleModuloSolicitado    += (_, modulo) => AbrirDetalleModulo(modulo, desdeAsistido: true);
            VistaAsistida.VolverAlHubSolicitado      += (_, __) => MostrarVistaHubCFWDesdeAsistido();

            // Tarjetas del hub CFW: navegan a las vistas ya existentes.
            VistaHubCFW.AlertasSolicitado          += (_, __) => AbrirHubCFW_Alertas();
            VistaHubCFW.InstalacionSolicitada      += (_, __) => AbrirHubCFW_Instalacion();
            VistaHubCFW.CatalogoSolicitado         += (_, __) => AbrirHubCFW_Catalogo();
            VistaHubCFW.PersonalizacionSolicitada  += (_, __) => AbrirHubCFW_Personalizacion();
            VistaHubCFW.ActualizarSolicitado       += (_, __) => AbrirHubCFW_Actualizar();
            VistaHubCFW.HerramientasSolicitado     += (_, __) => AbrirHubCFW_Herramientas();

            // Sonido hover por tarjeta — se suscribe cuando el generador de items termina
            CatalogoModulos.ItemContainerGenerator.StatusChanged += (_, _) =>
            {
                if (CatalogoModulos.ItemContainerGenerator.Status != GeneratorStatus.ContainersGenerated) return;

                foreach (var item in CatalogoModulos.Items)
                {
                    var cp = CatalogoModulos.ItemContainerGenerator.ContainerFromItem(item)
                             as System.Windows.Controls.ContentPresenter;
                    if (cp != null)
                        cp.MouseEnter += Catalogo_HoverTarjeta;
                }
            };

            ListaNews.ItemContainerGenerator.StatusChanged += (_, _) => ConectarHoverNews();
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await CargarCatalogoInicialAsync();
        }

        private async Task CargarCatalogoInicialAsync()
        {
            _cargandoCatalogoInicial = true;

            string? letraSD = (InfoSD.ComboDrives.SelectedItem as SDInfo)?.Letra;
            _datosGist = await _cerebro.SincronizarTodoAsync(ConfiguracionLocal.UrlGistPrincipal, letraSD!);

            if (_datosGist == null)
            {
                _cargandoCatalogoInicial = false;
                return;
            }

            AplicarConfiguracionRemota(_datosGist);

            // Pre-cachear iconos de UI en background para que funcionen offline
            _ = Servicios.Iconos.PreCachearIconosUiAsync(_datosGist.ConfiguracionUI ?? new ConfiguracionUI());

            // Imagenes de fondo de las tarjetas del hub CFW (seccion "tarjetasHubCfw" del Gist)
            VistaHubCFW.AplicarImagenesRemotas(_datosGist.TarjetasHubCfw);

            // ── Evaluar actualización disponible ────────────────────────
            Servicios.Actualizacion.Evaluar(
                _datosGist.AppVersion,
                _datosGist.AppUpdateUrl,
                _datosGist.AppUpdateNotes);

            if (_datosGist.NyxConfigColors is not null)
                ConfiguracionRemota.NyxColors = _datosGist.NyxConfigColors;

            if (_datosGist.Recomendados?.Count > 0)
                ConfiguracionRemota.Recomendados = _datosGist.Recomendados
                    .OrderBy(r => r.Orden)
                    .ToList();

            _mundosMenu         = _datosGist.MundosMenu ?? new List<MundoMenuConfig>();
            _filtrosCentroMando = _datosGist.FiltrosCentroMando ?? new List<FiltroMandoConfig>();

            // TODO(Fase 3 - CFW): quitar esta inyección temporal en cuanto el
            // mundo "cfw" se dé de alta en el Gist remoto (MundosMenu). Permite
            // probar el hub localmente sin depender todavía del JSON remoto.
            // Ver CODEBASE_INDEX.md → "Migración de mundos a CFW".
            if (!_mundosMenu.Any(m => string.Equals(m.Tipo, "cfw_hub", StringComparison.OrdinalIgnoreCase)))
            {
                _mundosMenu.Insert(0, new MundoMenuConfig
                {
                    Id            = "cfw",
                    Nombre        = "CFW",
                    Subtitulo     = "Centro de administracion del Custom Firmware",
                    Tipo          = "cfw_hub",
                    ColorNeon     = "#00D2FF",
                    SubMundosIds  = new List<string> { "asistido", "catalogo", "personalizacion" },
                });
            }

            _catalogoModulos = new ObservableCollection<ModuloConfig>(_datosGist.Modulos ?? new List<ModuloConfig>());

            // Los mundos ya migrados al hub CFW (asistido, catalogo,
            // personalizacion) dejan de listarse por separado en el panel
            // izquierdo: ahora se navega a ellos desde las tarjetas del hub.
            // Ver CODEBASE_INDEX.md → "Migración de mundos a CFW".
            var idsAbsorbidosPorHub = _mundosMenu
                .Where(m => string.Equals(m.Tipo, "cfw_hub", StringComparison.OrdinalIgnoreCase))
                .SelectMany(m => m.SubMundosIds)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var mundosVisibles = idsAbsorbidosPorHub.Count > 0
                ? _mundosMenu.Where(m => !idsAbsorbidosPorHub.Contains(m.Id)).ToList()
                : _mundosMenu;

            MenuMundos.ListaMundos.ItemsSource   = mundosVisibles;
            MenuMundos.ListaMundos.SelectedIndex = -1;
            _mundoSeleccionado = null;

            ActualizarFiltrosDelMundo(string.Empty);
            RefrescarVistaActual();
            MostrarVistaInicio();

            // Revalida en background los iconos de mundos ya cacheados: la URL
            // del Gist normalmente no cambia, pero el asset remoto sí puede
            // cambiar (mismo link, contenido distinto). Si detecta diferencias,
            // refresca el ItemsSource para forzar la recarga de las imágenes.
            _ = RevalidarIconosMundosAsync(mundosVisibles);

            await MenuMundos.AplicarBrandingAsync(_datosGist.GlobalBranding);
            await ActualizarListaUnidadesAsync();

            // Re-sincronizar con la letra real de la SD ahora que esta disponible
            // (la primera sincronizacion no tenia letra -> no detecta modulos instalados)
            string? letraSDReal = (InfoSD.ComboDrives.SelectedItem as SDInfo)?.Letra;
            if (!string.IsNullOrEmpty(letraSDReal))
            {
                var datosConSD = await _cerebro.SincronizarTodoAsync(ConfiguracionLocal.UrlGistPrincipal, letraSDReal);
                if (datosConSD != null)
                {
                    _datosGist       = datosConSD;
                    _catalogoModulos = new ObservableCollection<ModuloConfig>(_datosGist.Modulos ?? new List<ModuloConfig>());
                    VistaHubCFW.AplicarImagenesRemotas(_datosGist.TarjetasHubCfw);
                    if (VistaNews.Visibility == Visibility.Visible)
                    {
                        CargarNewsInicio();
                        ActualizarDiagnosticoSD();
                    }
                    else
                        RefrescarVistaActual();
                }
            }

            _cargandoCatalogoInicial = false;
        }

        /// <summary>
        /// Revalida en background los iconos de mundos (<see cref="MundoMenuConfig.IconoUrl"/>)
        /// que ya estén en caché local. La URL del Gist normalmente no cambia
        /// (solo el asset detrás de ella), así que <see cref="Servicios.Iconos"/>
        /// no vuelve a tocar la red una vez cacheados. Aquí se compara el hash
        /// del contenido remoto contra la copia local y, si difiere, se
        /// refresca el <c>ItemsSource</c> de <c>ListaMundos</c> para que WPF
        /// vuelva a evaluar el binding y recargue la imagen actualizada.
        /// </summary>
        private async Task RevalidarIconosMundosAsync(List<MundoMenuConfig> mundos)
        {
            bool huboCambios = false;

            foreach (var url in mundos.Select(m => m.IconoUrl).Where(u => !string.IsNullOrWhiteSpace(u)).Distinct())
            {
                if (await Servicios.Iconos.RevalidarSiCambioAsync(url))
                    huboCambios = true;
            }

            if (huboCambios)
            {
                var actual = MenuMundos.ListaMundos.ItemsSource;
                MenuMundos.ListaMundos.ItemsSource = null;
                MenuMundos.ListaMundos.ItemsSource = actual;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // Helpers compartidos por todos los overlays (frosted glass + bloqueo)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Aplica/quita un BlurEffect a las regiones principales del MainWindow
        /// para crear el efecto frosted glass cuando se muestra cualquier
        /// overlay modal. Llamar con <c>true</c> al abrir y <c>false</c> al cerrar.
        /// </summary>
        internal void AplicarBlurFondo(bool activar)
        {
            void Aplicar(UIElement elemento)
            {
                if (!activar)
                {
                    elemento.Effect = null;
                    return;
                }

                var blur = new BlurEffect { Radius = 0, KernelType = KernelType.Gaussian };
                elemento.Effect = blur;
                blur.BeginAnimation(
                    BlurEffect.RadiusProperty,
                    new DoubleAnimation(0, 50, new Duration(TimeSpan.FromMilliseconds(320))));
            }

            Aplicar(BarraTopBar);
            Aplicar(PanelLateralIzquierdo);
            Aplicar(GridContenidoCentralContenido);
            Aplicar(GridPanelDerechoContenedor);
        }

        /// <summary>
        /// Muestra un overlay con la misma entrada visual base que el panel de
        /// dependencias: fondo borroso + fade-in del overlay.
        /// </summary>
        internal void MostrarOverlayConAnimacion(UIElement overlay)
        {
            AplicarBlurFondo(true);
            overlay.Opacity = 0;
            overlay.Visibility = Visibility.Visible;
            overlay.BeginAnimation(
                UIElement.OpacityProperty,
                new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(320))));
        }

        /// <summary>
        /// Handler que absorbe cualquier click sobre el backdrop de OverlayCarga.
        /// Garantiza que durante una operación crítica el usuario no pueda
        /// interactuar con la app por accidente.
        /// </summary>
        private void OverlayCarga_BloquearClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        /// <summary>
        /// Cierra automáticamente cualquier overlay de operación sobre SD que
        /// esté abierto cuando se desconecta una unidad. Evita estados
        /// inconsistentes (intentar formatear/particionar una SD que ya no está).
        /// La pantalla de carga (<see cref="OverlayCarga"/>) NO se cierra: si hay
        /// una operación en curso debe terminar (o fallará controladamente).
        /// </summary>
        private void CerrarOverlaysPorDesconexionSD()
        {
            if (PanelFormatoFAT32Overlay?.Visibility == Visibility.Visible)
                CerrarOverlayFormato();

            if (PanelParticionadoOverlay?.Visibility == Visibility.Visible)
                CerrarOverlayParticionado();

            if (PanelAsistidoCompletoOverlay?.Visibility == Visibility.Visible)
                CerrarOverlayAsistidoCompleto();

            if (PanelRp2040Overlay?.Visibility == Visibility.Visible && !_rp2040MostrandoFeedback)
                CerrarOverlayRp2040();

            if (PanelRespaldoLlavesOverlay?.Visibility == Visibility.Visible)
                CerrarOverlayRespaldoLlaves();
        }

        /// <summary>
        /// Vuelca <paramref name="datos"/>.ConfiguracionUI a <see cref="ConfiguracionRemota.Ui"/>
        /// y re-fusiona <see cref="MasterKeyTable"/> y <see cref="ModeloSwitchTable"/> con los
        /// valores remotos. Llamar tras cualquier sincronización con el Gist para que los datos
        /// del panel derecho siempre reflejen la tabla más reciente sin recompilar.
        /// </summary>
        /// <summary>
        /// Llamado en el hilo de UI cuando la revalidación en background del Gist
        /// detecta que el JSON cambió. Re-aplica las tablas remotas y repuebla los
        /// campos del panel derecho que dependen de <see cref="MasterKeyTable"/> y
        /// <see cref="ModeloSwitchTable"/>, para que el usuario vea datos frescos
        /// sin necesidad de reiniciar la aplicación.
        /// </summary>
        private void OnGistActualizadoEnBackground(GistData datos)
        {
            AplicarConfiguracionRemota(datos);
            VistaHubCFW.AplicarImagenesRemotas(datos.TarjetasHubCfw);

            // Repoblar modelo y región con la tabla actualizada
            string serial = InfoSD.TxtSDSerial.Text;
            var entradaModelo = NX_Swite.Core.ModeloSwitchTable.ResolverSoloRemota(serial);
            InfoSD.TxtSDModelo.Text       = entradaModelo?.Modelo ?? string.Empty;
            InfoSD.TxtSDModelo.Visibility = entradaModelo != null ? Visibility.Visible : Visibility.Collapsed;
            InfoSD.LblSDModelo.Visibility = InfoSD.TxtSDModelo.Visibility;
            InfoSD.TxtSDRegion.Text       = entradaModelo?.Region ?? string.Empty;
            InfoSD.TxtSDRegion.Visibility = entradaModelo != null ? Visibility.Visible : Visibility.Collapsed;
            InfoSD.LblSDRegion.Visibility = InfoSD.TxtSDRegion.Visibility;

            // Repoblar firmware compatible y Atmosphere mínima con la tabla actualizada
            if (InfoSD.TxtMasterKey.Visibility == Visibility.Visible &&
                !string.IsNullOrEmpty(InfoSD.TxtMasterKey.Text))
            {
                var mk = NX_Swite.Core.MasterKeyTable.BuscarSoloRemota(InfoSD.TxtMasterKey.Text);
                InfoSD.TxtFirmware.Text    = mk?.RangoHosCompatible ?? "--";
                InfoSD.TxtAtmosMinima.Text = mk?.AtmosphereDesde    ?? "--";
            }
        }

        private static void AplicarConfiguracionRemota(GistData datos)
        {
            var cfg = datos.ConfiguracionUI ?? new ConfiguracionUI();

            ConfiguracionRemota.Ui.IconoCacheUrl            = cfg.IconoCacheUrl;
            ConfiguracionRemota.Ui.ColorTextoCategoria      = cfg.ColorTextoCategoria;
            ConfiguracionRemota.Ui.IconoEliminarUrl         = cfg.IconoEliminarUrl;
            ConfiguracionRemota.Ui.IconoAgregarUrl          = cfg.IconoAgregarUrl;
            ConfiguracionRemota.Ui.IconoVolverUrl           = cfg.IconoVolverUrl;
            ConfiguracionRemota.Ui.IconoSiguienteUrl        = cfg.IconoSiguienteUrl;
            ConfiguracionRemota.Ui.IconoPaginaAnteriorUrl   = cfg.IconoPaginaAnteriorUrl;
            ConfiguracionRemota.Ui.IconoPaginaSiguienteUrl  = cfg.IconoPaginaSiguienteUrl;
            ConfiguracionRemota.Ui.IconoZipUrl              = cfg.IconoZipUrl;
            ConfiguracionRemota.Ui.IconoQueueUrl            = cfg.IconoQueueUrl;
            ConfiguracionRemota.Ui.IconoBellUrl             = cfg.IconoBellUrl;
            ConfiguracionRemota.Ui.IconoMailUrl             = cfg.IconoMailUrl;
            ConfiguracionRemota.Ui.IconoUpdateUrl           = cfg.IconoUpdateUrl;
            ConfiguracionRemota.Ui.IconoMicroSDUrl          = cfg.IconoMicroSDUrl;
            ConfiguracionRemota.Ui.IconoPaintUrl            = cfg.IconoPaintUrl;
            ConfiguracionRemota.Ui.IconoInfoUrl             = cfg.IconoInfoUrl;
            ConfiguracionRemota.Ui.IconoEjectUrl            = cfg.IconoEjectUrl;
            ConfiguracionRemota.Ui.IconoConfigUrl           = cfg.IconoConfigUrl;
            ConfiguracionRemota.Ui.IconoCarpetaUrl          = cfg.IconoCarpetaUrl;
            ConfiguracionRemota.Ui.IconoArchivoUrl          = cfg.IconoArchivoUrl;
            ConfiguracionRemota.Ui.IconoShieldUrl           = cfg.IconoShieldUrl;
            ConfiguracionRemota.Ui.IconoLogUrl              = cfg.IconoLogUrl;
            ConfiguracionRemota.Ui.UrlFat32Format           = cfg.UrlFat32Format;
            ConfiguracionRemota.Ui.VersionCompatible        = cfg.VersionCompatible;
            ConfiguracionRemota.Ui.IconoRp2040Url           = cfg.IconoRp2040Url;
            ConfiguracionRemota.Ui.UrlFirmwareRp2040        = cfg.UrlFirmwareRp2040;
            ConfiguracionRemota.Ui.VersionFirmwareRp2040    = cfg.VersionFirmwareRp2040;
            ConfiguracionRemota.Ui.NotaCertificado          = cfg.NotaCertificado;
            ConfiguracionRemota.Ui.TablaMasterKeys          = cfg.TablaMasterKeys;
            ConfiguracionRemota.Ui.TablaModelosSwitch       = cfg.TablaModelosSwitch;

            // Re-fusionar tablas — siempre desde la base embebida + remota
            MasterKeyTable.AplicarRemota(cfg.TablaMasterKeys);
            ModeloSwitchTable.AplicarRemota(cfg.TablaModelosSwitch);

            // Sección raíz "tools" del Gist: configuración de herramientas
            // externas administradas (CLI de NxNandManager, etc.)
            ConfiguracionRemota.Tools = datos.Tools ?? new ToolsConfig();
        }
    }
}
