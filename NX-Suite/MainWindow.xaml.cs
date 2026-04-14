using NX_Suite.Core;
using NX_Suite.Hardware;
using NX_Suite.Models;
using NX_Suite.UI.Controles;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace NX_Suite{
    public partial class MainWindow : Window
    {
        public static ConfiguracionUI UIGlobal { get; set; }
        
        private readonly ISuiteController _cerebro;
        private readonly DiskMaster _diskMaster = new DiskMaster();
        private readonly ControladorCarga _pantallaCarga;
        
        private ModuloConfig _moduloActual;
        private bool _panelIzquierdoAbierto = false;
        private bool _panelDerechoAbierto = false;
        private GistData _datosGist;
        private ObservableCollection<ModuloConfig> _catalogoModulos;

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
            _diskMaster.UnidadConectada += (s, e) => Dispatcher.InvokeAsync(async () => await ActualizarListaUnidadesAsync());
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
            string letraSD = (InfoSD.ComboDrives.SelectedItem as SDInfo)?.Letra;
            _datosGist = await _cerebro.SincronizarTodoAsync(ConfiguracionPro.UrlGistPrincipal, letraSD);

            if (_datosGist == null)
                return;

            UIGlobal = _datosGist.ConfiguracionUI ?? new ConfiguracionUI();

            _catalogoModulos = new ObservableCollection<ModuloConfig>(_datosGist.Modulos ?? new List<ModuloConfig>());
            CatalogoModulos.ItemsSource = _catalogoModulos;
            MenuMundos.ListaMundos.ItemsSource = _datosGist.MundosMenu ?? new List<MundoMenuConfig>();
            FiltrosRetractil.ListaCategorias.ItemsSource = _datosGist.FiltrosCentroMando ?? new List<FiltroMandoConfig>();

            await ActualizarListaUnidadesAsync();
        }

        private async Task ActualizarListaUnidadesAsync()
        {
            try
            {
                string letraPrevia = (InfoSD.ComboDrives.SelectedItem as SDInfo)?.Letra;
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
            InfoSD.TxtTotalSize.Text = "0 GB";
            InfoSD.TxtFileSystem.Text = "--";
            InfoSD.TxtSDSerial.Text = "ID: NO DETECTADA";
            InfoSD.TxtAtmosVer.Text = "N/A";    
        }

        private void ComboDrives_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (InfoSD.ComboDrives.SelectedItem is not SDInfo unidad)
                return;

            if (_catalogoModulos == null || _datosGist == null)
                return;

            var info = _cerebro.ObtenerInfoPanel(unidad, _catalogoModulos.ToList());

            InfoSD.TxtTotalSize.Text = info.Capacidad;
            InfoSD.TxtFileSystem.Text = info.Formato;
            InfoSD.TxtAtmosVer.Text = info.VersionAtmos;
            InfoSD.TxtSDSerial.Text = $"ID: {info.Serial}";

            InfoSD.TxtFileSystem.Foreground = info.Formato == "FAT32"
                ? (System.Windows.Media.SolidColorBrush)FindResource("AcentoCian")
                : (System.Windows.Media.SolidColorBrush)FindResource("AcentoRojo");
        }

       
       

        #region Gestión de Paneles Laterales
        
        private void CambiarColorRiel(System.Windows.Controls.Border riel, bool aplicar, string colorHex)
        {
            if (aplicar)
                riel.Background = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorHex));
        }

        private void BtnCerrarPaneles_Click(object sender, RoutedEventArgs e)
        {
            UiAnimaciones.CerrarPaneles(
                FiltrosRetractil.RielMando, FiltrosRetractil.ContenedorMando,
                ArsenalRetractil.RielGris, ArsenalRetractil.ContenedorArsenal, FondoOscuro);
            
            _panelIzquierdoAbierto = false;
            _panelDerechoAbierto = false;

            if (FiltrosRetractil.Pestanita != null) FiltrosRetractil.Pestanita.Visibility = Visibility.Visible;
            if (ArsenalRetractil.Pestanita != null) ArsenalRetractil.Pestanita.Visibility = Visibility.Visible;

            FiltrosRetractil.ContenedorMando.IsHitTestVisible = false;
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

        #endregion

        #region Filtrado de Catálogo

        private void ListaMundos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MenuMundos.ListaMundos.SelectedItem is MundoMenuConfig mundo)
            {
                var modulosFiltrados = _cerebro.FiltrarPorMundo(_datosGist.Modulos, mundo.Id);
                CatalogoModulos.ItemsSource = new ObservableCollection<ModuloConfig>(modulosFiltrados);
            }
        }

        private void ListaCategorias_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FiltrosRetractil.ListaCategorias.SelectedItem is FiltroMandoConfig filtro)
            {
                var modulosFiltrados = _cerebro.FiltrarPorEtiqueta(_datosGist.Modulos, filtro.Tag);
                CatalogoModulos.ItemsSource = new ObservableCollection<ModuloConfig>(modulosFiltrados);
                BtnCerrarPaneles_Click(null, null);
            }
        }

        #endregion

        #region Gestión de Tarjetas del Catálogo

        private void Catalogo_ClickTarjeta(object sender, MouseButtonEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is ModuloConfig modulo)
            {
                AbrirDetalleModulo(modulo);
            }
        }

        private void Catalogo_ClickBoton(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is Button btn && btn.DataContext is ModuloConfig modulo)
            {
                var respuesta = MessageBox.Show(
                    $"¿Deseas eliminar {modulo.Nombre} de la memoria caché de tu PC?\nDeberás descargarlo de nuevo para instalarlo.",
                    "Limpiar Caché Local", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (respuesta == MessageBoxResult.Yes)
                {
                    try
                    {
                        _cerebro.LimpiarCacheModulo(modulo);
                        _cerebro.ActualizarEstadoCacheCatalogo(_catalogoModulos);
                        MessageBox.Show("Caché eliminada correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
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
            TxtDescDetalle.Text = modulo.Descripcion ?? string.Empty;

            if (modulo.Versiones != null && modulo.Versiones.Count > 0)
                TxtVersionDetalle.Text = $"Versión: {modulo.Versiones[0].Version}";
            else
                TxtVersionDetalle.Text = "Versión: --";

            if (!string.IsNullOrEmpty(modulo.IconoUrl))
                ImgDetalle.Source = new BitmapImage(new Uri(modulo.IconoUrl));

            VistaCatalogo.Visibility = Visibility.Collapsed;
            VistaDetalle.Visibility = Visibility.Visible;
            BtnCerrarPaneles_Click(null, null);
        }

        private void BtnVolver_Click(object sender, RoutedEventArgs e)
        {
            VistaDetalle.Visibility = Visibility.Collapsed;
            VistaCatalogo.Visibility = Visibility.Visible;
            _moduloActual = null;
        }

        public async void BtnInstalar_Click(object sender, RoutedEventArgs e)
        {
            if (_moduloActual == null) return;

            string letraSD = (InfoSD.ComboDrives.SelectedItem as SDInfo)?.Letra;
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

                var resultado = await _cerebro.InstalarModuloAsync(_moduloActual, letraSD, reportador);

                if (resultado.Exito)
                {
                    await Task.Delay(1000);
                    _pantallaCarga.Ocultar();
                    _cerebro.ActualizarEstadoCacheCatalogo(_catalogoModulos);
                    await ActualizarListaUnidadesAsync();
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

            if (respuesta == MessageBoxResult.Yes)
            {
                string letraSD = (InfoSD.ComboDrives.SelectedItem as SDInfo)?.Letra;
                if (string.IsNullOrEmpty(letraSD)) return;

                bool exito = await _cerebro.DesinstalarModuloAsync(_moduloActual, letraSD);
                
                if (exito)
                    MessageBox.Show($"¡{_moduloActual.Nombre} se ha eliminado!", "Éxito", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                else
                    MessageBox.Show("Hubo un error al intentar borrar algunos archivos.", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        #endregion

        #region Controles de Ventana

        private void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

        #endregion
    }
}