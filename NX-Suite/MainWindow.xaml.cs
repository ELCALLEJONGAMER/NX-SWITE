using NX_Suite.Core;
using NX_Suite.Hardware;
using NX_Suite.Models;
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

            VistaAsistida.InstalacionSolicitada += VistaAsistida_InstalacionSolicitada;

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
            UIConfigService.Current.IconoCacheUrl       = cfg.IconoCacheUrl;
            UIConfigService.Current.ColorTextoCategoria = cfg.ColorTextoCategoria;
            UIConfigService.Current.IconoEliminarUrl    = cfg.IconoEliminarUrl;
            UIConfigService.Current.IconoAgregarUrl     = cfg.IconoAgregarUrl;
            UIConfigService.Current.IconoVolverUrl      = cfg.IconoVolverUrl;

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

            var info = _cerebro.ObtenerInfoPanel(unidad, _catalogoModulos.ToList());

            InfoSD.TxtTotalSize.Text  = info.Capacidad;
            InfoSD.TxtFileSystem.Text = info.Formato;
            InfoSD.TxtAtmosVer.Text   = info.VersionAtmos;
            InfoSD.TxtSDSerial.Text   = $"ID: {info.Serial}";

            InfoSD.TxtFileSystem.Foreground = info.Formato == "FAT32"
                ? (SolidColorBrush)FindResource("AcentoCian")
                : (SolidColorBrush)FindResource("AcentoRojo");

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
            if (string.Equals(mundo.Tipo, "asistido", StringComparison.OrdinalIgnoreCase))
            {
                PanelTituloSeccion.Visibility = Visibility.Collapsed;
                return;
            }

            PanelTituloSeccion.Visibility = Visibility.Visible;
            TxtTituloSeccion.Text         = mundo.Nombre ?? "CATÁLOGO";
            TxtSubtituloSeccion.Text      = !string.IsNullOrWhiteSpace(mundo.Subtitulo)
                ? mundo.Subtitulo
                : "Selecciona una categoría para continuar";
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

        #endregion

        #region Tarjetas

        private void Catalogo_HoverTarjeta(object sender, System.Windows.Input.MouseEventArgs e)
            => GestorSonidos.Instancia.Reproducir(EventoSonido.Hover);

        private void Catalogo_ClickTarjeta(object sender, MouseButtonEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is ModuloConfig modulo)
                AbrirDetalleModulo(modulo);
        }

        private async void Catalogo_ClickBoton(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not Button btn || btn.DataContext is not ModuloConfig modulo)
                return;

            GestorSonidos.Instancia.Reproducir(EventoSonido.Click);

            string? letraSD = (InfoSD.ComboDrives.SelectedItem as SDInfo)?.Letra;

            switch (modulo.AccionRapida)
            {
                case AccionRapidaModulo.Instalar:
                case AccionRapidaModulo.Actualizar:
                case AccionRapidaModulo.Reinstalar:
                    if (string.IsNullOrEmpty(letraSD))
                    {
                        MessageBox.Show("No hay ninguna SD seleccionada.", "Advertencia",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    await EjecutarInstalacionRapidaAsync(modulo, letraSD);
                    break;

                case AccionRapidaModulo.Eliminar:
                    if (string.IsNullOrEmpty(letraSD)) return;
                    await EjecutarEliminacionRapidaAsync(modulo, letraSD);
                    break;

                default:
                    ConfirmarLimpiezaCache(modulo);
                    break;
            }
        }

        private async Task EjecutarInstalacionRapidaAsync(ModuloConfig modulo, string letraSD)
        {
            const double VelocidadBase = 0.0018; // mínimo: siempre avanza aunque no haya noticias
            const double VelocidadMax  = 0.032;  // máximo: al recibir un salto grande de progreso

            double targetProgress = 0.0;
            double velocidad      = VelocidadBase;

            modulo.EstaInstalando      = true;
            modulo.ProgresoInstalacion = 0.0;

            GestorSonidos.Instancia.Reproducir(EventoSonido.Instalar);

            // Timer a ~60 fps con velocidad propia
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
            });

            try
            {
                var resultado = await _cerebro.InstalarModuloAsync(modulo, letraSD, progreso);

                // Llevar al 100% y esperar que el relleno llegue visualmente (máx 2s)
                targetProgress = 1.0;
                var limite = DateTime.Now.AddSeconds(2);
                while (modulo.ProgresoInstalacion < 0.995 && DateTime.Now < limite)
                    await Task.Delay(16);

                timer.Stop();
                modulo.ProgresoInstalacion = 1.0;
                await Task.Delay(300); // pausa breve al llegar al 100%

                modulo.EstaInstalando      = false;
                modulo.ProgresoInstalacion = 0.0;

                if (_catalogoModulos != null)
                    _cerebro.ActualizarEstadoCacheCatalogo(_catalogoModulos);

                await ActualizarListaUnidadesAsync();
                RefrescarVistaActual();

                if (!resultado.Exito)
                {
                    GestorSonidos.Instancia.Reproducir(EventoSonido.Error);
                    MessageBox.Show($"Error:\n{resultado.MensajeError}", "Fallo",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    GestorSonidos.Instancia.Reproducir(EventoSonido.Exito);
                }
            }
            catch (Exception ex)
            {
                timer.Stop();
                modulo.EstaInstalando      = false;
                modulo.ProgresoInstalacion = 0.0;
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task EjecutarEliminacionRapidaAsync(ModuloConfig modulo, string letraSD)
        {
            var resp = MessageBox.Show(
                $"¿Eliminar {modulo.Nombre} de la SD?",
                "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (resp != MessageBoxResult.Yes) return;

            try
            {
                bool exito = await _cerebro.DesinstalarModuloAsync(modulo, letraSD);
                await ActualizarListaUnidadesAsync();
                RefrescarVistaActual();

                if (!exito)
                    MessageBox.Show("Hubo un error al eliminar algunos archivos.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
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

            _moduloActual          = modulo;
            TxtTituloDetalle.Text  = modulo.Nombre ?? string.Empty;
            TxtDescDetalle.Text    = modulo.Descripcion ?? string.Empty;
            TxtVersionDetalle.Text = modulo.Versiones?.Count > 0
                ? $"Versión: {modulo.Versiones[0].Version}"
                : "Versión: --";

            if (!string.IsNullOrEmpty(modulo.IconoUrl))
            {
                try
                {
                    string? rutaLocal = Core.GestorIconos.Instancia?.ObtenerRutaLocal(modulo.IconoUrl);
                    string uriStr     = rutaLocal ?? modulo.IconoUrl;
                    ImgDetalle.Source = new BitmapImage(new Uri(uriStr));

                    if (rutaLocal == null)
                        _ = Core.GestorIconos.Instancia?.DescargarSiNoExisteAsync(modulo.IconoUrl);
                }
                catch { ImgDetalle.Source = null; }
            }
            else ImgDetalle.Source = null;

            VistaCatalogo.Visibility = Visibility.Collapsed;
            VistaAsistida.Visibility = Visibility.Collapsed;
            UiAnimaciones.MostrarDetalle(VistaDetalle);
            BtnCerrarPaneles_Click(null, null);
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

            try
            {
                _pantallaCarga.Mostrar($"Instalando {_moduloActual.Nombre}");
                var resultado = await _cerebro.InstalarModuloAsync(_moduloActual, letraSD, _pantallaCarga.ObtenerReportador());

                if (resultado.Exito)
                {
                    await Task.Delay(1000);
                    _pantallaCarga.Ocultar();

                    if (_catalogoModulos != null)
                        _cerebro.ActualizarEstadoCacheCatalogo(_catalogoModulos);

                    await ActualizarListaUnidadesAsync();
                    RefrescarVistaActual();

                    MessageBox.Show($"¡{_moduloActual.Nombre} se ha instalado correctamente!", "Éxito",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    _pantallaCarga.Ocultar();
                    MessageBox.Show($"Error durante la instalación:\n\n{resultado.MensajeError}", "Fallo",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _pantallaCarga.Ocultar();
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
                    MessageBox.Show($"¡{_moduloActual.Nombre} se ha eliminado!", "Éxito",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    RefrescarVistaActual();
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

        #endregion

        #region Controles de Ventana

        private void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private async void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            GestorSonidos.Instancia.Reproducir(EventoSonido.Cerrar);
            await Task.Delay(600); // esperar a que el sonido termine antes de cerrar
            Application.Current.Shutdown();
        }

        #endregion
    }
}