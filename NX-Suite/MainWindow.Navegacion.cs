using NX_Suite.Core;
using NX_Suite.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace NX_Suite
{
    /// <summary>
    /// MainWindow — Navegación entre mundos del menú, filtrado por
    /// categorías/etiquetas y selección de la vista visible (catálogo,
    /// detalle o asistido).
    /// </summary>
    public partial class MainWindow
    {
        private UI.VentanaPersonalizacion? _ventanaPersonalizacion;

        private void ListaMundos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_cargandoCatalogoInicial) return;
            if (MenuMundos.ListaMundos.SelectedItem is not MundoMenuConfig mundo) return;

            _mundoSeleccionado  = mundo;
            _filtroSeleccionado = null;

            Servicios.Sonidos.Reproducir(EventoSonido.Navegacion);

            ActualizarEncabezadoSeccion(mundo);
            ActualizarFiltrosDelMundo(mundo.Id);
            MostrarVistaPorTipo(mundo.Tipo);
            RefrescarVistaActual();
        }

        private void ListaCategorias_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ChipsFiltro.SelectedItem is not FiltroMandoConfig filtro)
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

            // ?? Vista asistida: la VistaAsistida gestiona su propio filtrado interno ??
            if (string.Equals(_mundoSeleccionado?.Tipo, "asistido", StringComparison.OrdinalIgnoreCase))
            {
                var nodos = _datosGist.DiagramaNodos ?? new List<NodoDiagramaConfig>();
                var todos = _datosGist.Modulos       ?? new List<ModuloConfig>();
                VistaAsistida.Cargar(nodos, todos, _mundoSeleccionado?.ModoAsistente ?? "libre");
                return;
            }

            // ?? Catálogo (diagrama, catalogo y tipos futuros) ??
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

            // 2. Filtro secundario: categoría seleccionada en la barra de chips.
            if (_filtroSeleccionado != null &&
                !string.IsNullOrWhiteSpace(_filtroSeleccionado.Tag) &&
                !string.Equals(_filtroSeleccionado.Tag, "all", StringComparison.OrdinalIgnoreCase))
            {
                modulos = _cerebro.FiltrarPorEtiqueta(modulos, _filtroSeleccionado.Tag);
            }

            // 3. Filtro de texto libre.
            if (!string.IsNullOrWhiteSpace(_textoBusqueda))
                modulos = _cerebro.FiltrarPorTexto(modulos, _textoBusqueda);

            // 4. Orden por prioridad de estado:
            //    Actualizar > Reinstalar > Instalado > NoInstalado > Ninguna
            modulos = modulos.OrderBy(m => m.AccionRapida switch
            {
                AccionRapidaModulo.Actualizar  => 0,
                AccionRapidaModulo.Reinstalar  => 1,
                AccionRapidaModulo.Eliminar    => 2,
                AccionRapidaModulo.Instalar    => 3,
                _                              => 4,
            });

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

        private string _textoBusqueda = string.Empty;

        private void ActualizarFiltrosDelMundo(string mundoId)
        {
            if (_filtrosCentroMando == null) return;

            var filtros = _filtrosCentroMando
                .Where(f => f.Mundos == null || f.Mundos.Count == 0 ||
                            f.Mundos.Any(m => string.Equals(m, mundoId, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            ChipsFiltro.ItemsSource   = filtros;
            ChipsFiltro.SelectedIndex = -1;
            _filtroSeleccionado       = null;

            _textoBusqueda            = string.Empty;
            TxtBusqueda.Text          = string.Empty;

            PanelChipsFiltro.Visibility = Visibility.Visible;
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
            VistaCatalogo.Visibility     = Visibility.Visible;
            VistaDetalle.Visibility      = Visibility.Collapsed;
            VistaAsistida.Visibility     = Visibility.Collapsed;
            PanelChipsFiltro.Visibility  = Visibility.Visible;
        }

        private void MostrarVistaDetalle()
        {
            VistaCatalogo.Visibility    = Visibility.Collapsed;
            VistaDetalle.Visibility     = Visibility.Visible;
            VistaAsistida.Visibility    = Visibility.Collapsed;
            PanelChipsFiltro.Visibility = Visibility.Collapsed;
        }

        private void MostrarVistaAsistida()
        {
            VistaCatalogo.Visibility    = Visibility.Collapsed;
            VistaDetalle.Visibility     = Visibility.Collapsed;
            VistaAsistida.Visibility    = Visibility.Visible;
            PanelChipsFiltro.Visibility = Visibility.Collapsed;
        }

        private void ChipsFiltro_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var item = (e.OriginalSource as FrameworkElement)?.DataContext as FiltroMandoConfig;
            if (item == null || item != _filtroSeleccionado) return;

            // Clic sobre el chip ya seleccionado ? deseleccionar
            e.Handled = true;
            ChipsFiltro.SelectedIndex = -1;
            _filtroSeleccionado       = null;
            RefrescarVistaActual();
        }

        private void TxtBusqueda_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            _textoBusqueda                   = TxtBusqueda.Text;
            PlaceholderBusqueda.Visibility   = string.IsNullOrEmpty(_textoBusqueda)
                                               ? Visibility.Visible
                                               : Visibility.Collapsed;
            RefrescarVistaActual();
        }

        private void BtnHerramientasPersonalizacion_Click(object sender, RoutedEventArgs e)
        {
            if (_ventanaPersonalizacion is { IsVisible: true })
            {
                AplicarBlurFondo(true);
                PanelPersonalizacionBackdrop.Visibility = Visibility.Visible;
                _ventanaPersonalizacion.Activate();
                return;
            }

            AplicarBlurFondo(true);
            PanelPersonalizacionBackdrop.Visibility = Visibility.Visible;

            _ventanaPersonalizacion = new UI.VentanaPersonalizacion
            {
                Owner = this,
                Opacity = 0
            };

            _ventanaPersonalizacion.Closed += (_, _) =>
            {
                _ventanaPersonalizacion = null;
                PanelPersonalizacionBackdrop.Visibility = Visibility.Collapsed;
                AplicarBlurFondo(false);
            };

            _ventanaPersonalizacion.Show();
            _ventanaPersonalizacion.BeginAnimation(
                UIElement.OpacityProperty,
                new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(320))));
        }

        private void PanelPersonalizacionBackdrop_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            e.Handled = true;
            _ventanaPersonalizacion?.Close();
        }
    }
}
