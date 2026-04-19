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
    //  ViewModels de UI
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// ViewModel que representa un slot del asistente (Firmware, Bootloader, CFW…).
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

        public bool   TieneSeleccion => Seleccion != null;
        public string NombreModulo   => Seleccion?.Nombre ?? string.Empty;
        public string VersionModulo  => Seleccion?.Versiones?.Count > 0
                                            ? Seleccion.Versiones[0].Version
                                            : string.Empty;
        public string IconoModulo    => Seleccion?.IconoUrl ?? string.Empty;

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
    /// ViewModel de un grupo de complementos derivado de ModuloConfig.Complementos.
    /// Cada entrada de la lista Complementos se convierte en una SubcategoriaVM.
    /// </summary>
    public class SubcategoriaVM : INotifyPropertyChanged
    {
        /// <summary>Etiqueta o ID que define qué módulos pertenecen a este grupo.</summary>
        public string Etiqueta { get; }

        /// <summary>Nombre visible en la UI.</summary>
        public string Nombre { get; }

        /// <summary>true → el usuario puede elegir varios módulos en este grupo.</summary>
        public bool PermiteMultiseleccion { get; init; } = true;

        public ObservableCollection<ModuloConfig> Seleccionados { get; } = new();

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

        public SubcategoriaVM(string etiqueta, bool permiteMultiseleccion = true)
        {
            Etiqueta            = etiqueta ?? throw new ArgumentNullException(nameof(etiqueta));
            Nombre              = etiqueta;
            PermiteMultiseleccion = permiteMultiseleccion;
            Seleccionados.CollectionChanged += (_, _)
                => OnPropertyChanged(nameof(SlotsVisibles));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// Centinela que representa el slot vacío "+" en una subcategoría.
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
    /// Datos emitidos al MainWindow cuando el usuario pulsa INSTALAR.
    /// </summary>
    public class SesionAsistida
    {
        public List<SlotAsistidoVM> SlotsNucleo { get; init; } = new();
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
        private List<SlotAsistidoVM>                      _slotsConComplementos   = new();
        private int                                       _indiceComplementoActual;
        private Dictionary<string, List<SubcategoriaVM>> _subcategoriasPorSlot   = new();
        private List<SubcategoriaVM>                      _subcategoriasActuales  = new();

        public event EventHandler<SesionAsistida>? InstalacionSolicitada;

        public VistaAsistida()
        {
            InitializeComponent();
        }

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

            SwitchModo.IsChecked   = _modoForzado;
            SwitchModo.Content     = _modoForzado ? "MODO FORZADO" : "MODO LIBRE";
            ListaSlots.ItemsSource = _slots;

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
            // Slots que tienen selección Y al menos un complemento definido
            _slotsConComplementos = _slots
                .Where(s => s.TieneSeleccion && s.Seleccion!.TieneComplementos)
                .ToList();

            if (_slotsConComplementos.Count == 0)
            {
                MostrarResumen();
                return;
            }

            _indiceComplementoActual = 0;
            MostrarComplementoDeSlot(_slotsConComplementos[0]);
        }

        private void ActualizarBotonSiguienteNucleo()
        {
            bool algoSeleccionado      = _slots.Any(s => s.TieneSeleccion);
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

            _modulosFiltrados                = FiltrarParaSubcategoria(subVM);
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
                if (!_subcategoriaEnEdicion.PermiteMultiseleccion)
                    _subcategoriaEnEdicion.Seleccionados.Clear();

                if (!_subcategoriaEnEdicion.Seleccionados.Contains(modulo))
                    _subcategoriaEnEdicion.Seleccionados.Add(modulo);

                MostrarVista(GridComplementos);
            }
            else if (_slotEnEdicion != null)
            {
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
                // Cada entrada de Complementos se convierte en un grupo seleccionable.
                // Por defecto todos permiten multiselección; ajusta según necesidad.
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
            else
            {
                MostrarVista(GridNucleo);
            }
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

            foreach (var slot in _slotsConComplementos)
            {
                if (slot.Seleccion == null) continue;
                if (!_subcategoriasPorSlot.TryGetValue(slot.Nodo.Id, out var subVMs)) continue;

                foreach (var subVM in subVMs.Where(s => s.Seleccionados.Count > 0))
                {
                    var clave = $"{slot.Seleccion.Id}::{subVM.Etiqueta}";
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

        /// <summary>
        /// Filtra módulos para un slot de núcleo usando NodoDiagramaConfig.EtiquetasFiltro.
        /// </summary>
        private List<ModuloConfig> FiltrarParaNodo(NodoDiagramaConfig nodo)
        {
            if (nodo.EtiquetasFiltro == null || nodo.EtiquetasFiltro.Count == 0)
                return _todosModulos;

            return _todosModulos
                .Where(m => nodo.EtiquetasFiltro.Any(ef => CoincideEtiqueta(m, ef)))
                .ToList();
        }

        /// <summary>
        /// Filtra módulos para una subcategoría usando SubcategoriaVM.Etiqueta.
        /// Busca por ID exacto o por etiqueta del módulo.
        /// </summary>
        private List<ModuloConfig> FiltrarParaSubcategoria(SubcategoriaVM sub)
        {
            return _todosModulos
                .Where(m => CoincideEtiqueta(m, sub.Etiqueta) ||
                            string.Equals(m.Id, sub.Etiqueta, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        /// <summary>
        /// Comprueba si un módulo tiene la etiqueta indicada.
        /// </summary>
        private static bool CoincideEtiqueta(ModuloConfig modulo, string etiqueta)
            => modulo.Etiquetas != null &&
               modulo.Etiquetas.Any(t => string.Equals(t, etiqueta, StringComparison.OrdinalIgnoreCase));
    }
}