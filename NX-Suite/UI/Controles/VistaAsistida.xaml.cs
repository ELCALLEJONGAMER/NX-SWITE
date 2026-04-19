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

namespace NX_Suite.UI.Controles
{
    // ══════════════════════════════════════════════════════════════
    //  ViewModels de UI
    // ══════════════════════════════════════════════════════════════

    public class SlotAsistidoVM : INotifyPropertyChanged
    {
        /// <summary>
        /// Colores neon por etiqueta. El JSON puede sobreescribir el ColorNeon del nodo,
        /// pero si la etiqueta está aquí, este color tiene prioridad visual.
        /// </summary>
        private static readonly Dictionary<string, string> _coloresPorEtiqueta = new(
            StringComparer.OrdinalIgnoreCase)
        {
            { "firmware",    "#FFD700" },   // Oro
            { "cfw",         "#00D2FF" },   // Cian
            { "atmosphere",  "#00D2FF" },   // Cian
            { "bootloader",  "#A855F7" },   // Púrpura
            { "hekate",      "#A855F7" },   // Púrpura
            { "payload",     "#FF6B35" },   // Naranja
            { "sigpatches",  "#FF4444" },   // Rojo
            { "homebrew",    "#22C55E" },   // Verde
            { "theme",       "#EC4899" },   // Rosa
        };

        public NodoDiagramaConfig Nodo { get; }

        private ModuloConfig? _seleccion;
        public ModuloConfig? Seleccion
        {
            get => _seleccion;
            set
            {
                if (_seleccion == value) return;
                _seleccion = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TieneSeleccion));
                OnPropertyChanged(nameof(NombreModulo));
                OnPropertyChanged(nameof(VersionModulo));
                OnPropertyChanged(nameof(IconoModulo));
                OnPropertyChanged(nameof(ColorEtiqueta));
            }
        }

        public bool   TieneSeleccion => Seleccion != null;
        public string NombreModulo   => Seleccion?.Nombre ?? string.Empty;
        public string VersionModulo  => Seleccion?.Versiones?.Count > 0
                                            ? Seleccion.Versiones[0].Version
                                            : string.Empty;
        public string IconoModulo    => Seleccion?.IconoUrl ?? string.Empty;

        public string Nombre        => Nodo.Nombre;
        public bool   EsObligatorio => Nodo.EsObligatorio;

        /// <summary>
        /// Color neon basado en la primera etiqueta del nodo reconocida en el mapa.
        /// Si no hay coincidencia, usa el ColorNeon del nodo o el cian por defecto.
        /// </summary>
        public string ColorEtiqueta
        {
            get
            {
                var etiquetaConocida = Nodo.EtiquetasFiltro?
                    .FirstOrDefault(e => _coloresPorEtiqueta.ContainsKey(e));

                if (etiquetaConocida != null)
                    return _coloresPorEtiqueta[etiquetaConocida];

                return string.IsNullOrWhiteSpace(Nodo.ColorNeon)
                    ? "#00D2FF"
                    : Nodo.ColorNeon;
            }
        }

        public SlotAsistidoVM(NodoDiagramaConfig nodo)
        {
            Nodo = nodo ?? throw new ArgumentNullException(nameof(nodo));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class SubcategoriaVM : INotifyPropertyChanged
    {
        public string Etiqueta { get; }
        public string Nombre   { get; }
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
        {
            Etiqueta              = etiqueta ?? throw new ArgumentNullException(nameof(etiqueta));
            Nombre                = etiqueta;
            PermiteMultiseleccion = permiteMultiseleccion;
            Seleccionados.CollectionChanged += (_, _)
                => OnPropertyChanged(nameof(SlotsVisibles));
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

    public class SesionAsistida
    {
        public List<SlotAsistidoVM> SlotsNucleo { get; init; } = new();
        public Dictionary<string, List<ModuloConfig>> Complementos { get; init; } = new();
    }

    // ══════════════════════════════════════════════════════════════
    //  UserControl
    // ══════════════════════════════════════════════════════════════

    public partial class VistaAsistida : UserControl
    {
        private List<SlotAsistidoVM> _slots            = new();
        private List<ModuloConfig>   _todosModulos     = new();
        private List<ModuloConfig>   _modulosFiltrados = new();
        private bool                 _modoForzado;

        private SlotAsistidoVM?  _slotEnEdicion;
        private SubcategoriaVM?  _subcategoriaEnEdicion;
        private bool             _selectorDesdeComplementos;

        private List<SlotAsistidoVM>                      _slotsConComplementos  = new();
        private int                                       _indiceComplementoActual;
        private Dictionary<string, List<SubcategoriaVM>> _subcategoriasPorSlot  = new();
        private List<SubcategoriaVM>                      _subcategoriasActuales = new();

        public event EventHandler<SesionAsistida>? InstalacionSolicitada;

        public VistaAsistida() => InitializeComponent();

        // ════════════════════════════════════════════════════════════
        //  API pública
        // ════════════════════════════════════════════════════════════

        public void Cargar(List<NodoDiagramaConfig> nodos,
                           List<ModuloConfig>       modulos,
                           string                   modoAsistente)
        {
            _todosModulos = modulos ?? new List<ModuloConfig>();
            _modoForzado  = string.Equals(modoAsistente, "forzado",
                                          StringComparison.OrdinalIgnoreCase);

            _slots = (nodos ?? new List<NodoDiagramaConfig>())
                     .Select(n => new SlotAsistidoVM(n))
                     .ToList();

            _subcategoriasPorSlot.Clear();

            // ── Auto-relleno inteligente ──────────────────────────────
            // Si ya hay algo instalado o parcialmente instalado en la SD,
            // se pre-selecciona automáticamente en su slot correspondiente.
            foreach (var slot in _slots)
            {
                var moduloInstalado = FiltrarParaNodo(slot.Nodo)
                    .FirstOrDefault(m => m.EstadoSd == EstadoSdModulo.Instalado ||
                                        m.EstadoSd == EstadoSdModulo.ParcialmenteInstalado);

                if (moduloInstalado != null)
                    slot.Seleccion = moduloInstalado;
            }
            // ─────────────────────────────────────────────────────────

            SwitchModo.IsChecked   = _modoForzado;
            SwitchModo.Content     = _modoForzado ? "MODO FORZADO" : "MODO LIBRE";
            ListaSlots.ItemsSource = _slots;

            ActualizarBotonSiguienteNucleo();
            MostrarVista(GridNucleo);
        }

        // ════════════════════════════════════════════════════════════
        //  Navegación
        // ════════════════════════════════════════════════════════════

        private void MostrarVista(Grid vistaActiva)
        {
            GridNucleo.Visibility       = Visibility.Collapsed;
            GridSelector.Visibility     = Visibility.Collapsed;
            GridComplementos.Visibility = Visibility.Collapsed;
            GridResumen.Visibility      = Visibility.Collapsed;
            vistaActiva.Visibility      = Visibility.Visible;
        }

        // ════════════════════════════════════════════════════════════
        //  VISTA A — Núcleo
        // ════════════════════════════════════════════════════════════

        private void SlotBoton_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not Button { Tag: SlotAsistidoVM slot }) return;
            e.Handled = true;
            AbrirSelectorParaSlot(slot);
        }

        private void SwitchModo_Changed(object sender, RoutedEventArgs e)
        {
            _modoForzado       = SwitchModo.IsChecked == true;
            SwitchModo.Content = _modoForzado ? "MODO FORZADO" : "MODO LIBRE";
            ActualizarBotonSiguienteNucleo();
        }

        private void BtnSiguienteNucleo_Click(object sender, RoutedEventArgs e)
        {
            _slotsConComplementos = _slots
                .Where(s => s.TieneSeleccion && s.Seleccion!.TieneComplementos)
                .ToList();

            if (_slotsConComplementos.Count == 0) { MostrarResumen(); return; }

            _indiceComplementoActual = 0;
            MostrarComplementoDeSlot(_slotsConComplementos[0]);
        }

        private void ActualizarBotonSiguienteNucleo()
        {
            bool algoSeleccionado      = _slots.Any(s => s.TieneSeleccion);
            bool obligatoriosCubiertos = _slots.Where(s => s.EsObligatorio).All(s => s.TieneSeleccion);

            bool mostrar = _modoForzado
                ? obligatoriosCubiertos && algoSeleccionado
                : algoSeleccionado;

            BtnSiguienteNucleo.Visibility = mostrar ? Visibility.Visible : Visibility.Collapsed;
        }

        // ════════════════════════════════════════════════════════════
        //  VISTA B — Selector
        // ════════════════════════════════════════════════════════════

        private void AbrirSelectorParaSlot(SlotAsistidoVM slot)
        {
            _slotEnEdicion             = slot;
            _selectorDesdeComplementos = false;

            TxtTituloSelector.Text    = $"Selecciona {slot.Nombre}";
            TxtSubtituloSelector.Text = "El buscador filtra solo por esta categoría";
            TxtBuscador.Text          = string.Empty;

            _modulosFiltrados                = FiltrarParaNodo(slot.Nodo);
            ListaModulosSelector.ItemsSource = _modulosFiltrados;

            MostrarVista(GridSelector);
        }

        private void AbrirSelectorParaSubcategoria(SubcategoriaVM subVM)
        {
            _subcategoriaEnEdicion     = subVM;
            _selectorDesdeComplementos = true;

            TxtTituloSelector.Text    = subVM.Nombre;
            TxtSubtituloSelector.Text = subVM.PermiteMultiseleccion
                ? "Puedes seleccionar varios"
                : "Selecciona uno";
            TxtBuscador.Text = string.Empty;

            _modulosFiltrados                = FiltrarParaSubcategoria(subVM);
            ListaModulosSelector.ItemsSource = _modulosFiltrados;

            MostrarVista(GridSelector);
        }

        private void BtnVolverDesdeSelector_Click(object sender, RoutedEventArgs e)
            => MostrarVista(_selectorDesdeComplementos ? GridComplementos : GridNucleo);

        private void SelectorModulo_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            if ((e.OriginalSource as FrameworkElement)?.DataContext is not ModuloConfig modulo)
                return;

            if (_selectorDesdeComplementos && _subcategoriaEnEdicion != null)
            {
                if (!_subcategoriaEnEdicion.PermiteMultiseleccion)
                    _subcategoriaEnEdicion.Seleccionados.Clear();

                if (!_subcategoriaEnEdicion.Seleccionados.Contains(modulo))
                    _subcategoriaEnEdicion.Seleccionados.Add(modulo);

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    MostrarVista(GridComplementos);
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
            else if (_slotEnEdicion != null)
            {
                _slotEnEdicion.Seleccion = modulo;
                ActualizarBotonSiguienteNucleo();

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    MostrarVista(GridNucleo);
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private void TxtBuscador_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filtro = TxtBuscador.Text.Trim();
            ListaModulosSelector.ItemsSource = string.IsNullOrEmpty(filtro)
                ? _modulosFiltrados
                : _modulosFiltrados
                    .Where(m => m.Nombre.Contains(filtro, StringComparison.OrdinalIgnoreCase))
                    .ToList();
        }

        // ════════════════════════════════════════════════════════════
        //  VISTA C — Complementos
        // ════════════════════════════════════════════════════════════

        private void MostrarComplementoDeSlot(SlotAsistidoVM slot)
        {
            var modulo = slot.Seleccion!;

            TxtTituloComplementos.Text  = modulo.Nombre;
            TxtVersionComplementos.Text = modulo.Versiones?.Count > 0
                ? $"v{modulo.Versiones[0].Version}"
                : string.Empty;

            if (!_subcategoriasPorSlot.TryGetValue(slot.Nodo.Id, out var subVMs))
            {
                subVMs = modulo.Complementos
                               .Select(c => new SubcategoriaVM(c, permiteMultiseleccion: true))
                               .ToList();
                _subcategoriasPorSlot[slot.Nodo.Id] = subVMs;
            }

            _subcategoriasActuales         = subVMs;
            ListaSubcategorias.ItemsSource = _subcategoriasActuales;
            MostrarVista(GridComplementos);
        }

        private void ComplementoSlotClick(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not Button { Tag: SlotVacioPlaceholder placeholder }) return;
            e.Handled = true;
            AbrirSelectorParaSubcategoria(placeholder.Subcategoria);
        }

        private void BtnVolverDesdeComplementos_Click(object sender, RoutedEventArgs e)
        {
            if (_indiceComplementoActual > 0)
            {
                _indiceComplementoActual--;
                MostrarComplementoDeSlot(_slotsConComplementos[_indiceComplementoActual]);
            }
            else MostrarVista(GridNucleo);
        }

        private void BtnSiguienteComplementos_Click(object sender, RoutedEventArgs e)
        {
            _indiceComplementoActual++;
            if (_indiceComplementoActual < _slotsConComplementos.Count)
                MostrarComplementoDeSlot(_slotsConComplementos[_indiceComplementoActual]);
            else
                MostrarResumen();
        }

        // ════════════════════════════════════════════════════════════
        //  VISTA D — Resumen
        // ════════════════════════════════════════════════════════════

        private void MostrarResumen()
        {
            ListaResumen.ItemsSource = _slots.Where(s => s.TieneSeleccion).ToList();
            MostrarVista(GridResumen);
        }

        private void BtnInstalarAsistido_Click(object sender, RoutedEventArgs e)
        {
            var complementos = new Dictionary<string, List<ModuloConfig>>();

            foreach (var slot in _slotsConComplementos)
            {
                if (slot.Seleccion == null) continue;
                if (!_subcategoriasPorSlot.TryGetValue(slot.Nodo.Id, out var subVMs)) continue;

                foreach (var subVM in subVMs.Where(s => s.Seleccionados.Count > 0))
                    complementos[$"{slot.Seleccion.Id}::{subVM.Etiqueta}"] =
                        subVM.Seleccionados.ToList();
            }

            InstalacionSolicitada?.Invoke(this, new SesionAsistida
            {
                SlotsNucleo  = _slots.Where(s => s.TieneSeleccion).ToList(),
                Complementos = complementos
            });
        }

        // ════════════════════════════════════════════════════════════
        //  Helpers de filtrado
        // ════════════════════════════════════════════════════════════

        private List<ModuloConfig> FiltrarParaNodo(NodoDiagramaConfig nodo)
        {
            if (nodo.EtiquetasFiltro == null || nodo.EtiquetasFiltro.Count == 0)
                return _todosModulos;

            return _todosModulos
                .Where(m => nodo.EtiquetasFiltro.Any(ef => CoincideEtiqueta(m, ef)))
                .ToList();
        }

        private List<ModuloConfig> FiltrarParaSubcategoria(SubcategoriaVM sub)
            => _todosModulos
                .Where(m => CoincideEtiqueta(m, sub.Etiqueta) ||
                            string.Equals(m.Id, sub.Etiqueta, StringComparison.OrdinalIgnoreCase))
                .ToList();

        private static bool CoincideEtiqueta(ModuloConfig modulo, string etiqueta)
            => modulo.Etiquetas != null &&
               modulo.Etiquetas.Any(t => string.Equals(t, etiqueta, StringComparison.OrdinalIgnoreCase));

        private void BtnEliminarSlot_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true; // evita que suba al SlotBoton_Click del ItemsControl

            if (sender is Button { Tag: SlotAsistidoVM slot })
            {
                slot.Seleccion = null;
                ActualizarBotonSiguienteNucleo();
            }
        }

        private void SlotSeleccionado_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject origen &&
                BuscarPadre<Button>(origen) is not null)
            {
                return; // Si fue click en X o ↺, no abrir selector.
            }

            if (sender is FrameworkElement { DataContext: SlotAsistidoVM slot })
            {
                e.Handled = true;
                AbrirSelectorParaSlot(slot);
            }
        }

        private static T? BuscarPadre<T>(DependencyObject? actual) where T : DependencyObject
        {
            while (actual != null)
            {
                if (actual is T encontrado)
                    return encontrado;

                actual = VisualTreeHelper.GetParent(actual);
            }

            return null;
        }
    }

}