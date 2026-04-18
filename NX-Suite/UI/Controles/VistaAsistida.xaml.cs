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

namespace NX_Suite.UI.Controles
{
    // ═══════════════════════════════════════════════════════════════
    //  ViewModels de UI  (solo viven en la capa de presentación)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// ViewModel que representa un slot del asistente (Firmware, Bootloader, CFW…).
    /// Envuelve un NodoDiagramaConfig y trackea la selección del usuario.
    /// </summary>
    public class SlotAsistidoVM : INotifyPropertyChanged
    {
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
            }
        }

        // Propiedades calculadas para el binding del XAML
        public bool   TieneSeleccion => Seleccion != null;
        public string NombreModulo   => Seleccion?.Nombre ?? string.Empty;
        public string VersionModulo  => Seleccion?.Versiones?.Count > 0
                                            ? Seleccion.Versiones[0].Version
                                            : string.Empty;
        public string IconoModulo    => Seleccion?.IconoUrl ?? string.Empty;

        // Delegados al NodoDiagramaConfig (para el binding del XAML)
        public string Nombre        => Nodo.Nombre;
        public string ColorNeon     => Nodo.ColorNeon;
        public bool   EsObligatorio => Nodo.EsObligatorio;

        public SlotAsistidoVM(NodoDiagramaConfig nodo)
        {
            Nodo = nodo ?? throw new ArgumentNullException(nameof(nodo));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// ViewModel que representa una subcategoría de complementos.
    /// Mantiene la lista de módulos seleccionados y expone SlotsVisibles
    /// (seleccionados + placeholder "+") para el DataTemplateSelector.
    /// </summary>
    public class SubcategoriaVM : INotifyPropertyChanged
    {
        public SubcategoriaConfig Config { get; }
        public ObservableCollection<ModuloConfig> Seleccionados { get; } = new();

        public string Nombre              => Config.Nombre;
        public bool   PermiteMultiseleccion => Config.PermiteMultiseleccion;

        /// <summary>
        /// Lista mixta: módulos seleccionados + un SlotVacioPlaceholder al final
        /// cuando se permiten más selecciones.
        /// </summary>
        public IEnumerable<object> SlotsVisibles
        {
            get
            {
                foreach (var m in Seleccionados)
                    yield return m;

                if (PermiteMultiseleccion || Seleccionados.Count == 0)
                    yield return new SlotVacioPlaceholder(this);
            }
        }

        public SubcategoriaVM(SubcategoriaConfig config)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            Seleccionados.CollectionChanged += (_, _)
                => OnPropertyChanged(nameof(SlotsVisibles));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// Objeto centinela que representa el slot vacío "+" en una subcategoría.
    /// El DataTemplateSelector lo detecta y muestra PlantillaSlotVacio.
    /// </summary>
    public class SlotVacioPlaceholder
    {
        public SubcategoriaVM Subcategoria { get; }
        public SlotVacioPlaceholder(SubcategoriaVM sub) { Subcategoria = sub; }
    }

    /// <summary>
    /// Elige entre PlantillaSlotRelleno (ModuloConfig) y PlantillaSlotVacio
    /// (SlotVacioPlaceholder) según el tipo del item.
    /// </summary>
    public class SlotTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? ModuloTemplate { get; set; }
        public DataTemplate? VacioTemplate  { get; set; }

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
            => item is SlotVacioPlaceholder ? VacioTemplate : ModuloTemplate;
    }

    /// <summary>
    /// Datos que se emiten al MainWindow cuando el usuario pulsa INSTALAR.
    /// </summary>
    public class SesionAsistida
    {
        /// <summary>Slots nucleares con su módulo seleccionado.</summary>
        public List<SlotAsistidoVM> SlotsNucleo { get; init; } = new();

        /// <summary>
        /// Complementos por slot. Clave: "{slotId}::{subcategoriaNombre}".
        /// </summary>
        public Dictionary<string, List<ModuloConfig>> Complementos { get; init; } = new();
    }

    // ═══════════════════════════════════════════════════════════════
    //  UserControl
    // ═══════════════════════════════════════════════════════════════

    public partial class VistaAsistida : UserControl
    {
        // ── Estado global ────────────────────────────────────────────
        private List<SlotAsistidoVM> _slots            = new();
        private List<ModuloConfig>   _todosModulos     = new();
        private List<ModuloConfig>   _modulosFiltrados = new();
        private bool                 _modoForzado;

        // ── Contexto del selector (Vista B) ─────────────────────────
        private SlotAsistidoVM?  _slotEnEdicion;
        private SubcategoriaVM?  _subcategoriaEnEdicion;
        private bool             _selectorDesdeComplementos;

        // ── Navegación de complementos (Vista C) ────────────────────
        private List<SlotAsistidoVM>                   _slotsConSubcategorias    = new();
        private int                                    _indiceComplementoActual;
        private Dictionary<string, List<SubcategoriaVM>> _subcategoriasPorSlot   = new();
        private List<SubcategoriaVM>                   _subcategoriasActuales    = new();

        /// <summary>
        /// Se dispara cuando el usuario confirma la instalación desde el Resumen.
        /// MainWindow debe suscribirse para iniciar el proceso.
        /// </summary>
        public event EventHandler<SesionAsistida>? InstalacionSolicitada;

        public VistaAsistida()
        {
            InitializeComponent();
        }

        // ════════════════════════════════════════════════════════════
        //  API pública
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Inicializa el asistente con los nodos del JSON y todos los módulos disponibles.
        /// Llamar cada vez que se selecciona un mundo de tipo "asistido".
        /// </summary>
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

            SwitchModo.IsChecked    = _modoForzado;
            SwitchModo.Content      = _modoForzado ? "MODO FORZADO" : "MODO LIBRE";
            ListaSlots.ItemsSource  = _slots;

            ActualizarBotonSiguienteNucleo();
            MostrarVista(GridNucleo);
        }

        // ════════════════════════════════════════════════════════════
        //  Navegación entre vistas
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
            // Construir la lista de slots que tienen selección Y subcategorías
            _slotsConSubcategorias = _slots
                .Where(s => s.TieneSeleccion && s.Seleccion!.TieneSubcategorias)
                .ToList();

            if (_slotsConSubcategorias.Count == 0)
            {
                MostrarResumen();
                return;
            }

            _indiceComplementoActual = 0;
            MostrarComplementoDeSlot(_slotsConSubcategorias[0]);
        }

        /// <summary>
        /// Muestra u oculta el botón Siguiente según las reglas del modo.
        /// Modo forzado → todos los slots obligatorios deben tener selección.
        /// Modo libre   → basta con tener al menos una selección.
        /// </summary>
        private void ActualizarBotonSiguienteNucleo()
        {
            bool algoSeleccionado  = _slots.Any(s => s.TieneSeleccion);
            bool obligatoriosCubiertos = _slots
                .Where(s => s.EsObligatorio)
                .All(s => s.TieneSeleccion);

            bool mostrar = _modoForzado
                ? obligatoriosCubiertos && algoSeleccionado
                : algoSeleccionado;

            BtnSiguienteNucleo.Visibility = mostrar
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        // ════════════════════════════════════════════════════════════
        //  VISTA B — Selector de módulos
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

            _modulosFiltrados                = FiltrarParaSubcategoria(subVM.Config);
            ListaModulosSelector.ItemsSource = _modulosFiltrados;

            MostrarVista(GridSelector);
        }

        private void BtnVolverDesdeSelector_Click(object sender, RoutedEventArgs e)
        {
            MostrarVista(_selectorDesdeComplementos ? GridComplementos : GridNucleo);
        }

        private void SelectorModulo_Click(object sender, MouseButtonEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is not ModuloConfig modulo)
                return;

            if (_selectorDesdeComplementos && _subcategoriaEnEdicion != null)
            {
                // Selección de complemento
                if (!_subcategoriaEnEdicion.PermiteMultiseleccion)
                    _subcategoriaEnEdicion.Seleccionados.Clear();

                if (!_subcategoriaEnEdicion.Seleccionados.Contains(modulo))
                    _subcategoriaEnEdicion.Seleccionados.Add(modulo);

                MostrarVista(GridComplementos);
            }
            else if (_slotEnEdicion != null)
            {
                // Selección de módulo nuclear
                _slotEnEdicion.Seleccion = modulo;
                ActualizarBotonSiguienteNucleo();
                MostrarVista(GridNucleo);
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
        //  VISTA C — Complementos del módulo
        // ════════════════════════════════════════════════════════════

        private void MostrarComplementoDeSlot(SlotAsistidoVM slot)
        {
            var modulo = slot.Seleccion!;

            TxtTituloComplementos.Text  = modulo.Nombre;
            TxtVersionComplementos.Text = modulo.Versiones?.Count > 0
                ? $"v{modulo.Versiones[0].Version}"
                : string.Empty;

            // Reusar VMs si el usuario vuelve atrás para no perder selecciones
            if (!_subcategoriasPorSlot.TryGetValue(slot.Nodo.Id, out var subVMs))
            {
                subVMs = modulo.Subcategorias
                               .Select(s => new SubcategoriaVM(s))
                               .ToList();
                _subcategoriasPorSlot[slot.Nodo.Id] = subVMs;
            }

            _subcategoriasActuales          = subVMs;
            ListaSubcategorias.ItemsSource  = _subcategoriasActuales;

            MostrarVista(GridComplementos);
        }

        private void ComplementoSlotClick(object sender, RoutedEventArgs e)
        {
            // Solo interesa el click sobre el botón del slot vacío
            if (e.OriginalSource is not Button { Tag: SlotVacioPlaceholder placeholder }) return;
            e.Handled = true;
            AbrirSelectorParaSubcategoria(placeholder.Subcategoria);
        }

        private void BtnVolverDesdeComplementos_Click(object sender, RoutedEventArgs e)
        {
            // Volver al slot anterior o al Núcleo si estamos en el primero
            if (_indiceComplementoActual > 0)
            {
                _indiceComplementoActual--;
                MostrarComplementoDeSlot(_slotsConSubcategorias[_indiceComplementoActual]);
            }
            else
            {
                MostrarVista(GridNucleo);
            }
        }

        private void BtnSiguienteComplementos_Click(object sender, RoutedEventArgs e)
        {
            _indiceComplementoActual++;

            if (_indiceComplementoActual < _slotsConSubcategorias.Count)
                MostrarComplementoDeSlot(_slotsConSubcategorias[_indiceComplementoActual]);
            else
                MostrarResumen();
        }

        // ════════════════════════════════════════════════════════════
        //  VISTA D — Resumen final
        // ════════════════════════════════════════════════════════════

        private void MostrarResumen()
        {
            ListaResumen.ItemsSource = _slots
                .Where(s => s.TieneSeleccion)
                .ToList();

            MostrarVista(GridResumen);
        }

        private void BtnInstalarAsistido_Click(object sender, RoutedEventArgs e)
        {
            var complementos = new Dictionary<string, List<ModuloConfig>>();

            foreach (var slot in _slotsConSubcategorias)
            {
                if (slot.Seleccion == null) continue;
                if (!_subcategoriasPorSlot.TryGetValue(slot.Nodo.Id, out var subVMs)) continue;

                foreach (var subVM in subVMs.Where(s => s.Seleccionados.Count > 0))
                {
                    var clave = $"{slot.Seleccion.Id}::{subVM.Nombre}";
                    complementos[clave] = subVM.Seleccionados.ToList();
                }
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
            if (nodo.CategoriasFiltro == null || nodo.CategoriasFiltro.Count == 0)
                return _todosModulos;

            return _todosModulos
                .Where(m => nodo.CategoriasFiltro.Any(cat => CoincideCategoria(m, cat)))
                .ToList();
        }

        private List<ModuloConfig> FiltrarParaSubcategoria(SubcategoriaConfig sub)
        {
            if (sub.CategoriasFiltro == null || sub.CategoriasFiltro.Count == 0)
                return _todosModulos;

            return _todosModulos
                .Where(m => sub.CategoriasFiltro.Any(cat => CoincideCategoria(m, cat)))
                .ToList();
        }

        private static bool CoincideCategoria(ModuloConfig modulo, string categoria)
        {
            if (string.Equals(modulo.Categoria, categoria, StringComparison.OrdinalIgnoreCase))
                return true;

            return modulo.Etiquetas != null &&
                   modulo.Etiquetas.Any(t =>
                       string.Equals(t, categoria, StringComparison.OrdinalIgnoreCase));
        }
    }
}