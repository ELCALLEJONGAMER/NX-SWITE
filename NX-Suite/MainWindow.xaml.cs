using NX_Suite.Core;
using NX_Suite.Hardware;
using NX_Suite.Models;
using NX_Suite.UI.Controles;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
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
        public static ConfiguracionUI UIGlobal { get; set; }
        private readonly ISuiteController _cerebro;
        private readonly NX_Suite.Core.GestorCache _gestorCache = new NX_Suite.Core.GestorCache();
        

        private readonly NX_Suite.Hardware.DiskMaster _diskMaster = new NX_Suite.Hardware.DiskMaster();
        private readonly SDMonitorLogic _monitorLogic = new SDMonitorLogic();

        private ControladorCarga _pantallaCarga;
        private ModuloConfig _moduloActual;
        private bool _panelIzquierdoAbierto = false;
        private bool _panelDerechoAbierto = false;
        private GistData _datosGist;

        public MainWindow()
        {
            InitializeComponent();
            _cerebro = new SuiteControllerFacade(new SuiteController(_gestorCache));

            _pantallaCarga = new ControladorCarga(OverlayCarga, TxtCargaSubtitulo, TxtCargaDetalle, TxtCargaPorcentaje,
                                                  BarraProgresoNeon, TxtPaso1, TxtPaso2, TxtPaso3, TxtPaso4);

            _diskMaster.IniciarEscucha(this);
            _diskMaster.UnidadConectada += (s, e) => Dispatcher.Invoke(() => ListarUnidadesSD());
            ListarUnidadesSD();

            // CONEXIÓN PANELES IZQUIERDOS
            MenuMundos.ListaMundos.SelectionChanged += ListaMundos_SelectionChanged;
            FiltrosRetractil.ListaCategorias.SelectionChanged += ListaCategorias_SelectionChanged;
            FiltrosRetractil.RielMando.MouseLeftButtonDown += RielMando_Click;
            FiltrosRetractil.RielMando.MouseEnter += RielMando_Hover;
            FiltrosRetractil.RielMando.MouseLeave += RielMando_Leave;

            // CONEXIÓN PANELES DERECHOS
            InfoSD.ComboDrives.SelectionChanged += ComboDrives_SelectionChanged;
            ArsenalRetractil.RielGris.MouseLeftButtonDown += RielGris_Click;
            ArsenalRetractil.RielGris.MouseEnter += RielGris_Hover;
            ArsenalRetractil.RielGris.MouseLeave += RielGris_Leave;

            this.Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            string letraSD = (InfoSD.ComboDrives.SelectedItem as NX_Suite.Hardware.SDInfo)?.Letra;
            var todoElGist = await _cerebro.SincronizarTodoAsync(ConfiguracionPro.UrlGistPrincipal, letraSD);

            if (todoElGist != null)
            {
                _datosGist = todoElGist;
                MainWindow.UIGlobal = todoElGist.ConfiguracionUI;

                CatalogoModulos.ItemsSource = new ObservableCollection<ModuloConfig>(todoElGist.Modulos);
                MenuMundos.ListaMundos.ItemsSource = todoElGist.MundosMenu;
                FiltrosRetractil.ListaCategorias.ItemsSource = todoElGist.FiltrosCentroMando;
            }
        }

        private void ListarUnidadesSD()
        {
            try
            {
                string letraPrevia = (InfoSD.ComboDrives.SelectedItem as NX_Suite.Hardware.SDInfo)?.Letra;
                var unidades = _diskMaster.ObtenerUnidadesRemovibles();

                InfoSD.ComboDrives.ItemsSource = unidades;
                InfoSD.ComboDrives.DisplayMemberPath = "FullName";

                if (InfoSD.ComboDrives.Items.Count > 0)
                {
                    bool encontrada = false;
                    foreach (NX_Suite.Hardware.SDInfo item in InfoSD.ComboDrives.Items)
                    {
                        if (item.Letra == letraPrevia) { InfoSD.ComboDrives.SelectedItem = item; encontrada = true; break; }
                    }
                    if (!encontrada) InfoSD.ComboDrives.SelectedIndex = 0;
                }
                else LimpiarInterfazSD();
            }
            catch (Exception ex) { MessageBox.Show($"Error detectando la SD: {ex.Message}", "Diagnóstico", MessageBoxButton.OK, MessageBoxImage.Warning); }
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
            if (InfoSD.ComboDrives.SelectedItem is NX_Suite.Hardware.SDInfo unidad) ActualizarInformacionSD(unidad.Letra);
        }

        private void ActualizarInformacionSD(string letraSD)
        {
            var unidad = InfoSD.ComboDrives.SelectedItem as NX_Suite.Hardware.SDInfo;
            var listaModulos = CatalogoModulos.ItemsSource as System.Collections.Generic.List<ModuloConfig>;
            var info = _cerebro.ObtenerInfoPanel(unidad, listaModulos);

            InfoSD.TxtTotalSize.Text = info.Capacidad;
            InfoSD.TxtFileSystem.Text = info.Formato;
            InfoSD.TxtAtmosVer.Text = info.VersionAtmos;
            InfoSD.TxtSDSerial.Text = "ID: " + info.Serial;

            InfoSD.TxtFileSystem.Foreground = info.Formato == "FAT32"
                ? (SolidColorBrush)FindResource("AcentoCian")
                : (SolidColorBrush)FindResource("AcentoRojo");
        }

        private void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

        private void BtnCerrarPaneles_Click(object sender, RoutedEventArgs e)
        {
            UiAnimaciones.CerrarPaneles(FiltrosRetractil.RielMando, FiltrosRetractil.ContenedorMando, ArsenalRetractil.RielGris, ArsenalRetractil.ContenedorArsenal, FondoOscuro);
            _panelIzquierdoAbierto = false;
            _panelDerechoAbierto = false;

            if (FiltrosRetractil.Pestanita != null) FiltrosRetractil.Pestanita.Visibility = Visibility.Visible;
            if (ArsenalRetractil.Pestanita != null) ArsenalRetractil.Pestanita.Visibility = Visibility.Visible;

            FiltrosRetractil.ContenedorMando.IsHitTestVisible = false;
            ArsenalRetractil.ContenedorArsenal.IsHitTestVisible = false;
        }

        private void ListaMundos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MenuMundos.ListaMundos.SelectedItem is MundoMenuConfig mundoSeleccionado)
            {
                var vista = System.Windows.Data.CollectionViewSource.GetDefaultView(CatalogoModulos.ItemsSource);
                if (vista != null)
                {
                    vista.Filter = (obj) =>
                    {
                        var mod = obj as ModuloConfig;
                        return mod != null && !string.IsNullOrEmpty(mod.Mundo) && mod.Mundo.Equals(mundoSeleccionado.Id, StringComparison.OrdinalIgnoreCase);
                    };
                }
            }
        }

        private void ListaCategorias_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FiltrosRetractil.ListaCategorias.SelectedItem is FiltroMandoConfig seleccionada)
            {
                var vista = System.Windows.Data.CollectionViewSource.GetDefaultView(CatalogoModulos.ItemsSource);
                if (vista != null)
                {
                    vista.Filter = (obj) =>
                    {
                        var mod = obj as ModuloConfig;
                        return seleccionada.Nombre == "Todos" || (mod.Etiquetas != null && mod.Etiquetas.Contains(seleccionada.Tag));
                    };
                }
                BtnCerrarPaneles_Click(null, null);
            }
        }

        // --- ANIMACIONES PANEL IZQUIERDO ---
        private void RielMando_Hover(object sender, MouseEventArgs e) { if (!_panelIzquierdoAbierto) FiltrosRetractil.RielMando.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3E3E4F")); }
        private void RielMando_Leave(object sender, MouseEventArgs e) { if (!_panelIzquierdoAbierto) FiltrosRetractil.RielMando.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A2A35")); }
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

        // --- ANIMACIONES PANEL DERECHO ---
        private void RielGris_Hover(object sender, MouseEventArgs e) { if (!_panelDerechoAbierto) ArsenalRetractil.RielGris.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3E3E4F")); }
        private void RielGris_Leave(object sender, MouseEventArgs e) { if (!_panelDerechoAbierto) ArsenalRetractil.RielGris.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A2A35")); }
        private void RielGris_Click(object sender, MouseButtonEventArgs e)
        {
            if (!_panelDerechoAbierto)
            {
                BtnCerrarPaneles_Click(null, null); // Cerramos otros por seguridad
                UiAnimaciones.AbrirPanelDerecho(ArsenalRetractil.RielGris, ArsenalRetractil.ContenedorArsenal, FondoOscuro);
                _panelDerechoAbierto = true;

                // Ocultamos los puntos y activamos los clics internos
                if (ArsenalRetractil.Pestanita != null) ArsenalRetractil.Pestanita.Visibility = Visibility.Collapsed;
                ArsenalRetractil.ContenedorArsenal.IsHitTestVisible = true;
            }
            else
            {
                // Si ya estaba abierto, el clic lo cierra
                BtnCerrarPaneles_Click(null, null);
            }
        }

        public void RefrescarEstadoCacheModulos()
        {
            if (CatalogoModulos.ItemsSource is IEnumerable<ModuloConfig> modulos)
            {
                _gestorCache.ActualizarEstadoCache(modulos);
                CatalogoModulos.Items.Refresh();
            }
        }

        private void Catalogo_ClickTarjeta(object sender, MouseButtonEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is ModuloConfig modulo) AbrirDetalleModulo(modulo);
        }

        private void Catalogo_ClickBoton(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is Button btn && btn.DataContext is ModuloConfig modulo)
            {
                MessageBoxResult respuesta = MessageBox.Show($"¿Deseas eliminar {modulo.Nombre} de la memoria caché de tu PC?\nDeberás descargarlo de nuevo para instalarlo.", "Limpiar Caché Local", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (respuesta == MessageBoxResult.Yes)
                {
                    bool borradoExitoso = _gestorCache.BorrarCacheModulo(modulo);
                    if (borradoExitoso) RefrescarEstadoCacheModulos();
                    else MessageBox.Show("No se pudieron borrar todos los archivos. Puede que estén en uso.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void AbrirDetalleModulo(ModuloConfig modulo)
        {
            _moduloActual = modulo;
            TxtTituloDetalle.Text = modulo.Nombre;
            TxtDescDetalle.Text = modulo.Descripcion;
            TxtVersionDetalle.Text = $"Versión: {modulo.Versiones[0].Version}";

            if (!string.IsNullOrEmpty(modulo.IconoUrl)) ImgDetalle.Source = new BitmapImage(new Uri(modulo.IconoUrl));

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
            try
            {
                _pantallaCarga.Mostrar($"Instalando {_moduloActual.Nombre}");
                var reportador = _pantallaCarga.ObtenerReportador();
                string letraSD = (InfoSD.ComboDrives.SelectedItem as NX_Suite.Hardware.SDInfo)?.Letra;

                if (string.IsNullOrEmpty(letraSD))
                {
                    _pantallaCarga.Ocultar();
                    MessageBox.Show("No hay ninguna SD seleccionada para instalar.", "Advertencia", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var resultado = await _cerebro.InstalarModuloAsync(_moduloActual, letraSD, reportador);

                if (resultado.Exito)
                {
                    await Task.Delay(1000);
                    _pantallaCarga.Ocultar();
                    _gestorCache.ActualizarEstadoCache((IEnumerable<ModuloConfig>)CatalogoModulos.ItemsSource);
                    CatalogoModulos.Items.Refresh();
                    ListarUnidadesSD();
                    MessageBox.Show($"¡{_moduloActual.Nombre} se ha instalado correctamente!", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    _pantallaCarga.Ocultar();
                    MessageBox.Show($"Error durante la instalación:\n\n{resultado.MensajeError}", "Fallo", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _pantallaCarga.Ocultar();
                MessageBox.Show($"Excepción en la interfaz: {ex.Message}", "Error Crítico", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnBorrar_Click(object sender, RoutedEventArgs e)
        {
            if (_moduloActual == null) return;
            MessageBoxResult respuesta = MessageBox.Show($"¿Estás seguro de que deseas eliminar {_moduloActual.Nombre} de la SD?", "Confirmar Eliminación", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (respuesta == MessageBoxResult.Yes)
            {
                string letraSD = (InfoSD.ComboDrives.SelectedItem as NX_Suite.Hardware.SDInfo)?.Letra;
                if (string.IsNullOrEmpty(letraSD)) return;

                bool exito = await _cerebro.DesinstalarModuloAsync(_moduloActual, letraSD);
                if (exito) MessageBox.Show($"¡{_moduloActual.Nombre} se ha eliminado!", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                else MessageBox.Show("Hubo un error al intentar borrar algunos archivos.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}