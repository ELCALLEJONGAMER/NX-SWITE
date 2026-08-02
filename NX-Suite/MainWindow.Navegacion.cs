using NX_Swite.Core;
using NX_Swite.Models;
using NX_Swite.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace NX_Swite
{
    /// <summary>
    /// MainWindow � Navegaci�n entre mundos del men�, filtrado por
    /// categor�as/etiquetas y selecci�n de la vista visible (cat�logo,
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
            _filtroSoloActualizables = false;

            Servicios.Sonidos.Reproducir(EventoSonido.Navegacion);

            // El encabezado de la TopBar se actualiza de inmediato (no forma parte del �rea animada)
            ActualizarEncabezadoSeccion(mundo);

            // Obtener las transformadas del RenderTransform del contenido central
            var tg        = (System.Windows.Media.TransformGroup)GridContenidoCentralContenido.RenderTransform;
            var scale     = (System.Windows.Media.ScaleTransform)tg.Children[0];
            var translate = (System.Windows.Media.TranslateTransform)tg.Children[1];

            // Transici�n gamer: OUT ? carga contenido ? IN
            UI.Controles.UiAnimaciones.TransicionMundo(
                GridContenidoCentralContenido,
                scale,
                translate,
                FlashNavegacion,
                () =>
                {
                    ActualizarFiltrosDelMundo(mundo.Id);
                    MostrarVistaPorTipo(mundo.Tipo);
                    RefrescarVistaActual();
                });
        }

        private void ListaCategorias_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ChipsFiltro.SelectedItem is not FiltroMandoConfig filtro)
                return;

            _filtroSeleccionado = filtro;
            RefrescarVistaActual();
        }

        /// <summary>
        /// Punto �nico de refresco. Los m�dulos se filtran siempre por etiquetas,
        /// nunca por un campo "Mundo" del m�dulo.
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

            // ?? Cat�logo (diagrama, catalogo y tipos futuros) ??
            IEnumerable<ModuloConfig> modulos = _datosGist.Modulos ?? Enumerable.Empty<ModuloConfig>();

            // 1. Filtro base del mundo: muestra solo los m�dulos que tengan
            //    al menos una de las etiquetas declaradas en EtiquetasFiltro.
            //    Si EtiquetasFiltro est� vac�o, se muestran todos.
            var etiquetasBase = _mundoSeleccionado?.EtiquetasFiltro;
            if (etiquetasBase?.Count > 0)
            {
                modulos = modulos.Where(m =>
                    m.Etiquetas != null &&
                    m.Etiquetas.Any(t => etiquetasBase.Any(eb =>
                        string.Equals(t, eb, StringComparison.OrdinalIgnoreCase))));
            }

            // 2. Filtro secundario: categor�a seleccionada en la barra de chips.
            if (_filtroSeleccionado != null &&
                !string.IsNullOrWhiteSpace(_filtroSeleccionado.Tag) &&
                !string.Equals(_filtroSeleccionado.Tag, "all", StringComparison.OrdinalIgnoreCase))
            {
                modulos = _cerebro.FiltrarPorEtiqueta(modulos, _filtroSeleccionado.Tag);
            }

            // 3. Filtro de texto libre.
            if (!string.IsNullOrWhiteSpace(_textoBusqueda))
                modulos = _cerebro.FiltrarPorTexto(modulos, _textoBusqueda);

            // 3b. Filtro "solo actualizables" (entrada Actualizar Modulos).
            if (_filtroSoloActualizables)
                modulos = modulos.Where(m => m.AccionRapida == AccionRapidaModulo.Actualizar);

            // 4. Orden por prioridad de estado:
            //    Actualizar > Reinstalar > Instalado > NoInstalado > Ninguna
            modulos = modulos.OrderBy(m => m.AccionRapida switch
            {
                AccionRapidaModulo.Actualizar  => 0,
                AccionRapidaModulo.Reinstalar  => 1,
                AccionRapidaModulo.Reparar     => 1,
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

            if (string.Equals(mundo.Tipo, "cfw_hub", StringComparison.OrdinalIgnoreCase))
            {
                PanelTituloSeccion.Visibility              = Visibility.Collapsed;
                BtnHerramientasPersonalizacion.Visibility  = Visibility.Collapsed;
                TxtTopBarSeccion.Text                      = mundo.Nombre ?? "CFW";
                return;
            }

            if (string.Equals(mundo.Tipo, "asistido", StringComparison.OrdinalIgnoreCase))
            {
                PanelTituloSeccion.Visibility              = Visibility.Collapsed;
                BtnHerramientasPersonalizacion.Visibility  = Visibility.Collapsed;
                TxtTopBarSeccion.Text                      = "Instalacion Asistida";
                return;
            }

            PanelTituloSeccion.Visibility             = Visibility.Collapsed;
            BtnHerramientasPersonalizacion.Visibility = esPersonalizacion
                ? Visibility.Visible : Visibility.Collapsed;

            TxtTituloSeccion.Text    = mundo.Nombre ?? "CATALOGO";
            TxtSubtituloSeccion.Text = !string.IsNullOrWhiteSpace(mundo.Subtitulo)
                ? mundo.Subtitulo
                : "Selecciona una categoria para continuar";
            TxtTopBarSeccion.Text   = mundo.Nombre ?? "Catalogo";
        }

        private string _textoBusqueda = string.Empty;

        /// <summary>
        /// True cuando el catálogo debe mostrar únicamente los módulos con
        /// una actualización disponible (entrada "Actualizar Modulos" del
        /// selector de actualización del hub CFW). Se resetea al navegar
        /// a cualquier otro mundo/vista.
        /// </summary>
        private bool _filtroSoloActualizables = false;

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
            if (string.Equals(tipo, "cfw_hub", StringComparison.OrdinalIgnoreCase))
                MostrarVistaHubCFW();
            else if (string.Equals(tipo, "asistido", StringComparison.OrdinalIgnoreCase))
                MostrarVistaAsistida();
            else
                MostrarVistaCatalogo();
        }

        private void MostrarVistaCatalogo()
        {
            VistaNews.Visibility          = Visibility.Collapsed;
            VistaCatalogo.Visibility       = Visibility.Visible;
            VistaDetalle.Visibility        = Visibility.Collapsed;
            VistaAsistida.Visibility       = Visibility.Collapsed;
            VistaHubCFW.Visibility         = Visibility.Collapsed;
            VistaDiagnosticoSD.Visibility  = Visibility.Collapsed;
            PanelChipsFiltro.Visibility    = Visibility.Visible;
        }

        private void MostrarVistaDetalle()
        {
            VistaNews.Visibility          = Visibility.Collapsed;
            VistaCatalogo.Visibility       = Visibility.Collapsed;
            VistaDetalle.Visibility        = Visibility.Visible;
            VistaAsistida.Visibility       = Visibility.Collapsed;
            VistaHubCFW.Visibility         = Visibility.Collapsed;
            VistaDiagnosticoSD.Visibility  = Visibility.Collapsed;
            PanelChipsFiltro.Visibility    = Visibility.Collapsed;
        }

        private void MostrarVistaAsistida()
        {
            VistaNews.Visibility          = Visibility.Collapsed;
            VistaCatalogo.Visibility       = Visibility.Collapsed;
            VistaDetalle.Visibility        = Visibility.Collapsed;
            VistaAsistida.Visibility       = Visibility.Visible;
            VistaHubCFW.Visibility         = Visibility.Collapsed;
            VistaDiagnosticoSD.Visibility  = Visibility.Collapsed;
            PanelChipsFiltro.Visibility    = Visibility.Collapsed;
        }

        /// <summary>
        /// Muestra el hub CFW (dashboard con tarjetas: Alertas, Instalación,
        /// Catálogo, Personalización). Punto de entrada del mundo "cfw_hub".
        /// </summary>
        private void MostrarVistaHubCFW()
        {
            VistaNews.Visibility          = Visibility.Collapsed;
            VistaCatalogo.Visibility       = Visibility.Collapsed;
            VistaDetalle.Visibility        = Visibility.Collapsed;
            VistaAsistida.Visibility       = Visibility.Collapsed;
            VistaHubCFW.Visibility         = Visibility.Visible;
            VistaDiagnosticoSD.Visibility  = Visibility.Collapsed;
            PanelChipsFiltro.Visibility    = Visibility.Collapsed;

            ActualizarResumenesHubCFW();
        }

        /// <summary>
        /// Calcula y aplica los textos de resumen de las tarjetas del hub
        /// (Alertas y Catálogo) a partir del estado ya calculado del catálogo
        /// de módulos. No introduce lógica de diagnóstico nueva: reutiliza los
        /// mismos criterios que <see cref="ActualizarDiagnosticoSD"/> y
        /// <see cref="RefrescarVistaActual"/>.
        /// </summary>
        private void ActualizarResumenesHubCFW()
        {
            if (_catalogoModulos == null) return;

            int necesitanAccion = _catalogoModulos.Count(m =>
                m.AccionRapida == AccionRapidaModulo.Actualizar ||
                m.AccionRapida == AccionRapidaModulo.Reinstalar ||
                m.AccionRapida == AccionRapidaModulo.Reparar);

            VistaHubCFW.ActualizarResumenCatalogo(necesitanAccion > 0
                ? $"{necesitanAccion} modulo(s) necesitan atencion."
                : "Explora todos los modulos disponibles.");

            int necesitanActualizar = _catalogoModulos.Count(m =>
                m.AccionRapida == AccionRapidaModulo.Actualizar);

            VistaHubCFW.ActualizarResumenActualizar(necesitanActualizar > 0
                ? $"{necesitanActualizar} modulo(s) tienen una version mas reciente."
                : "Todo tu CFW esta al dia.");

            var instalados = _catalogoModulos
                .Where(m => m.EstadoSd != EstadoSdModulo.NoInstalado)
                .ToList();

            int conProblemas = instalados.Count(m => m.HallazgosConfig?.Count > 0);
            int conDepsRotas = 0;
            foreach (var modulo in instalados.Where(m => m.Dependencias?.Count > 0))
            {
                var pendientes = AnalizadorDependencias.Analizar(modulo, _catalogoModulos)
                    .Where(d => d.Estado != EstadoDependencia.OK);
                if (pendientes.Any()) conDepsRotas++;
            }

            int totalAlertas = conProblemas + conDepsRotas;
            VistaHubCFW.ActualizarResumenAlertas(totalAlertas > 0
                ? $"{totalAlertas} elemento(s) requieren revision."
                : "Todo en orden. No se encontraron problemas.");
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

        // ════════════════════════════════════════════════════════════════
        // Hub CFW — navegación desde las tarjetas hacia las vistas reales.
        // Fase 2 de la migración (ver CODEBASE_INDEX.md). Reutilizan la
        // infraestructura ya existente, sin lógica nueva de negocio.
        // ════════════════════════════════════════════════════════════════

        private void AbrirHubCFW_Alertas()
        {
            Servicios.Sonidos.Reproducir(EventoSonido.Navegacion);
            MostrarVistaDiagnosticoSD();
        }

        private void AbrirHubCFW_Instalacion()
        {
            Servicios.Sonidos.Reproducir(EventoSonido.Navegacion);

            // Restaura el comportamiento original del mundo "asistido": muestra
            // la VistaAsistida completa (selector Completo/Parcial + wizard
            // Bootloader -> CFW -> Firmware paso a paso).
            var mundoAsistido = _mundosMenu.FirstOrDefault(m =>
                string.Equals(m.Id, "asistido", StringComparison.OrdinalIgnoreCase));

            TxtTopBarSeccion.Text = mundoAsistido?.Nombre ?? "Instalacion Asistida";

            ActualizarFiltrosDelMundo(mundoAsistido?.Id ?? string.Empty);
            MostrarVistaAsistida();

            if (mundoAsistido != null)
                _mundoSeleccionado = mundoAsistido;

            RefrescarVistaActual();
        }

        /// <summary>
        /// Navega de vuelta al hub CFW desde el botón "VOLVER A CFW" del
        /// selector de modo de <see cref="VistaAsistida"/>.
        /// </summary>
        private void MostrarVistaHubCFWDesdeAsistido()
        {
            Servicios.Sonidos.Reproducir(EventoSonido.Navegacion);

            var mundoCfw = _mundosMenu.FirstOrDefault(m =>
                string.Equals(m.Tipo, "cfw_hub", StringComparison.OrdinalIgnoreCase));

            if (mundoCfw != null)
                _mundoSeleccionado = mundoCfw;

            TxtTopBarSeccion.Text = mundoCfw?.Nombre ?? "CFW";

            MostrarVistaHubCFW();
        }

        /// <summary>
        /// Botón "VOLVER" premium en la cabecera del catálogo/personalización
        /// (fila de búsqueda). Regresa al hub CFW igual que
        /// <see cref="MostrarVistaHubCFWDesdeAsistido"/>.
        /// </summary>
        private void BtnVolverHubDesdeCatalogo_Click(object sender, RoutedEventArgs e)
        {
            Servicios.Sonidos.Reproducir(EventoSonido.Navegacion);
            _filtroSoloActualizables = false;

            var mundoCfw = _mundosMenu.FirstOrDefault(m =>
                string.Equals(m.Tipo, "cfw_hub", StringComparison.OrdinalIgnoreCase));

            if (mundoCfw != null)
                _mundoSeleccionado = mundoCfw;

            TxtTopBarSeccion.Text = mundoCfw?.Nombre ?? "CFW";
            BtnHerramientasPersonalizacion.Visibility = Visibility.Collapsed;

            MostrarVistaHubCFW();
        }

        private void AbrirHubCFW_Catalogo()
        {
            Servicios.Sonidos.Reproducir(EventoSonido.Navegacion);
            TxtTopBarSeccion.Text = "Catalogo";
            _filtroSoloActualizables = false;
            ActualizarFiltrosDelMundo(string.Empty);
            MostrarVistaCatalogo();
            RefrescarVistaActual();
        }

        private void AbrirHubCFW_Personalizacion()
        {
            Servicios.Sonidos.Reproducir(EventoSonido.Navegacion);
            _filtroSoloActualizables = false;

            // Replica el comportamiento del mundo "personalizacion": muestra
            // el catalogo filtrado por sus etiquetas y deja visible el boton
            // de herramientas que abre la ventana de creacion de temas.
            var mundoPersonalizacion = _mundosMenu.FirstOrDefault(m =>
                string.Equals(m.Id, "personalizacion", StringComparison.OrdinalIgnoreCase));

            TxtTopBarSeccion.Text = mundoPersonalizacion?.Nombre ?? "Personalizacion";
            BtnHerramientasPersonalizacion.Visibility = Visibility.Visible;

            ActualizarFiltrosDelMundo(mundoPersonalizacion?.Id ?? string.Empty);
            MostrarVistaCatalogo();

            if (mundoPersonalizacion != null)
                _mundoSeleccionado = mundoPersonalizacion;

            RefrescarVistaActual();
        }

        private void AbrirHubCFW_Actualizar()
        {
            Servicios.Sonidos.Reproducir(EventoSonido.Navegacion);
            AbrirSelectorActualizacion();
        }

        // ════════════════════════════════════════════════════════════════
        // Selector de modo de actualización — mismo estilo visual que el
        // selector de instalación (VistaAsistida.OverlaySelectorModo).
        // Dos tarjetas: "Actualizar paquete predefinido" (reutiliza el
        // overlay Asistido Completo en modo solo-instalar) y
        // "Actualizar Modulos" (catálogo filtrado a módulos con
        // actualización pendiente).
        // ════════════════════════════════════════════════════════════════

        private void AbrirSelectorActualizacion()
        {
            // A diferencia de los overlays modales de ventana completa, este
            // selector vive dentro del área de contenido central: no debe
            // difuminar la topbar ni los paneles laterales.
            PanelSelectorActualizarOverlay.Opacity = 0;
            PanelSelectorActualizarOverlay.Visibility = Visibility.Visible;
            PanelSelectorActualizarOverlay.BeginAnimation(
                UIElement.OpacityProperty,
                new System.Windows.Media.Animation.DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(320))));
        }

        private void CerrarSelectorActualizacion()
        {
            PanelSelectorActualizarOverlay.Visibility = Visibility.Collapsed;
        }

        private void BtnCerrarSelectorActualizar_Click(object sender, RoutedEventArgs e)
        {
            Servicios.Sonidos.Reproducir(EventoSonido.Navegacion);
            CerrarSelectorActualizacion();
        }

        private void TarjetaSelectorActualizar_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Servicios.Sonidos.Reproducir(EventoSonido.Hover);
        }

        private void TarjetaActualizarPaquete_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Servicios.Sonidos.Reproducir(EventoSonido.Click);
            CerrarSelectorActualizacion();
            AbrirOverlayAsistidoCompleto(soloInstalar: true, mostrarSelectorModo: false);
        }

        private void TarjetaActualizarModulos_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Servicios.Sonidos.Reproducir(EventoSonido.Click);
            CerrarSelectorActualizacion();
            AbrirCatalogoSoloActualizables();
        }

        /// <summary>
        /// Muestra el catálogo filtrado únicamente con los módulos que
        /// tienen una actualización disponible (AccionRapida == Actualizar).
        /// Reutiliza la infraestructura de filtros existente.
        /// </summary>
        private void AbrirCatalogoSoloActualizables()
        {
            TxtTopBarSeccion.Text = "Actualizar Modulos";
            ActualizarFiltrosDelMundo(string.Empty);
            _filtroSoloActualizables = true;
            MostrarVistaCatalogo();
            RefrescarVistaActual();
        }

        /// <summary>
        /// Handler de la tarjeta "Herramientas" del hub CFW. Placeholder:
        /// aún no navega a ninguna vista real (ver TODO en CODEBASE_INDEX.md
        /// → "Hub CFW — tarjetas e imágenes remotas").
        /// </summary>
        private void AbrirHubCFW_Herramientas()
        {
            Servicios.Sonidos.Reproducir(EventoSonido.Click);
            Dialogos.Info("Proximamente: RP2040, respaldo de llaves y mas utilidades.", "Herramientas");
        }
    }
}
