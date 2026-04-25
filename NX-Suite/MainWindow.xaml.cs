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
        private bool                                _panelIzquierdoAbierto;
        private bool                                _panelDerechoAbierto;
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

            ConfigurarEventos();

            _notificadorDiscos.IniciarEscucha(this);
            _notificadorDiscos.UnidadConectada += (s, e) =>
                Dispatcher.InvokeAsync(async () => await ActualizarListaUnidadesAsync());
        }

        private void ConfigurarEventos()
        {
            MenuMundos.ListaMundos.SelectionChanged           += ListaMundos_SelectionChanged;
            FiltrosRetractil.ListaCategorias.SelectionChanged += ListaCategorias_SelectionChanged;

            FiltrosRetractil.RielMando.MouseLeftButtonDown += RielMando_Click;
            FiltrosRetractil.RielMando.MouseEnter += (s, e) => CambiarColorRiel(FiltrosRetractil.RielMando, !_panelIzquierdoAbierto, "#3E3E4F");
            FiltrosRetractil.RielMando.MouseLeave += (s, e) => CambiarColorRiel(FiltrosRetractil.RielMando, !_panelIzquierdoAbierto, "#2A2A35");

            ArsenalRetractil.RielGris.MouseLeftButtonDown += RielGris_Click;
            ArsenalRetractil.RielGris.MouseEnter += (s, e) => CambiarColorRiel(ArsenalRetractil.RielGris, !_panelDerechoAbierto, "#3E3E4F");
            ArsenalRetractil.RielGris.MouseLeave += (s, e) => CambiarColorRiel(ArsenalRetractil.RielGris, !_panelDerechoAbierto, "#2A2A35");

            InfoSD.ComboDrives.SelectionChanged += ComboDrives_SelectionChanged;
            Loaded += MainWindow_Loaded;

            VistaAsistida.InstalacionSolicitada      += VistaAsistida_InstalacionSolicitada;
            VistaAsistida.ProcesarCompletoSolicitado += VistaAsistida_ProcesarCompletoSolicitado;
            VistaAsistida.DetalleModuloSolicitado    += (_, modulo) => AbrirDetalleModulo(modulo);

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
    }
}
