using NX_Suite.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace NX_Suite.UI.Controles
{
    // ??????????????????????????????????????????????????????????????
    //  Sesión de instalación asistida
    // ??????????????????????????????????????????????????????????????

    public class SesionAsistida
    {
        public List<ModuloConfig> Modulos { get; init; } = new();
    }

    // ??????????????????????????????????????????????????????????????
    //  Item del checkout (módulo seleccionado por el usuario)
    // ??????????????????????????????????????????????????????????????

    public class ItemCheckoutVM
    {
        public ModuloConfig Modulo     { get; init; } = null!;
        public string       PasoTitulo { get; init; } = string.Empty;
        public string       ColorNeon  { get; init; } = "#00D2FF";

        public string Nombre   => Modulo.Nombre;
        public string Version  => Modulo.Versiones?.Count > 0 ? $"v{Modulo.Versiones[0].Version}" : string.Empty;
        public string IconoUrl => Modulo.IconoUrl;
    }

    // ??????????????????????????????????????????????????????????????
    //  SubcategoriaVM — usado en el paso Extras
    // ??????????????????????????????????????????????????????????????

    public class SubcategoriaVM : INotifyPropertyChanged
    {
        public string Etiqueta            { get; }
        public string Nombre              { get; }
        public bool   PermiteMultiseleccion { get; init; } = true;

        public ObservableCollection<ModuloConfig> Seleccionados { get; } = new();

        public IEnumerable<object> SlotsVisibles
        {
            get
            {
                foreach (var m in Seleccionados) yield return m;
                if (PermiteMultiseleccion || Seleccionados.Count == 0)
                    yield return new SlotVacioPlaceholder(this);
            }
        }

        public SubcategoriaVM(string etiqueta, bool permiteMultiseleccion = true)
            : this(etiqueta, etiqueta, permiteMultiseleccion) { }

        public SubcategoriaVM(string etiqueta, string nombre, bool permiteMultiseleccion = true)
        {
            Etiqueta              = etiqueta ?? throw new ArgumentNullException(nameof(etiqueta));
            Nombre                = nombre ?? etiqueta;
            PermiteMultiseleccion = permiteMultiseleccion;
            Seleccionados.CollectionChanged += (_, _) => OnPropertyChanged(nameof(SlotsVisibles));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class SlotVacioPlaceholder
    {
        public SubcategoriaVM Subcategoria { get; }
        public SlotVacioPlaceholder(SubcategoriaVM sub) { Subcategoria = sub; }
    }

    public class SlotTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? ModuloTemplate { get; set; }
        public DataTemplate? VacioTemplate  { get; set; }
        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
            => item is SlotVacioPlaceholder ? VacioTemplate : ModuloTemplate;
    }

    // ??????????????????????????????????????????????????????????????
    //  UserControl principal
    // ??????????????????????????????????????????????????????????????

    public partial class VistaAsistida : UserControl
    {
        // ?? Definición estática de los 4 pasos ??????????????????

        private record PasoAsistente(
            int    Indice,
            string Titulo,
            string Descripcion,
            string ColorNeon,
            bool   EsObligatorio,
            bool   PermiteMultiple,
            IReadOnlyList<string> Etiquetas);

        private static readonly IReadOnlyList<PasoAsistente> _pasos = new PasoAsistente[]
        {
            new(0, "Bootloader", "Controla el arranque de tu consola. Necesario para ejecutar el Custom Firmware.",         "#A855F7", true,  false, new[]{"bootloader","hekate"}),
            new(1, "CFW",        "Custom Firmware. El núcleo del hackeo. Sin esto, nada de lo demás funciona.",             "#00D2FF", true,  false, new[]{"cfw","atmosphere"}),
            new(2, "Firmware",   "Sistema operativo oficial de la consola. Puedes saltarte este paso si no quieres actualizarlo.", "#FFD700", false, false, new[]{"firmware"}),
            new(3, "Extras",     "Temas, homebrew, emuladores, sigpatches y más complementos opcionales.",                  "#22C55E", false, true,  new[]{"sigpatches","homebrew","theme","emulador","app","cheats"}),
        };

        private static readonly Dictionary<string, string> _nombresExtras = new(StringComparer.OrdinalIgnoreCase)
        {
            { "sigpatches", "Sigpatches"   },
            { "homebrew",   "Homebrew"     },
            { "theme",      "Temas"        },
            { "emulador",   "Emuladores"   },
            { "app",        "Aplicaciones" },
            { "cheats",     "Trucos"       },
        };

        // ?? Estado ???????????????????????????????????????????????

        private int _pasoActual = 0;
        private readonly Dictionary<int, ModuloConfig?> _selecciones = new();
        private List<SubcategoriaVM>  _subcategoriasExtras      = new();
        private SubcategoriaVM?       _subcategoriaEnEdicion;
        private List<ModuloConfig>    _todosModulos             = new();
        private List<ModuloConfig>    _modulosFiltradosSelector = new();

        private readonly ObservableCollection<ItemCheckoutVM> _itemsCheckout = new();

        // Referencias nombradas al indicador de pasos
        private Border[]?    _circulosPaso;
        private TextBlock[]? _textosPaso;
        private Rectangle[]? _lineasPaso;

        public event EventHandler<SesionAsistida>? InstalacionSolicitada;

        public VistaAsistida()
        {
            InitializeComponent();

            _circulosPaso = new[] { CirPaso0, CirPaso1, CirPaso2, CirPaso3, CirPaso4 };
            _textosPaso   = new[] { TxtPaso0, TxtPaso1, TxtPaso2, TxtPaso3, TxtPaso4 };
            _lineasPaso   = new[] { LineaPaso0, LineaPaso1, LineaPaso2, LineaPaso3 };

            CheckoutLista.ItemsSource = _itemsCheckout;
        }

        // ????????????????????????????????????????????????????????
        //  API pública
        // ????????????????????????????????????????????????????????

        public void Cargar(List<NodoDiagramaConfig> nodos, List<ModuloConfig> modulos, string modoAsistente)
        {
            _todosModulos = modulos ?? new List<ModuloConfig>();
            _selecciones.Clear();
            _itemsCheckout.Clear();

            // Auto-detectar módulos ya instalados en la SD
            for (int i = 0; i < 3; i++)
            {
                var paso      = _pasos[i];
                var instalado = _todosModulos
                    .Where(m => paso.Etiquetas.Any(e => CoincideEtiqueta(m, e)))
                    .FirstOrDefault(m => m.EstadoSd == EstadoSdModulo.Instalado ||
                                        m.EstadoSd == EstadoSdModulo.ParcialmenteInstalado);
                if (instalado != null)
                    SeleccionarEnPaso(i, instalado, silencioso: true);
            }

            IniciarSubcategoriasExtras();
            IrAlPaso(0);
        }

        // ????????????????????????????????????????????????????????
        //  Navegación central
        // ????????????????????????????????????????????????????????

        private void IrAlPaso(int indice)
        {
            _pasoActual = indice;

            if (indice < _pasos.Count)
            {
                var paso = _pasos[indice];
                if (paso.PermiteMultiple)
                    MostrarExtras();
                else
                    MostrarPaso(paso);
            }
            else
            {
                MostrarResumen();
            }

            ActualizarIndicadorPasos();
        }

        private void MostrarVista(UIElement vistaActiva)
        {
            GridPaso.Visibility     = GridPaso     == vistaActiva ? Visibility.Visible : Visibility.Collapsed;
            GridSelector.Visibility = GridSelector == vistaActiva ? Visibility.Visible : Visibility.Collapsed;
            GridExtras.Visibility   = GridExtras   == vistaActiva ? Visibility.Visible : Visibility.Collapsed;
            GridResumen.Visibility  = GridResumen  == vistaActiva ? Visibility.Visible : Visibility.Collapsed;
        }

        // ????????????????????????????????????????????????????????
        //  Indicador de pasos
        // ????????????????????????????????????????????????????????

        private void ActualizarIndicadorPasos()
        {
            if (_circulosPaso == null) return;

            int total = _pasos.Count + 1; // 4 pasos + resumen

            for (int i = 0; i < total; i++)
            {
                var circulo = _circulosPaso[i];
                var texto   = _textosPaso![i];

                string hex   = i < _pasos.Count ? _pasos[i].ColorNeon : "#40C057";
                var color    = (Color)ColorConverter.ConvertFromString(hex);
                bool actual  = i == _pasoActual;
                bool pasado  = i < _pasoActual;

                var num = circulo.Child as TextBlock;

                if (pasado)
                {
                    circulo.Background  = new SolidColorBrush(color);
                    circulo.BorderBrush = new SolidColorBrush(color);
                    if (num != null) num.Text       = "?";
                    if (num != null) num.Foreground = Brushes.Black;
                    texto.Foreground = Brushes.White;
                    texto.Opacity    = 1;
                }
                else if (actual)
                {
                    circulo.Background  = new SolidColorBrush(Color.FromArgb(0x22, color.R, color.G, color.B));
                    circulo.BorderBrush = new SolidColorBrush(color);
                    if (num != null) num.Text       = (i + 1).ToString();
                    if (num != null) num.Foreground = new SolidColorBrush(color);
                    texto.Foreground = new SolidColorBrush(color);
                    texto.Opacity    = 1;
                }
                else
                {
                    circulo.Background  = Brushes.Transparent;
                    circulo.BorderBrush = new SolidColorBrush(Color.FromArgb(0x30, 0x70, 0x70, 0x80));
                    if (num != null) num.Text       = (i + 1).ToString();
                    if (num != null) num.Foreground = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
                    texto.Foreground = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
                    texto.Opacity    = 1;
                }

                // Línea conectora
                if (i < _lineasPaso!.Length)
                {
                    _lineasPaso[i].Fill = pasado
                        ? new SolidColorBrush(color)
                        : new SolidColorBrush(Color.FromArgb(0x20, 0x70, 0x70, 0x80));
                }
            }
        }

        // ????????????????????????????????????????????????????????
        //  VISTA PASO — pasos 0, 1, 2
        // ????????????????????????????????????????????????????????

        private void MostrarPaso(PasoAsistente paso)
        {
            TxtTituloPaso.Text       = paso.Titulo;
            TxtTituloPaso.Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom(paso.ColorNeon)!;
            TxtDescripcionPaso.Text  = paso.Descripcion;

            BadgeObligatorio.Visibility = paso.EsObligatorio ? Visibility.Visible  : Visibility.Collapsed;
            BadgeOpcional.Visibility    = paso.EsObligatorio ? Visibility.Collapsed : Visibility.Visible;

            ListaModulosPaso.ItemsSource = FiltrarPorEtiquetas(paso.Etiquetas);

            BtnVolverPaso.Visibility = _pasoActual > 0 ? Visibility.Visible : Visibility.Collapsed;
            BtnSaltarPaso.Visibility = !paso.EsObligatorio ? Visibility.Visible : Visibility.Collapsed;
            ActualizarBotonSiguientePaso();

            MostrarVista(GridPaso);
        }

        private void ActualizarBotonSiguientePaso()
        {
            bool tieneSeleccion = _selecciones.TryGetValue(_pasoActual, out var sel) && sel != null;
            bool obligatorio    = _pasoActual < _pasos.Count && _pasos[_pasoActual].EsObligatorio;
            BtnSiguientePaso.Visibility = (obligatorio ? tieneSeleccion : true) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void PasoModulo_Click(object sender, MouseButtonEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is not ModuloConfig modulo) return;
            e.Handled = true;
            SeleccionarEnPaso(_pasoActual, modulo);
            ActualizarBotonSiguientePaso();
        }

        private void SeleccionarEnPaso(int indicePaso, ModuloConfig modulo, bool silencioso = false)
        {
            var anterior = _selecciones.GetValueOrDefault(indicePaso);
            _selecciones[indicePaso] = modulo;

            // Reemplazar en checkout si ya había uno
            if (anterior != null)
            {
                var itemAnterior = _itemsCheckout.FirstOrDefault(x => x.Modulo == anterior);
                if (itemAnterior != null) _itemsCheckout.Remove(itemAnterior);
            }

            // Insertar en posición de orden correcto
            int insertAt = _itemsCheckout.Count(x =>
                _pasos.Any(p => p.Titulo == x.PasoTitulo && p.Indice < indicePaso));

            var nuevo = new ItemCheckoutVM
            {
                Modulo     = modulo,
                PasoTitulo = _pasos[indicePaso].Titulo,
                ColorNeon  = _pasos[indicePaso].ColorNeon,
            };

            if (insertAt >= _itemsCheckout.Count) _itemsCheckout.Add(nuevo);
            else _itemsCheckout.Insert(insertAt, nuevo);

            ActualizarCheckout();
        }

        private void BtnVolverPaso_Click(object sender, RoutedEventArgs e)    => IrAlPaso(_pasoActual - 1);
        private void BtnSaltarPaso_Click(object sender, RoutedEventArgs e)    => IrAlPaso(_pasoActual + 1);
        private void BtnSiguientePaso_Click(object sender, RoutedEventArgs e) => IrAlPaso(_pasoActual + 1);

        // ????????????????????????????????????????????????????????
        //  VISTA EXTRAS — paso 3
        // ????????????????????????????????????????????????????????

        private void IniciarSubcategoriasExtras()
        {
            _subcategoriasExtras = _pasos[3].Etiquetas
                .Select(e => new SubcategoriaVM(
                    e,
                    _nombresExtras.TryGetValue(e, out var n) ? n : e,
                    permiteMultiseleccion: true))
                .Where(s => FiltrarPorEtiqueta(s.Etiqueta).Any())
                .ToList();
        }

        private void MostrarExtras()
        {
            ListaExtras.ItemsSource = _subcategoriasExtras;
            MostrarVista(GridExtras);
        }

        private void ExtrasSlotClick(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not Button { Tag: SlotVacioPlaceholder placeholder }) return;
            e.Handled = true;
            AbrirSelector(placeholder.Subcategoria);
        }

        private void AbrirSelector(SubcategoriaVM subVM)
        {
            _subcategoriaEnEdicion             = subVM;
            TxtTituloSelector.Text             = subVM.Nombre;
            TxtSubtituloSelector.Text          = "Puedes seleccionar varios";
            TxtBuscador.Text                   = string.Empty;
            _modulosFiltradosSelector          = FiltrarPorEtiqueta(subVM.Etiqueta);
            ListaModulosSelector.ItemsSource   = _modulosFiltradosSelector;
            MostrarVista(GridSelector);
        }

        private void BtnVolverExtras_Click(object sender, RoutedEventArgs e)    => IrAlPaso(_pasoActual - 1);
        private void BtnSiguienteExtras_Click(object sender, RoutedEventArgs e) => IrAlPaso(_pasoActual + 1);

        // ????????????????????????????????????????????????????????
        //  VISTA SELECTOR — compartida
        // ????????????????????????????????????????????????????????

        private void SelectorModulo_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if ((e.OriginalSource as FrameworkElement)?.DataContext is not ModuloConfig modulo) return;
            if (_subcategoriaEnEdicion == null) return;

            if (!_subcategoriaEnEdicion.Seleccionados.Contains(modulo))
            {
                _subcategoriaEnEdicion.Seleccionados.Add(modulo);

                if (!_itemsCheckout.Any(x => x.Modulo == modulo))
                {
                    _itemsCheckout.Add(new ItemCheckoutVM
                    {
                        Modulo     = modulo,
                        PasoTitulo = _subcategoriaEnEdicion.Nombre,
                        ColorNeon  = _pasos[3].ColorNeon,
                    });
                }
                ActualizarCheckout();
            }

            Dispatcher.BeginInvoke(new Action(() => MostrarVista(GridExtras)),
                System.Windows.Threading.DispatcherPriority.Background);
        }

        private void BtnVolverDesdeSelector_Click(object sender, RoutedEventArgs e)
            => MostrarVista(GridExtras);

        private void TxtBuscador_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filtro = TxtBuscador.Text.Trim();
            ListaModulosSelector.ItemsSource = string.IsNullOrEmpty(filtro)
                ? _modulosFiltradosSelector
                : _modulosFiltradosSelector
                    .Where(m => m.Nombre.Contains(filtro, StringComparison.OrdinalIgnoreCase))
                    .ToList();
        }

        // ????????????????????????????????????????????????????????
        //  VISTA RESUMEN — paso final
        // ????????????????????????????????????????????????????????

        private void MostrarResumen()
        {
            ListaResumen.ItemsSource = _itemsCheckout;
            MostrarVista(GridResumen);
        }

        private void BtnVolverResumen_Click(object sender, RoutedEventArgs e)
            => IrAlPaso(_pasos.Count - 1);

        private void BtnInstalarAsistido_Click(object sender, RoutedEventArgs e)
        {
            var modulos = _itemsCheckout.Select(x => x.Modulo).Distinct().ToList();
            if (modulos.Count == 0) return;
            InstalacionSolicitada?.Invoke(this, new SesionAsistida { Modulos = modulos });
        }

        // ????????????????????????????????????????????????????????
        //  CHECKOUT BAR
        // ????????????????????????????????????????????????????????

        private void ActualizarCheckout()
        {
            int n = _itemsCheckout.Count;
            BarraCheckout.Visibility  = n > 0 ? Visibility.Visible : Visibility.Collapsed;
            TxtContadorCheckout.Text  = $"{n} módulo{(n != 1 ? "s" : "")} seleccionado{(n != 1 ? "s" : "")}";
        }

        private void BtnEliminarCheckout_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: ItemCheckoutVM item }) return;
            _itemsCheckout.Remove(item);

            // Limpiar de selecciones principales
            foreach (var kv in _selecciones.Where(kv => kv.Value == item.Modulo).ToList())
                _selecciones[kv.Key] = null;

            // Limpiar de subcategorías extras
            foreach (var sub in _subcategoriasExtras)
                sub.Seleccionados.Remove(item.Modulo);

            ActualizarCheckout();

            if (_pasoActual < _pasos.Count)
                ActualizarBotonSiguientePaso();
        }

        // ????????????????????????????????????????????????????????
        //  Helpers de filtrado
        // ????????????????????????????????????????????????????????

        private List<ModuloConfig> FiltrarPorEtiquetas(IReadOnlyList<string> etiquetas)
            => _todosModulos
                .Where(m => etiquetas.Any(e => CoincideEtiqueta(m, e)))
                .ToList();

        private List<ModuloConfig> FiltrarPorEtiqueta(string etiqueta)
            => _todosModulos
                .Where(m => CoincideEtiqueta(m, etiqueta))
                .ToList();

        private static bool CoincideEtiqueta(ModuloConfig modulo, string etiqueta)
            => modulo.Etiquetas != null &&
               modulo.Etiquetas.Any(t => string.Equals(t, etiqueta, StringComparison.OrdinalIgnoreCase));
    }
}
