using NX_Suite.Core;
using NX_Suite.Core;
using NX_Suite.Hardware;
using NX_Suite.Models;
using NX_Suite.UI;
using NX_Suite.UI.Controles;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NX_Suite
{
    public partial class MainWindow : Window
    {
        private readonly ISuiteController _cerebro;
        private readonly DiskMaster _diskMaster = new DiskMaster();
        private readonly ControladorCarga _pantallaCarga;

        private ModuloConfig? _moduloActual;
        private bool _panelIzquierdoAbierto;
        private bool _panelDerechoAbierto;
        private GistData? _datosGist;
        private ObservableCollection<ModuloConfig>? _catalogoModulos;

        private List<MundoMenuConfig> _mundosMenu = new();
        private List<FiltroMandoConfig> _filtrosCentroMando = new();
        private MundoMenuConfig? _mundoSeleccionado;
        private FiltroMandoConfig? _filtroSeleccionado;

        private bool _cargandoCatalogoInicial;

        public MainWindow()
        {
            InitializeComponent();

            AjustarTamañoVentana();

            var gestorCache = new GestorCache();
            new Core.GestorIconos(gestorCache.RutaCacheIconos);
            _cerebro = new SuiteControllerFacade(new SuiteController(gestorCache));

            _pantallaCarga = new ControladorCarga(
                OverlayCarga, TxtCargaSubtitulo, TxtCargaDetalle, TxtCargaPorcentaje,
                BarraProgresoNeon, TxtPaso1, TxtPaso2, TxtPaso3, TxtPaso4);

            ConfigurarEventos();

            _diskMaster.IniciarEscucha(this);
            _diskMaster.UnidadConectada += (s, e) =>
                Dispatcher.InvokeAsync(async () => await ActualizarListaUnidadesAsync());
        }

        private void ConfigurarEventos()
        {
            MenuMundos.ListaMundos.SelectionChanged += ListaMundos_SelectionChanged;
            FiltrosRetractil.ListaCategorias.SelectionChanged += ListaCategorias_SelectionChanged;

            FiltrosRetractil.RielMando.MouseLeftButtonDown += RielMando_Click;
            FiltrosRetractil.RielMando.MouseEnter += (s, e) => CambiarColorRiel(FiltrosRetractil.RielMando, !_panelIzquierdoAbierto, "#3E3E4F");
            FiltrosRetractil.RielMando.MouseLeave += (s, e) => CambiarColorRiel(FiltrosRetractil.RielMando, !_panelIzquierdoAbierto, "#2A2A35");

            ArsenalRetractil.RielGris.MouseLeftButtonDown += RielGris_Click;
            ArsenalRetractil.RielGris.MouseEnter += (s, e) => CambiarColorRiel(ArsenalRetractil.RielGris, !_panelDerechoAbierto, "#3E3E4F");
            ArsenalRetractil.RielGris.MouseLeave += (s, e) => CambiarColorRiel(ArsenalRetractil.RielGris, !_panelDerechoAbierto, "#2A2A35");

            InfoSD.ComboDrives.SelectionChanged += ComboDrives_SelectionChanged;
            Loaded += MainWindow_Loaded;

            VistaAsistida.InstalacionSolicitada    += VistaAsistida_InstalacionSolicitada;
            VistaAsistida.ProcesarCompletoSolicitado += VistaAsistida_ProcesarCompletoSolicitado;
            VistaAsistida.DetalleModuloSolicitado += (_, modulo) => AbrirDetalleModulo(modulo);

            // Sonido hover por tarjeta — se suscribe cuando el generador de items termina
            CatalogoModulos.ItemContainerGenerator.StatusChanged += (_, _) =>
            {
                if (CatalogoModulos.ItemContainerGenerator.Status !=
                    System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated) return;

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
            _datosGist = await _cerebro.SincronizarTodoAsync(ConfiguracionPro.UrlGistPrincipal, letraSD!);

            if (_datosGist == null)
            {
                _cargandoCatalogoInicial = false;
                return;
            }

            var cfg = _datosGist.ConfiguracionUI ?? new ConfiguracionUI();
            UIConfigService.Current.IconoCacheUrl            = cfg.IconoCacheUrl;
            UIConfigService.Current.ColorTextoCategoria      = cfg.ColorTextoCategoria;
            UIConfigService.Current.IconoEliminarUrl         = cfg.IconoEliminarUrl;
            UIConfigService.Current.IconoAgregarUrl          = cfg.IconoAgregarUrl;
            UIConfigService.Current.IconoVolverUrl           = cfg.IconoVolverUrl;
            UIConfigService.Current.IconoSiguienteUrl        = cfg.IconoSiguienteUrl;
            UIConfigService.Current.IconoPaginaAnteriorUrl   = cfg.IconoPaginaAnteriorUrl;
            UIConfigService.Current.IconoPaginaSiguienteUrl  = cfg.IconoPaginaSiguienteUrl;
            UIConfigService.Current.IconoZipUrl              = cfg.IconoZipUrl;
            UIConfigService.Current.IconoQueueUrl            = cfg.IconoQueueUrl;
            UIConfigService.Current.IconoBellUrl             = cfg.IconoBellUrl;
            UIConfigService.Current.IconoMailUrl             = cfg.IconoMailUrl;
            UIConfigService.Current.IconoUpdateUrl           = cfg.IconoUpdateUrl;

            if (_datosGist.NyxConfigColors is not null)
                UIConfigService.NyxColors = _datosGist.NyxConfigColors;

            if (_datosGist.Recomendados?.Count > 0)
                UIConfigService.Recomendados = _datosGist.Recomendados
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
                var datosConSD = await _cerebro.SincronizarTodoAsync(ConfiguracionPro.UrlGistPrincipal, letraSDReal);
                if (datosConSD != null)
                {
                    _datosGist       = datosConSD;
                    _catalogoModulos = new ObservableCollection<ModuloConfig>(_datosGist.Modulos ?? new List<ModuloConfig>());
                    RefrescarVistaActual();
                }
            }

            _cargandoCatalogoInicial = false;
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
                }
                else
                {
                    LimpiarInterfazSD();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error detectando la SD: {ex.Message}",
                    "Diagnóstico", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void LimpiarInterfazSD()
        {
            InfoSD.TxtTotalSize.Text  = "0 GB";
            InfoSD.TxtFileSystem.Text = "--";
            InfoSD.TxtSDSerial.Text   = "ID: NO DETECTADA";
            InfoSD.TxtAtmosVer.Text   = "N/A";
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
            InfoSD.TxtSDSerial.Text   = $"ID: {info.Serial}";
            InfoSD.TxtFileSystem.Foreground = info.Formato == "FAT32"
                ? (SolidColorBrush)FindResource("AcentoCian")
                : (SolidColorBrush)FindResource("AcentoRojo");

            // Solo re-sincronizar si la carga inicial ya termino
            if (_cargandoCatalogoInicial) return;

            try
            {
                _datosGist = await _cerebro.SincronizarTodoAsync(ConfiguracionPro.UrlGistPrincipal, unidad.Letra);

                if (_datosGist == null) return;

                _catalogoModulos = new ObservableCollection<ModuloConfig>(_datosGist.Modulos ?? new List<ModuloConfig>());

                if (_mundoSeleccionado != null)
                    ActualizarFiltrosDelMundo(_mundoSeleccionado.Id);

                RefrescarVistaActual();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al sincronizar con la SD: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        #region Gestión de Paneles Laterales

        private void CambiarColorRiel(Border riel, bool aplicar, string colorHex)
        {
            if (aplicar)
                riel.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
        }

        private void BtnCerrarPaneles_Click(object sender, RoutedEventArgs e)
        {
            UiAnimaciones.CerrarPaneles(
                FiltrosRetractil.RielMando, FiltrosRetractil.ContenedorMando,
                ArsenalRetractil.RielGris, ArsenalRetractil.ContenedorArsenal, FondoOscuro);

            _panelIzquierdoAbierto = false;
            _panelDerechoAbierto   = false;

            if (FiltrosRetractil.Pestanita != null) FiltrosRetractil.Pestanita.Visibility = Visibility.Visible;
            if (ArsenalRetractil.Pestanita != null)  ArsenalRetractil.Pestanita.Visibility  = Visibility.Visible;

            FiltrosRetractil.ContenedorMando.IsHitTestVisible   = false;
            ArsenalRetractil.ContenedorArsenal.IsHitTestVisible = false;
        }

        private void RielMando_Click(object sender, MouseButtonEventArgs e)
        {
            if (!_panelIzquierdoAbierto)
            {
                BtnCerrarPaneles_Click(null, null);
                UiAnimaciones.AbrirPanelIzquierdo(FiltrosRetractil.RielMando, FiltrosRetractil.ContenedorMando, FondoOscuro);
                _panelIzquierdoAbierto = true;
                if (FiltrosRetractil.Pestanita != null) FiltrosRetractil.Pestanita.Visibility = Visibility.Collapsed;
                FiltrosRetractil.ContenedorMando.IsHitTestVisible = true;
            }
            else
            {
                CerrarPanelIzquierdo();
            }
        }

        private void RielGris_Click(object sender, MouseButtonEventArgs e)
        {
            if (!_panelDerechoAbierto)
            {
                BtnCerrarPaneles_Click(null, null);
                UiAnimaciones.AbrirPanelDerecho(ArsenalRetractil.RielGris, ArsenalRetractil.ContenedorArsenal, FondoOscuro);
                _panelDerechoAbierto = true;
                if (ArsenalRetractil.Pestanita != null) ArsenalRetractil.Pestanita.Visibility = Visibility.Collapsed;
                ArsenalRetractil.ContenedorArsenal.IsHitTestVisible = true;
            }
            else
            {
                BtnCerrarPaneles_Click(null, null);
            }
        }

        private void CerrarPanelIzquierdo()
        {
            UiAnimaciones.CerrarPanelIzquierdo(FiltrosRetractil.RielMando, FiltrosRetractil.ContenedorMando);
            _panelIzquierdoAbierto = false;
            if (FiltrosRetractil.Pestanita != null) FiltrosRetractil.Pestanita.Visibility = Visibility.Visible;
            FiltrosRetractil.ContenedorMando.IsHitTestVisible = false;
        }

        #endregion

        #region Navegación y Filtrado

        private void ListaMundos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_cargandoCatalogoInicial) return;
            if (MenuMundos.ListaMundos.SelectedItem is not MundoMenuConfig mundo) return;

            _mundoSeleccionado  = mundo;
            _filtroSeleccionado = null;

            GestorSonidos.Instancia.Reproducir(EventoSonido.Navegacion);

            ActualizarEncabezadoSeccion(mundo);
            ActualizarFiltrosDelMundo(mundo.Id);
            MostrarVistaPorTipo(mundo.Tipo);
            RefrescarVistaActual();
        }

        private void ListaCategorias_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FiltrosRetractil.ListaCategorias.SelectedItem is not FiltroMandoConfig filtro)
                return;

            _filtroSeleccionado = filtro;
            RefrescarVistaActual();
        }

        /// <summary>
        /// Punto único de refresco. Los módulos se filtran siempre por etiquetas,
        /// nunca por un campo "Mundo" del módulo.
        /// </summary>
        private void RefrescarVistaActual()
        {
            if (_datosGist == null) return;

            // ── Vista asistida: la VistaAsistida gestiona su propio filtrado interno ──
            if (string.Equals(_mundoSeleccionado?.Tipo, "asistido", StringComparison.OrdinalIgnoreCase))
            {
                var nodos = _datosGist.DiagramaNodos ?? new List<NodoDiagramaConfig>();
                var todos = _datosGist.Modulos       ?? new List<ModuloConfig>();
                VistaAsistida.Cargar(nodos, todos, _mundoSeleccionado?.ModoAsistente ?? "libre");
                return;
            }

            // ── Catálogo (diagrama, catalogo y tipos futuros) ──
            IEnumerable<ModuloConfig> modulos = _datosGist.Modulos ?? Enumerable.Empty<ModuloConfig>();

            // 1. Filtro base del mundo: muestra solo los módulos que tengan
            //    al menos una de las etiquetas declaradas en EtiquetasFiltro.
            //    Si EtiquetasFiltro está vacío, se muestran todos.
            var etiquetasBase = _mundoSeleccionado?.EtiquetasFiltro;
            if (etiquetasBase?.Count > 0)
            {
                modulos = modulos.Where(m =>
                    m.Etiquetas != null &&
                    m.Etiquetas.Any(t => etiquetasBase.Any(eb =>
                        string.Equals(t, eb, StringComparison.OrdinalIgnoreCase))));
            }

            // 2. Filtro secundario: categoría seleccionada en el panel lateral.
            if (_filtroSeleccionado != null &&
                !string.IsNullOrWhiteSpace(_filtroSeleccionado.Tag) &&
                !string.Equals(_filtroSeleccionado.Tag, "all", StringComparison.OrdinalIgnoreCase))
            {
                modulos = _cerebro.FiltrarPorEtiqueta(modulos, _filtroSeleccionado.Tag);
            }

            CatalogoModulos.ItemsSource = new ObservableCollection<ModuloConfig>(modulos.ToList());
        }

        private void ActualizarEncabezadoSeccion(MundoMenuConfig mundo)
        {
            bool esPersonalizacion = string.Equals(mundo.Id, "personalizacion",
                StringComparison.OrdinalIgnoreCase);

            if (string.Equals(mundo.Tipo, "asistido", StringComparison.OrdinalIgnoreCase))
            {
                PanelTituloSeccion.Visibility              = Visibility.Collapsed;
                BtnHerramientasPersonalizacion.Visibility  = Visibility.Collapsed;
                TxtTopBarSeccion.Text                      = "Instalacion Asistida";
                return;
            }

            PanelTituloSeccion.Visibility             = Visibility.Visible;
            BtnHerramientasPersonalizacion.Visibility = esPersonalizacion
                ? Visibility.Visible : Visibility.Collapsed;

            TxtTituloSeccion.Text    = mundo.Nombre ?? "CATALOGO";
            TxtSubtituloSeccion.Text = !string.IsNullOrWhiteSpace(mundo.Subtitulo)
                ? mundo.Subtitulo
                : "Selecciona una categoria para continuar";
            TxtTopBarSeccion.Text   = mundo.Nombre ?? "Catalogo";
        }

        private void ActualizarFiltrosDelMundo(string mundoId)
        {
            if (_filtrosCentroMando == null) return;

            var filtros = _filtrosCentroMando
                .Where(f => f.Mundos == null || f.Mundos.Count == 0 ||
                            f.Mundos.Any(m => string.Equals(m, mundoId, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            FiltrosRetractil.ListaCategorias.ItemsSource   = filtros;
            FiltrosRetractil.ListaCategorias.SelectedIndex = -1;
            _filtroSeleccionado = null;
        }

        private void MostrarVistaPorTipo(string tipo)
        {
            if (string.Equals(tipo, "asistido", StringComparison.OrdinalIgnoreCase))
                MostrarVistaAsistida();
            else
                MostrarVistaCatalogo();
        }

        private void MostrarVistaCatalogo()
        {
            VistaCatalogo.Visibility = Visibility.Visible;
            VistaDetalle.Visibility  = Visibility.Collapsed;
            VistaAsistida.Visibility = Visibility.Collapsed;
        }

        private void MostrarVistaDetalle()
        {
            VistaCatalogo.Visibility = Visibility.Collapsed;
            VistaDetalle.Visibility  = Visibility.Visible;
            VistaAsistida.Visibility = Visibility.Collapsed;
        }

        private void MostrarVistaAsistida()
        {
            VistaCatalogo.Visibility = Visibility.Collapsed;
            VistaDetalle.Visibility  = Visibility.Collapsed;
            VistaAsistida.Visibility = Visibility.Visible;
        }

        private UI.VentanaPersonalizacion? _ventanaPersonalizacion;

        private void BtnHerramientasPersonalizacion_Click(object sender, RoutedEventArgs e)
        {
            if (_ventanaPersonalizacion is { IsVisible: true })
            {
                _ventanaPersonalizacion.Activate();
                return;
            }
            _ventanaPersonalizacion = new UI.VentanaPersonalizacion();
            _ventanaPersonalizacion.Show();
        }

        #endregion

        #region Tarjetas

        private void Catalogo_HoverTarjeta(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_cargandoCatalogoInicial) return;
            GestorSonidos.Instancia.Reproducir(EventoSonido.Hover);
        }

        private void Catalogo_ClickTarjeta(object sender, MouseButtonEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is ModuloConfig modulo)
                AbrirDetalleModulo(modulo);
        }

        private async void Catalogo_ClickBoton(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not Button btn || btn.DataContext is not ModuloConfig modulo)
                return;

            string? letraSD = (InfoSD.ComboDrives.SelectedItem as SDInfo)?.Letra;

            switch (modulo.AccionRapida)
            {
                case AccionRapidaModulo.Instalar:
                case AccionRapidaModulo.Actualizar:
                case AccionRapidaModulo.Reinstalar:
                    // No se reproduce Click — Instalar sound lo cubre
                    if (string.IsNullOrEmpty(letraSD))
                    {
                        MessageBox.Show("No hay ninguna SD seleccionada.", "Advertencia",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    await EjecutarInstalacionRapidaAsync(modulo, letraSD);
                    break;

                case AccionRapidaModulo.Eliminar:
                    GestorSonidos.Instancia.Reproducir(EventoSonido.Click);
                    if (string.IsNullOrEmpty(letraSD)) return;
                    await EjecutarEliminacionRapidaAsync(modulo, letraSD);
                    break;

                default:
                    GestorSonidos.Instancia.Reproducir(EventoSonido.Click);
                    ConfirmarLimpiezaCache(modulo);
                    break;
            }
        }

        private async Task EjecutarInstalacionRapidaAsync(ModuloConfig modulo, string letraSD)
        {
            const double VelocidadBase = 0.0018;
            const double VelocidadMax  = 0.032;

            double targetProgress = 0.0;
            double velocidad      = VelocidadBase;

            modulo.EstaInstalando      = true;
            modulo.ProgresoInstalacion = 0.0;

            GestorSonidos.Instancia.Reproducir(EventoSonido.Instalar);

            var itemQueue = GestorQueue.Instancia.AgregarItem($"Instalando {modulo.Nombre}");

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            timer.Tick += (_, _) =>
            {
                double diff = targetProgress - modulo.ProgresoInstalacion;

                if (diff <= 0.0005)
                {
                    modulo.ProgresoInstalacion = targetProgress;
                    return;
                }

                // Velocidad objetivo: rápida si hay mucho gap, mínima si está cerca
                double vObjetivo = Math.Clamp(diff * 0.18, VelocidadBase, VelocidadMax);

                // La velocidad se suaviza sola (sin acelerones ni frenazos bruscos)
                velocidad += (vObjetivo - velocidad) * 0.10;

                modulo.ProgresoInstalacion = Math.Min(targetProgress, modulo.ProgresoInstalacion + velocidad);
            };
            timer.Start();

            var progreso = new Progress<EstadoProgreso>(estado =>
            {
                targetProgress = estado.Porcentaje / 100.0;
                GestorQueue.Instancia.ActualizarItem(itemQueue, estado.Porcentaje, estado.TareaActual);
            });

            try
            {
                var resultado = await _cerebro.InstalarModuloAsync(modulo, letraSD, progreso, itemQueue.Token);

                // Llevar al 100% y esperar que el relleno llegue visualmente (máx 2s)
                targetProgress = 1.0;
                var limite = DateTime.Now.AddSeconds(2);
                while (modulo.ProgresoInstalacion < 0.995 && DateTime.Now < limite)
                    await Task.Delay(16);

                timer.Stop();
                modulo.ProgresoInstalacion = 1.0;
                await Task.Delay(300);

                modulo.EstaInstalando      = false;
                modulo.ProgresoInstalacion = 0.0;

                if (_catalogoModulos != null)
                    _cerebro.ActualizarEstadoCacheCatalogo(_catalogoModulos);

                await ActualizarListaUnidadesAsync();
                RefrescarVistaActual();

                if (!resultado.Exito)
                {
                    GestorSonidos.Instancia.Reproducir(EventoSonido.Error);
                    GestorQueue.Instancia.ErrorItem(itemQueue, resultado.MensajeError);
                    MessageBox.Show($"Error:\n{resultado.MensajeError}", "Fallo",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    GestorSonidos.Instancia.Reproducir(EventoSonido.Exito);
                    GestorQueue.Instancia.CompletarItem(itemQueue);
                }
            }
            catch (OperationCanceledException)
            {
                timer.Stop();
                modulo.EstaInstalando      = false;
                modulo.ProgresoInstalacion = 0.0;
                GestorQueue.Instancia.CancelarItem(itemQueue);
            }
            catch (Exception ex)
            {
                timer.Stop();
                modulo.EstaInstalando      = false;
                modulo.ProgresoInstalacion = 0.0;
                GestorQueue.Instancia.ErrorItem(itemQueue, ex.Message);
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task EjecutarEliminacionRapidaAsync(ModuloConfig modulo, string letraSD)
        {
            var resp = MessageBox.Show(
                $"¿Eliminar {modulo.Nombre} de la SD?",
                "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (resp != MessageBoxResult.Yes) return;

            var itemQueue = GestorQueue.Instancia.AgregarItem($"Eliminando {modulo.Nombre}");
            GestorQueue.Instancia.ActualizarItem(itemQueue, 0, "Eliminando archivos de la SD...");

            try
            {
                bool exito = await _cerebro.DesinstalarModuloAsync(modulo, letraSD);
                await ActualizarListaUnidadesAsync();
                RefrescarVistaActual();

                if (!exito)
                {
                    GestorQueue.Instancia.ErrorItem(itemQueue, "Error al eliminar algunos archivos");
                    MessageBox.Show("Hubo un error al eliminar algunos archivos.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    GestorQueue.Instancia.CompletarItem(itemQueue);
                }
            }
            catch (Exception ex)
            {
                GestorQueue.Instancia.ErrorItem(itemQueue, ex.Message);
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ConfirmarLimpiezaCache(ModuloConfig modulo)
        {
            var resp = MessageBox.Show(
                $"¿Eliminar caché local de {modulo.Nombre}?",
                "Limpiar Caché", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (resp != MessageBoxResult.Yes) return;

            try
            {
                _cerebro.LimpiarCacheModulo(modulo);
                if (_catalogoModulos != null)
                    _cerebro.ActualizarEstadoCacheCatalogo(_catalogoModulos);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Vista de Detalle

        private void AbrirDetalleModulo(ModuloConfig modulo)
        {
            if (modulo == null) return;

            _moduloActual = modulo;

            // ── Textos básicos ──
            TxtTituloDetalle.Text  = modulo.Nombre ?? string.Empty;
            TxtDescDetalle.Text    = modulo.Descripcion ?? string.Empty;
            TxtVersionDetalle.Text = modulo.Versiones?.Count > 0
                ? $"Versión: {modulo.Versiones[0].Version}"
                : "Versión: --";

            // ── Badge de estado ──
            if (modulo.EstaInstaladoEnSd)
            {
                BadgeEstadoDetalle.Background  = new SolidColorBrush(Color.FromArgb(30, 0, 210, 100));
                BadgeEstadoDetalle.BorderBrush = new SolidColorBrush(Color.FromArgb(180, 0, 210, 100));
                BadgeEstadoDetalle.BorderThickness = new Thickness(1);
                TxtEstadoDetalle.Text       = "● INSTALADO";
                TxtEstadoDetalle.Foreground = new SolidColorBrush(Color.FromArgb(255, 0, 210, 100));
                TxtVersionInstaladaDetalle.Text = !string.IsNullOrWhiteSpace(modulo.VersionInstalada) &&
                                                   modulo.VersionInstalada is not ("No detectado" or "No instalado")
                    ? $"v{modulo.VersionInstalada} instalada"
                    : string.Empty;
            }
            else
            {
                BadgeEstadoDetalle.Background  = new SolidColorBrush(Color.FromArgb(30, 189, 0, 255));
                BadgeEstadoDetalle.BorderBrush = new SolidColorBrush(Color.FromArgb(120, 189, 0, 255));
                BadgeEstadoDetalle.BorderThickness = new Thickness(1);
                TxtEstadoDetalle.Text       = "○ NO INSTALADO";
                TxtEstadoDetalle.Foreground = new SolidColorBrush(Color.FromArgb(255, 189, 0, 255));
                TxtVersionInstaladaDetalle.Text = string.Empty;
            }

            // ── Etiquetas ──
            ListaEtiquetasDetalle.ItemsSource = modulo.Etiquetas?.Count > 0 ? modulo.Etiquetas : null;

            // ── Screenshots ──
            bool tieneScreenshots = modulo.ScreenshotsUrl?.Count > 0;
            PanelScreenshots.Visibility  = tieneScreenshots ? Visibility.Visible : Visibility.Collapsed;
            if (tieneScreenshots) ListaScreenshots.ItemsSource = modulo.ScreenshotsUrl;

            // ── Cache section ──
            RefrescarSeccionCache(modulo);

            // ── Imagen del icono ──
            if (!string.IsNullOrEmpty(modulo.IconoUrl))
            {
                try
                {
                    string? rutaLocal = Core.GestorIconos.Instancia?.ObtenerRutaLocal(modulo.IconoUrl);
                    string uriStr     = rutaLocal ?? modulo.IconoUrl;
                    var bmp = new BitmapImage(new Uri(uriStr));
                    ImgDetalle.Source = bmp;

                    if (rutaLocal == null)
                        _ = Core.GestorIconos.Instancia?.DescargarSiNoExisteAsync(modulo.IconoUrl);
                }
                catch { ImgDetalle.Source = null; }
            }
            else ImgDetalle.Source = null;

            // ── Banner (usa BannerUrl si existe, si no usa el icono como fondo) ──
            string bannerSrc = !string.IsNullOrEmpty(modulo.BannerUrl) ? modulo.BannerUrl : modulo.IconoUrl;
            if (!string.IsNullOrEmpty(bannerSrc))
            {
                try
                {
                    string? rutaLocal = Core.GestorIconos.Instancia?.ObtenerRutaLocal(bannerSrc);
                    string uriStr     = rutaLocal ?? bannerSrc;
                    BrushBannerDetalle.ImageSource = new BitmapImage(new Uri(uriStr));
                }
                catch { BrushBannerDetalle.ImageSource = null; }
            }
            else BrushBannerDetalle.ImageSource = null;

            // ── Visibilidad inteligente de botones ──
            ActualizarBotonesDetalle(modulo);

            VistaCatalogo.Visibility = Visibility.Collapsed;
            VistaAsistida.Visibility = Visibility.Collapsed;
            UiAnimaciones.MostrarDetalle(VistaDetalle);
            BtnCerrarPaneles_Click(null, null);
        }

        private void ActualizarBotonesDetalle(ModuloConfig modulo)
        {
            bool instalado     = modulo.EstaInstaladoEnSd;
            bool tieneUpdate   = modulo.TieneActualizacion;
            bool tieneSitioWeb = !string.IsNullOrWhiteSpace(modulo.UrlOficial);

            BtnInstalarDetalle.Visibility        = instalado ? Visibility.Collapsed : Visibility.Visible;
            BtnActualizarDetalle.Visibility      = (instalado && tieneUpdate) ? Visibility.Visible : Visibility.Collapsed;
            BtnBorrarDetalle.Visibility          = instalado ? Visibility.Visible : Visibility.Collapsed;
            BtnAbrirUbicacionDetalle.Visibility  = instalado ? Visibility.Visible : Visibility.Collapsed;
            BtnSitioWebDetalle.Visibility        = tieneSitioWeb ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Busca el módulo actual en los datos recién sincronizados y refresca
        /// el badge de estado y los botones de acción sin reiniciar animaciones.
        /// </summary>
        private void RefrescarEstadoDetalle()
        {
            if (_moduloActual == null || _datosGist?.Modulos == null) return;

            var refrescado = _datosGist.Modulos.FirstOrDefault(m => m.Id == _moduloActual.Id);
            if (refrescado == null) return;

            _moduloActual = refrescado;

            // Actualizar badge
            if (refrescado.EstaInstaladoEnSd)
            {
                BadgeEstadoDetalle.Background  = new SolidColorBrush(Color.FromArgb(30, 0, 210, 100));
                BadgeEstadoDetalle.BorderBrush = new SolidColorBrush(Color.FromArgb(180, 0, 210, 100));
                BadgeEstadoDetalle.BorderThickness = new Thickness(1);
                TxtEstadoDetalle.Text       = "● INSTALADO";
                TxtEstadoDetalle.Foreground = new SolidColorBrush(Color.FromArgb(255, 0, 210, 100));
                TxtVersionInstaladaDetalle.Text = !string.IsNullOrWhiteSpace(refrescado.VersionInstalada) &&
                                                   refrescado.VersionInstalada is not ("No detectado" or "No instalado")
                    ? $"v{refrescado.VersionInstalada} instalada"
                    : string.Empty;
            }
            else
            {
                BadgeEstadoDetalle.Background  = new SolidColorBrush(Color.FromArgb(30, 189, 0, 255));
                BadgeEstadoDetalle.BorderBrush = new SolidColorBrush(Color.FromArgb(120, 189, 0, 255));
                BadgeEstadoDetalle.BorderThickness = new Thickness(1);
                TxtEstadoDetalle.Text       = "○ NO INSTALADO";
                TxtEstadoDetalle.Foreground = new SolidColorBrush(Color.FromArgb(255, 189, 0, 255));
                TxtVersionInstaladaDetalle.Text = string.Empty;
            }

            ActualizarBotonesDetalle(refrescado);
            RefrescarSeccionCache(refrescado);
        }

        private void RefrescarSeccionCache(ModuloConfig modulo)
        {
            if (modulo == null) return;
            bool zipExiste     = !string.IsNullOrEmpty(modulo.RutaCacheZip)
                                 && System.IO.File.Exists(modulo.RutaCacheZip);
            bool carpetaExiste = !string.IsNullOrEmpty(modulo.RutaCacheCarpeta)
                                 && (System.IO.Directory.Exists(modulo.RutaCacheCarpeta)
                                     || System.IO.File.Exists(modulo.RutaCacheCarpeta));

            FilaCacheZip.Visibility      = zipExiste     ? Visibility.Visible : Visibility.Collapsed;
            FilaCacheCarpeta.Visibility  = carpetaExiste ? Visibility.Visible : Visibility.Collapsed;
            PanelCacheDetalle.Visibility = (zipExiste || carpetaExiste) ? Visibility.Visible : Visibility.Collapsed;
            TxtTamanoZip.Text      = zipExiste     ? "…" : string.Empty;
            TxtTamanoCarpeta.Text  = carpetaExiste ? "…" : string.Empty;

            if (zipExiste || carpetaExiste)
                _ = ComputarTamanosCacheAsync(modulo, zipExiste, carpetaExiste);
        }

        private async Task ComputarTamanosCacheAsync(ModuloConfig modulo, bool zipExiste, bool carpetaExiste)
        {
            string tamZip = string.Empty, tamCarpeta = string.Empty;
            await Task.Run(() =>
            {
                if (zipExiste && System.IO.File.Exists(modulo.RutaCacheZip))
                    tamZip = FormatBytes(new System.IO.FileInfo(modulo.RutaCacheZip).Length);
                if (carpetaExiste)
                {
                    if (System.IO.Directory.Exists(modulo.RutaCacheCarpeta))
                    {
                        long total = new System.IO.DirectoryInfo(modulo.RutaCacheCarpeta)
                            .GetFiles("*", System.IO.SearchOption.AllDirectories)
                            .Sum(f => f.Length);
                        tamCarpeta = FormatBytes(total);
                    }
                    else if (System.IO.File.Exists(modulo.RutaCacheCarpeta))
                        tamCarpeta = FormatBytes(new System.IO.FileInfo(modulo.RutaCacheCarpeta).Length);
                }
            });
            TxtTamanoZip.Text     = string.IsNullOrEmpty(tamZip)     ? string.Empty : $"({tamZip})";
            TxtTamanoCarpeta.Text = string.IsNullOrEmpty(tamCarpeta) ? string.Empty : $"({tamCarpeta})";
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1_073_741_824L) return $"{bytes / 1_073_741_824.0:F1} GB";
            if (bytes >= 1_048_576L)     return $"{bytes / 1_048_576.0:F1} MB";
            if (bytes >= 1024L)          return $"{bytes / 1024.0:F0} KB";
            return $"{bytes} B";
        }

        private void BtnBorrarCacheZip_Click(object sender, RoutedEventArgs e)
        {
            if (_moduloActual == null || string.IsNullOrEmpty(_moduloActual.RutaCacheZip)) return;
            try
            {
                if (System.IO.File.Exists(_moduloActual.RutaCacheZip))
                    System.IO.File.Delete(_moduloActual.RutaCacheZip);
                if (_catalogoModulos != null) _cerebro.ActualizarEstadoCacheCatalogo(_catalogoModulos);
                RefrescarSeccionCache(_moduloActual);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al borrar ZIP: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnBorrarCacheCarpeta_Click(object sender, RoutedEventArgs e)
        {
            if (_moduloActual == null || string.IsNullOrEmpty(_moduloActual.RutaCacheCarpeta)) return;
            try
            {
                if (System.IO.Directory.Exists(_moduloActual.RutaCacheCarpeta))
                    System.IO.Directory.Delete(_moduloActual.RutaCacheCarpeta, true);
                else if (System.IO.File.Exists(_moduloActual.RutaCacheCarpeta))
                    System.IO.File.Delete(_moduloActual.RutaCacheCarpeta);
                if (_catalogoModulos != null) _cerebro.ActualizarEstadoCacheCatalogo(_catalogoModulos);
                RefrescarSeccionCache(_moduloActual);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al borrar caché extraído: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnVolver_Click(object sender, RoutedEventArgs e)
        {
            _moduloActual = null;
            UiAnimaciones.OcultarDetalle(VistaDetalle, () =>
            {
                VistaCatalogo.Visibility = Visibility.Visible;
                UiAnimaciones.FadeInCatalogo(VistaCatalogo);
            });
        }

        private async void BtnInstalar_Click(object sender, RoutedEventArgs e)
        {
            if (_moduloActual == null) return;

            string? letraSD = (InfoSD.ComboDrives.SelectedItem as SDInfo)?.Letra;
            if (string.IsNullOrEmpty(letraSD))
            {
                MessageBox.Show("No hay ninguna SD seleccionada para instalar.", "Advertencia",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var itemQueue = GestorQueue.Instancia.AgregarItem($"Instalando {_moduloActual.Nombre}");

            try
            {
                _pantallaCarga.Mostrar($"Instalando {_moduloActual.Nombre}");

                // Reportador compuesto: actualiza overlay Y cola
                var reportadorOverlay = _pantallaCarga.ObtenerReportador();
                var progreso = new Progress<EstadoProgreso>(p =>
                {
                    ((IProgress<EstadoProgreso>)reportadorOverlay).Report(p);
                    GestorQueue.Instancia.ActualizarItem(itemQueue, p.Porcentaje, p.TareaActual);
                });

                var resultado = await _cerebro.InstalarModuloAsync(_moduloActual, letraSD, progreso, itemQueue.Token);

                if (resultado.Exito)
                {
                    await Task.Delay(1000);
                    _pantallaCarga.Ocultar();
                    GestorQueue.Instancia.CompletarItem(itemQueue);

                    if (_catalogoModulos != null)
                        _cerebro.ActualizarEstadoCacheCatalogo(_catalogoModulos);

                    await ActualizarListaUnidadesAsync();
                    RefrescarVistaActual();
                    RefrescarEstadoDetalle();

                    MessageBox.Show($"¡{_moduloActual?.Nombre} se ha instalado correctamente!", "Éxito",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    _pantallaCarga.Ocultar();
                    GestorQueue.Instancia.ErrorItem(itemQueue, resultado.MensajeError);
                    MessageBox.Show($"Error durante la instalación:\n\n{resultado.MensajeError}", "Fallo",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (OperationCanceledException)
            {
                _pantallaCarga.Ocultar();
                GestorQueue.Instancia.CancelarItem(itemQueue);
                MessageBox.Show($"Instalación de {_moduloActual?.Nombre} cancelada.", "Cancelado",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _pantallaCarga.Ocultar();
                GestorQueue.Instancia.ErrorItem(itemQueue, ex.Message);
                MessageBox.Show($"Excepción en la interfaz: {ex.Message}", "Error Crítico",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnBorrar_Click(object sender, RoutedEventArgs e)
        {
            if (_moduloActual == null) return;

            var respuesta = MessageBox.Show(
                $"¿Estás seguro de que deseas eliminar {_moduloActual.Nombre} de la SD?",
                "Confirmar Eliminación", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (respuesta != MessageBoxResult.Yes) return;

            string? letraSD = (InfoSD.ComboDrives.SelectedItem as SDInfo)?.Letra;
            if (string.IsNullOrEmpty(letraSD)) return;

            try
            {
                bool exito = await _cerebro.DesinstalarModuloAsync(_moduloActual, letraSD);

                if (exito)
                {
                    if (_catalogoModulos != null)
                        _cerebro.ActualizarEstadoCacheCatalogo(_catalogoModulos);

                    await ActualizarListaUnidadesAsync();
                    RefrescarVistaActual();
                    RefrescarEstadoDetalle();

                    MessageBox.Show($"¡{_moduloActual?.Nombre} se ha eliminado!", "Éxito",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Hubo un error al intentar borrar algunos archivos.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al eliminar: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnAbrirUbicacion_Click(object sender, RoutedEventArgs e)
        {
            if (_moduloActual == null) return;

            string? letraSD = (InfoSD.ComboDrives.SelectedItem as SDInfo)?.Letra;
            if (string.IsNullOrEmpty(letraSD))
            {
                MessageBox.Show("No hay ninguna SD seleccionada.", "Advertencia",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // letraSD viene en formato "H:\" desde DriveInfo.GetDrives(), no agregar separadores extra
                string raizSD        = letraSD.TrimEnd('\\') + "\\";
                string carpetaDestino = raizSD;

                // 1. Prioridad: primer archivo con SHA256 en FirmasDeteccion
                var archivoSha = _moduloActual.FirmasDeteccion?
                    .SelectMany(f => f.Archivos ?? Enumerable.Empty<ArchivoCritico>())
                    .FirstOrDefault(a => !string.IsNullOrWhiteSpace(a.SHA256) &&
                                         !string.IsNullOrWhiteSpace(a.Ruta));

                if (archivoSha != null)
                {
                    string relativa    = archivoSha.Ruta.TrimStart('/', '\\').Replace('/', '\\');
                    string rutaArchivo = System.IO.Path.Combine(raizSD, relativa);
                    string? dir        = System.IO.Path.GetDirectoryName(rutaArchivo);
                    if (!string.IsNullOrEmpty(dir) && System.IO.Directory.Exists(dir))
                        carpetaDestino = dir;
                }
                // 2. Fallback: primer archivo de FirmasDeteccion sin SHA256
                else
                {
                    var primerArchivo = _moduloActual.FirmasDeteccion?
                        .SelectMany(f => f.Archivos ?? Enumerable.Empty<ArchivoCritico>())
                        .FirstOrDefault(a => !string.IsNullOrWhiteSpace(a.Ruta));

                    if (primerArchivo != null)
                    {
                        string relativa    = primerArchivo.Ruta.TrimStart('/', '\\').Replace('/', '\\');
                        string rutaArchivo = System.IO.Path.Combine(raizSD, relativa);
                        string? dir        = System.IO.File.Exists(rutaArchivo)
                            ? System.IO.Path.GetDirectoryName(rutaArchivo)
                            : (System.IO.Directory.Exists(rutaArchivo) ? rutaArchivo : null);
                        if (!string.IsNullOrEmpty(dir) && System.IO.Directory.Exists(dir))
                            carpetaDestino = dir;
                    }
                }

                // Seleccionar el archivo concreto en el explorador si existe
                string? archivoFinal = archivoSha != null
                    ? System.IO.Path.Combine(raizSD,
                          archivoSha.Ruta.TrimStart('/', '\\').Replace('/', '\\'))
                    : null;

                if (archivoFinal != null && System.IO.File.Exists(archivoFinal))
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{archivoFinal}\"");
                else
                    System.Diagnostics.Process.Start("explorer.exe", carpetaDestino);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo abrir la ubicación: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnAbrirQueue_Click(object sender, RoutedEventArgs e)
            => PanelQueueOverlay.Visibility = Visibility.Visible;

        private void BtnCerrarQueue_Click(object sender, RoutedEventArgs e)
            => PanelQueueOverlay.Visibility = Visibility.Collapsed;

        private void BtnLimpiarQueue_Click(object sender, RoutedEventArgs e)
            => GestorQueue.Instancia.LimpiarCompletados();

        private void BtnCancelarItemQueue_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ItemQueue item)
                GestorQueue.Instancia.CancelarItem(item);
        }

        private void BtnSitioWeb_Click(object sender, RoutedEventArgs e)
        {
            if (_moduloActual == null || string.IsNullOrWhiteSpace(_moduloActual.UrlOficial)) return;

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName        = _moduloActual.UrlOficial,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo abrir el sitio web: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Vista Asistida — Instalación

        private async void VistaAsistida_InstalacionSolicitada(object? sender, SesionAsistida sesion)
        {
            string? letraSD = (InfoSD.ComboDrives.SelectedItem as SDInfo)?.Letra;
            if (string.IsNullOrEmpty(letraSD))
            {
                MessageBox.Show("No hay ninguna SD seleccionada.", "Advertencia",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var todosAInstalar = sesion.Modulos;

            if (todosAInstalar.Count == 0) return;

            try
            {
                int total    = todosAInstalar.Count;
                int fallidos = 0;

                for (int i = 0; i < total; i++)
                {
                    var modulo = todosAInstalar[i];
                    _pantallaCarga.Mostrar($"Instalando {modulo.Nombre} ({i + 1}/{total})");

                    var resultado = await _cerebro.InstalarModuloAsync(modulo, letraSD, _pantallaCarga.ObtenerReportador());

                    if (!resultado.Exito)
                    {
                        fallidos++;
                        var continuar = MessageBox.Show(
                            $"Error instalando {modulo.Nombre}:\n{resultado.MensajeError}\n\n¿Continuar con los demás?",
                            "Error parcial", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                        if (continuar == MessageBoxResult.No) break;
                    }
                }

                await Task.Delay(500);
                _pantallaCarga.Ocultar();

                if (_catalogoModulos != null)
                    _cerebro.ActualizarEstadoCacheCatalogo(_catalogoModulos);

                await ActualizarListaUnidadesAsync();
                RefrescarVistaActual();

                string mensaje = fallidos == 0
                    ? $"¡Instalación completada! {total} módulo(s) instalado(s)."
                    : $"Instalación finalizada con {fallidos} error(es) de {total}.";

                MessageBox.Show(mensaje, fallidos == 0 ? "Éxito" : "Completado con errores",
                    MessageBoxButton.OK,
                    fallidos == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                _pantallaCarga.Ocultar();
                MessageBox.Show($"Error crítico: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void VistaAsistida_ProcesarCompletoSolicitado(object? sender, NX_Suite.UI.Controles.ProcesarCompletoArgs args)
        {
            // La ventana ya se cerro — todos los datos vienen en args.
            string? letraSD     = args.LetraSD;
            int     numeroDisco = args.NumeroDisco;
            var     modulos     = args.Modulos;
            int     total       = modulos.Count;
            int     gbEmuMMC    = args.GbEmuMMC;

            if (string.IsNullOrEmpty(letraSD) || numeroDisco < 0)
            {
                MessageBox.Show("No se pudo identificar la SD o el disco fisico.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Abrir el panel de cola automaticamente
            PanelQueueOverlay.Visibility = Visibility.Visible;

            var itemPrincipal = GestorQueue.Instancia.AgregarItem($"Asistido Completo — disco {numeroDisco}");
            GestorSonidos.Instancia.Reproducir(EventoSonido.Instalar);

            int fallidos = 0;
            try
            {
                // ── FASE 1: Particionado y formateo ──────────────────────
                GestorQueue.Instancia.ActualizarItem(itemPrincipal, 2,
                    $"Particionando disco {numeroDisco} — emuMMC: {gbEmuMMC} GB…");

                var disk = new NX_Suite.Hardware.DiskMaster();
                var progresoDisk = new Progress<(int Pct, string Msg)>(p =>
                {
                    GestorQueue.Instancia.ActualizarItem(itemPrincipal, (int)(p.Pct * 0.45), p.Msg);
                });

                string urlFat32 = _datosGist?.ConfiguracionUI?.UrlFat32Format ?? string.Empty;
                await disk.ParticionarYFormatearAsync(numeroDisco, gbEmuMMC, urlFat32, progresoDisk);

                // Tras el particionado+formateo, Windows asigna la letra automáticamente.
                // Buscamos la nueva partición SWITCH SD por etiqueta o por disco físico.
                await Task.Delay(2000);
                await ActualizarListaUnidadesAsync();
                var unidades = disk.ObtenerUnidadesRemovibles();
                var sdNueva  = unidades.FirstOrDefault(u =>
                    u.Etiqueta.Equals("SWITCH SD", StringComparison.OrdinalIgnoreCase) ||
                    u.DiscoFisico == numeroDisco);
                if (sdNueva?.Letra != null) letraSD = sdNueva.Letra;

                GestorQueue.Instancia.ActualizarItem(itemPrincipal, 45, "Particionado OK. Instalando modulos…");

                // ── FASE 2: Instalacion de modulos ───────────────────────
                for (int i = 0; i < total; i++)
                {
                    var modulo  = modulos[i];
                    int pctBase = 45 + (int)((double)i / total * 55);
                    int pctSig  = 45 + (int)((double)(i + 1) / total * 55);

                    GestorQueue.Instancia.ActualizarItem(itemPrincipal, pctBase,
                        $"Instalando {modulo.Nombre} ({i + 1}/{total})…");

                    var progreso = new Progress<EstadoProgreso>(estado =>
                    {
                        int pct = pctBase + (int)((pctSig - pctBase) * estado.Porcentaje / 100.0);
                        GestorQueue.Instancia.ActualizarItem(itemPrincipal, pct, estado.TareaActual);
                    });

                    var resultado = await _cerebro.InstalarModuloAsync(modulo, letraSD, progreso);
                    if (!resultado.Exito)
                    {
                        fallidos++;
                        GestorQueue.Instancia.ActualizarItem(itemPrincipal, pctSig,
                            $"Error en {modulo.Nombre}: {resultado.MensajeError}");
                    }
                }

                if (_catalogoModulos != null)
                    _cerebro.ActualizarEstadoCacheCatalogo(_catalogoModulos);

                await ActualizarListaUnidadesAsync();
                RefrescarVistaActual();

                if (fallidos == 0)
                {
                    GestorQueue.Instancia.CompletarItem(itemPrincipal);
                    GestorSonidos.Instancia.Reproducir(EventoSonido.Exito);
                }
                else
                {
                    GestorQueue.Instancia.ErrorItem(itemPrincipal,
                        $"Completado con {fallidos} error(es) de {total} modulos");
                    GestorSonidos.Instancia.Reproducir(EventoSonido.Error);
                }
            }
            catch (Exception ex)
            {
                GestorQueue.Instancia.ErrorItem(itemPrincipal, ex.Message);
                GestorSonidos.Instancia.Reproducir(EventoSonido.Error);
            }
        }

        #endregion

        #region Controles de Ventana

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
            GestorSonidos.Instancia.Reproducir(EventoSonido.Cerrar);
            await Task.Delay(600);
            Application.Current.Shutdown();
        }

        private async void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            GestorSonidos.Instancia.Reproducir(EventoSonido.Cerrar);
            await Task.Delay(600);
            Application.Current.Shutdown();
        }

        #endregion
    }
}