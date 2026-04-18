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

            UIConfigService.Current = _datosGist.ConfiguracionUI ?? new ConfiguracionUI();

            _mundosMenu = _datosGist.MundosMenu ?? new List<MundoMenuConfig>();
            _filtrosCentroMando = _datosGist.FiltrosCentroMando ?? new List<FiltroMandoConfig>();

            _catalogoModulos = new ObservableCollection<ModuloConfig>(_datosGist.Modulos ?? new List<ModuloConfig>());
            CatalogoModulos.ItemsSource = _catalogoModulos;

            MenuMundos.ListaMundos.ItemsSource = _mundosMenu;

            TxtTituloSeccion.Text = "Firmware";
            TxtSubtituloSeccion.Text = "Selecciona un firmware para continuar";
            AplicarFiltrosFirmware();

            if (_mundosMenu.Count > 0)
            {
                MenuMundos.ListaMundos.SelectedIndex = -1;
                _mundoSeleccionado = null;
                ActualizarFiltrosDelMundo(string.Empty);
            }

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

                InfoSD.ComboDrives.ItemsSource = unidades;
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
                MessageBox.Show(
                    $"Error detectando la SD: {ex.Message}",
                    "Diagnóstico",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
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

                if (_datosGist == null)
                    return;

                _catalogoModulos = new ObservableCollection<ModuloConfig>(_datosGist.Modulos ?? new List<ModuloConfig>());
                CatalogoModulos.ItemsSource = _catalogoModulos;

                if (_mundoSeleccionado != null)
                    ActualizarFiltrosDelMundo(_mundoSeleccionado.Id);

                if (EsDiagrama(_mundoSeleccionado))
                    AplicarFiltrosFirmware();
                else
                    AplicarFiltrosCatalogo();
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

            FiltrosRetractil.ContenedorMando.IsHitTestVisible    = false;
            ArsenalRetractil.ContenedorArsenal.IsHitTestVisible  = false;
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

        #region Catálogo y Firmware

        private static bool EsDiagrama(MundoMenuConfig? mundo)
            => string.Equals(mundo?.Tipo, "diagrama", StringComparison.OrdinalIgnoreCase);

        private void ListaMundos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_cargandoCatalogoInicial)
                return;

            if (MenuMundos.ListaMundos.SelectedItem is not MundoMenuConfig mundo)
                return;

            _mundoSeleccionado = mundo;
            _filtroSeleccionado = null;

            if (EsDiagrama(mundo))
            {
                TxtTituloSeccion.Text    = "Firmware";
                TxtSubtituloSeccion.Text = "Selecciona un firmware para continuar";
                ActualizarFiltrosDelMundo(string.Empty);
                AplicarFiltrosFirmware();
                return;
            }

            TxtTituloSeccion.Text    = mundo.Nombre ?? "CATÁLOGO";
            TxtSubtituloSeccion.Text = "Selecciona una categoría para continuar";
            MostrarVistaCatalogo();
            ActualizarFiltrosDelMundo(mundo.Id);
            AplicarFiltrosCatalogo();
        }

        private void ListaCategorias_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FiltrosRetractil.ListaCategorias.SelectedItem is not FiltroMandoConfig filtro)
                return;

            _filtroSeleccionado = filtro;
            AplicarFiltrosCatalogo();
        }

        private void ActualizarFiltrosDelMundo(string mundoId)
        {
            if (_filtrosCentroMando == null)
                return;

            var filtros = _filtrosCentroMando
                .Where(f => f.Mundos == null || f.Mundos.Count == 0 ||
                            f.Mundos.Any(m => string.Equals(m, mundoId, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            FiltrosRetractil.ListaCategorias.ItemsSource  = filtros;
            FiltrosRetractil.ListaCategorias.SelectedIndex = -1;
            _filtroSeleccionado = null;
        }

        private void AplicarFiltrosFirmware()
        {
            if (_datosGist == null)
                return;

            var modulos = _cerebro.FiltrarFirmware(_datosGist.Modulos ?? Enumerable.Empty<ModuloConfig>());
            CatalogoModulos.ItemsSource = new ObservableCollection<ModuloConfig>(modulos.ToList());
        }

        private void AplicarFiltrosCatalogo()
        {
            if (_datosGist == null)
                return;

            IEnumerable<ModuloConfig> modulos = _datosGist.Modulos ?? Enumerable.Empty<ModuloConfig>();

            if (_mundoSeleccionado != null && !string.IsNullOrWhiteSpace(_mundoSeleccionado.Id))
                modulos = _cerebro.FiltrarPorMundo(modulos, _mundoSeleccionado.Id);

            if (_filtroSeleccionado != null &&
                !string.IsNullOrWhiteSpace(_filtroSeleccionado.Tag) &&
                !string.Equals(_filtroSeleccionado.Tag, "all", StringComparison.OrdinalIgnoreCase))
            {
                modulos = _cerebro.FiltrarPorEtiqueta(modulos, _filtroSeleccionado.Tag);
            }

            CatalogoModulos.ItemsSource = new ObservableCollection<ModuloConfig>(modulos.ToList());
        }

        private void MostrarVistaCatalogo()
        {
            VistaCatalogo.Visibility = Visibility.Visible;
            VistaDetalle.Visibility  = Visibility.Collapsed;
        }

        private void MostrarVistaDetalle()
        {
            VistaCatalogo.Visibility = Visibility.Collapsed;
            VistaDetalle.Visibility  = Visibility.Visible;
        }

        #endregion

        #region Tarjetas

        private void Catalogo_ClickTarjeta(object sender, MouseButtonEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is ModuloConfig modulo)
                AbrirDetalleModulo(modulo);
        }

        private void Catalogo_ClickBoton(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not Button btn || btn.DataContext is not ModuloConfig modulo)
                return;

            var respuesta = MessageBox.Show(
                $"¿Deseas eliminar {modulo.Nombre} de la memoria caché de tu PC?\nDeberás descargarlo de nuevo para instalarlo.",
                "Limpiar Caché Local", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (respuesta != MessageBoxResult.Yes)
                return;

            try
            {
                _cerebro.LimpiarCacheModulo(modulo);

                if (_catalogoModulos != null)
                    _cerebro.ActualizarEstadoCacheCatalogo(_catalogoModulos);

                MessageBox.Show("Caché eliminada correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

                if (EsDiagrama(_mundoSeleccionado))
                    AplicarFiltrosFirmware();
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
            if (modulo == null)
                return;

            _moduloActual = modulo;
            TxtTituloDetalle.Text = modulo.Nombre ?? string.Empty;
            TxtDescDetalle.Text   = modulo.Descripcion ?? string.Empty;
            TxtVersionDetalle.Text = modulo.Versiones?.Count > 0
                ? $"Versión: {modulo.Versiones[0].Version}"
                : "Versión: --";

            if (!string.IsNullOrEmpty(modulo.IconoUrl))
            {
                try { ImgDetalle.Source = new BitmapImage(new Uri(modulo.IconoUrl)); }
                catch { ImgDetalle.Source = null; }
            }
            else
            {
                ImgDetalle.Source = null;
            }

            MostrarVistaDetalle();
            BtnCerrarPaneles_Click(null, null);
        }

        private void BtnVolver_Click(object sender, RoutedEventArgs e)
        {
            _moduloActual = null;
            MostrarVistaCatalogo();
        }

        private async void BtnInstalar_Click(object sender, RoutedEventArgs e)
        {
            if (_moduloActual == null)
                return;

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
                var reportador = _pantallaCarga.ObtenerReportador();
                var resultado  = await _cerebro.InstalarModuloAsync(_moduloActual, letraSD, reportador);

                if (resultado.Exito)
                {
                    await Task.Delay(1000);
                    _pantallaCarga.Ocultar();

                    if (_catalogoModulos != null)
                        _cerebro.ActualizarEstadoCacheCatalogo(_catalogoModulos);

                    await ActualizarListaUnidadesAsync();

                    if (EsDiagrama(_mundoSeleccionado))
                        AplicarFiltrosFirmware();

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
            if (_moduloActual == null)
                return;

            var respuesta = MessageBox.Show(
                $"¿Estás seguro de que deseas eliminar {_moduloActual.Nombre} de la SD?",
                "Confirmar Eliminación", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (respuesta != MessageBoxResult.Yes)
                return;

            string? letraSD = (InfoSD.ComboDrives.SelectedItem as SDInfo)?.Letra;
            if (string.IsNullOrEmpty(letraSD))
                return;

            try
            {
                bool exito = await _cerebro.DesinstalarModuloAsync(_moduloActual, letraSD);

                if (exito)
                {
                    MessageBox.Show($"¡{_moduloActual.Nombre} se ha eliminado!", "Éxito",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    if (EsDiagrama(_mundoSeleccionado))
                        AplicarFiltrosFirmware();
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

        #region Controles de Ventana

        private void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

        #endregion
    }
}