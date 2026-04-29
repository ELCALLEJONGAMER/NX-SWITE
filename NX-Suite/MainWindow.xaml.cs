using NX_Suite.Core;
using NX_Suite.Core.Configuracion;
using NX_Suite.Hardware;
using NX_Suite.Models;
using NX_Suite.UI.Controles;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Effects;

namespace NX_Suite
{
    /// <summary>
    /// Ventana principal de NX-Suite.
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
                Dispatcher.InvokeAsync(async () => await ActualizarListaUnidadesAsync());

            // Auto-cerrar overlays activos cuando se desconecta una SD: evita
            // que el usuario intente formatear/particionar/asistir sobre una
            // unidad que ya no existe.
            _notificadorDiscos.UnidadDesconectada += (s, e) =>
                Dispatcher.InvokeAsync(CerrarOverlaysPorDesconexionSD);
        }

        private void ConfigurarEventos()
        {
            MenuMundos.ListaMundos.SelectionChanged += ListaMundos_SelectionChanged;
            ChipsFiltro.SelectionChanged            += ListaCategorias_SelectionChanged;

            ArsenalRetractil.RielGris.MouseLeftButtonDown += RielGris_Click;
            ArsenalRetractil.RielGris.MouseEnter += (s, e) => CambiarColorRiel(ArsenalRetractil.RielGris, !_panelDerechoAbierto, "#3E3E4F");
            ArsenalRetractil.RielGris.MouseLeave += (s, e) => CambiarColorRiel(ArsenalRetractil.RielGris, !_panelDerechoAbierto, "#2A2A35");

            // Apertura del overlay de formateo FAT32
            ArsenalRetractil.FormatFAT32Solicitado  += (_, __) => AbrirOverlayFormatoFAT32();
            // Apertura del overlay de particionado (sin módulos)
            ArsenalRetractil.ParticionadoSolicitado += (_, __) => AbrirOverlayParticionado();

            InfoSD.ComboDrives.SelectionChanged += ComboDrives_SelectionChanged;
            Loaded += MainWindow_Loaded;

            VistaAsistida.InstalacionSolicitada      += VistaAsistida_InstalacionSolicitada;
            VistaAsistida.ProcesarCompletoSolicitado += VistaAsistida_ProcesarCompletoSolicitado;
            VistaAsistida.DetalleModuloSolicitado    += (_, modulo) => AbrirDetalleModulo(modulo, desdeAsistido: true);

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

            var cfg = _datosGist.ConfiguracionUI ?? new ConfiguracionUI();
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
            ConfiguracionRemota.Ui.IconoInfoUrl             = cfg.IconoInfoUrl;
            ConfiguracionRemota.Ui.UrlFat32Format           = cfg.UrlFat32Format;

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

            _catalogoModulos = new ObservableCollection<ModuloConfig>(_datosGist.Modulos ?? new List<ModuloConfig>());

            MenuMundos.ListaMundos.ItemsSource   = _mundosMenu;
            MenuMundos.ListaMundos.SelectedIndex = -1;
            _mundoSeleccionado = null;

            ActualizarFiltrosDelMundo(string.Empty);
            RefrescarVistaActual();

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
                    RefrescarVistaActual();
                }
            }

            _cargandoCatalogoInicial = false;
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
            Effect? efecto = activar
                ? new BlurEffect { Radius = 6, KernelType = KernelType.Gaussian }
                : null;

            BarraTopBar.Effect                    = efecto;
            PanelLateralIzquierdo.Effect          = efecto;
            GridContenidoCentralContenido.Effect  = efecto;
            GridPanelDerechoContenedor.Effect     = efecto;
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
        }
    }
}
