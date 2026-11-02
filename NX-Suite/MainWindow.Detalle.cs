using NX_Swite.Core;
using NX_Swite.Hardware;
using NX_Swite.Models;
using NX_Swite.Services;
using NX_Swite.UI;
using NX_Swite.UI.Controles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace NX_Swite
{
    /// <summary>
    /// MainWindow — Vista de detalle de un módulo: cabecera, badges, screenshots,
    /// caché local y todos los botones de acción (Instalar / Borrar /
    /// Abrir ubicación / Sitio web / Limpiar caché).
    /// </summary>
    public partial class MainWindow
    {
        private ModuloVersion?                 _versionSeleccionadaDetalle;
        private int                              _idxOriginalVersionSeleccionada = -1;
        private bool                             _btnActualizarEsDegradacion;

        /// <summary>
        /// Carga una imagen reducida al ancho indicado. Las locales se decodifican
        /// una vez y se congelan; las remotas mantienen la descarga asíncrona de WPF.
        /// </summary>
        private static BitmapImage CargarImagenDetalle(string uri, int anchoMax)
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource        = new Uri(uri);
            bmp.DecodePixelWidth = anchoMax;
            if (System.IO.File.Exists(uri)) bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            if (bmp.CanFreeze && !bmp.IsDownloading) bmp.Freeze();
            return bmp;
        }
        private List<(Border Chip, Rectangle Barra, Color ColorBase, bool EsSoloDeteccion, ModuloVersion Version)> _infoChipsVersiones = new();

        private void AbrirDetalleModulo(ModuloConfig modulo, bool desdeAsistido = false)
        {
            if (modulo == null) return;
            _detalleDesdeAsistido = desdeAsistido;

            _versionSeleccionadaDetalle = null;
            _infoChipsVersiones.Clear();

            _moduloActual = modulo;

            // ── Textos básicos ──
            TxtTituloDetalle.Text = modulo.Nombre ?? string.Empty;
            TxtDescDetalle.Text   = modulo.Descripcion ?? string.Empty;

            // ── Header de estado: badge + texto de versión derivados de EstadoSd ──
            ActualizarHeaderEstadoDetalle(modulo);
            ActualizarDiagnosticoParcial(modulo);

            // ?? Etiquetas ??
            ListaEtiquetasDetalle.ItemsSource = modulo.Etiquetas?.Count > 0 ? modulo.Etiquetas : null;

            // ?? Screenshots ??
            bool tieneScreenshots = modulo.ScreenshotsUrl?.Count > 0;
            PanelScreenshots.Visibility  = tieneScreenshots ? Visibility.Visible : Visibility.Collapsed;
            if (tieneScreenshots) ListaScreenshots.ItemsSource = modulo.ScreenshotsUrl;
            ActualizarAlineacionCapturas();

            // ?? Cache section ??
            RefrescarSeccionCache(modulo);
            RellenarChipsVersiones(modulo);

            // ?? Imagen del icono ??
            if (!string.IsNullOrEmpty(modulo.IconoUrl))
            {
                try
                {
                    string? rutaLocal = Core.Servicios.Iconos.ObtenerRutaLocal(modulo.IconoUrl);
                    string uriStr     = rutaLocal ?? modulo.IconoUrl;
                    var bmp = CargarImagenDetalle(uriStr, 256);
                    ImgDetalle.Source = bmp;

                    if (rutaLocal == null)
                        _ = Core.Servicios.Iconos.DescargarSiNoExisteAsync(modulo.IconoUrl);
                }
                catch { ImgDetalle.Source = null; }
            }
            else ImgDetalle.Source = null;

            // ?? Banner (usa BannerUrl si existe, si no usa el icono como fondo) ??
            string bannerSrc = !string.IsNullOrEmpty(modulo.BannerUrl) ? modulo.BannerUrl : modulo.IconoUrl;
            if (!string.IsNullOrEmpty(bannerSrc))
            {
                try
                {
                    string? rutaLocal = Core.Servicios.Iconos.ObtenerRutaLocal(bannerSrc);
                    string uriStr     = rutaLocal ?? bannerSrc;
                    BrushBannerDetalle.ImageSource = CargarImagenDetalle(uriStr, 1280);
                }
                catch { BrushBannerDetalle.ImageSource = null; }
            }
            else BrushBannerDetalle.ImageSource = null;

            // ?? Visibilidad inteligente de botones ??
            ActualizarBotonesDetalle(modulo);

            VistaNews.Visibility        = Visibility.Collapsed;
            VistaCatalogo.Visibility    = Visibility.Collapsed;
            VistaAsistida.Visibility    = Visibility.Collapsed;
            PanelChipsFiltro.Visibility = Visibility.Collapsed;
            PanelTituloSeccion.Visibility = Visibility.Collapsed;
            UiAnimaciones.MostrarDetalle(VistaDetalle);
            BtnCerrarPaneles_Click(null, null);
        }

        private void ActualizarBotonesDetalle(ModuloConfig modulo)
        {
            bool haySd = !string.IsNullOrEmpty((InfoSD.ComboDrives.SelectedItem as SDInfo)?.Letra);

            var acciones = DetalleModuloLogic.DeterminarAcciones(modulo, _versionSeleccionadaDetalle, haySd);

            _btnActualizarEsDegradacion = acciones.EsDegradacion;

            BtnSitioWebDetalle.Visibility       = acciones.MostrarSitioWeb ? Visibility.Visible : Visibility.Collapsed;
            BtnInstalarDetalle.Visibility       = acciones.MostrarInstalar ? Visibility.Visible : Visibility.Collapsed;
            BtnActualizarDetalle.Visibility     = acciones.MostrarActualizar ? Visibility.Visible : Visibility.Collapsed;
            BtnBorrarDetalle.Visibility         = acciones.MostrarBorrar ? Visibility.Visible : Visibility.Collapsed;
            BtnAbrirUbicacionDetalle.Visibility = acciones.MostrarAbrirUbicacion ? Visibility.Visible : Visibility.Collapsed;

            // Textos de los botones (temporal: sin multiidioma todavia, preserva strings exactos).
            switch (acciones.TipoAccionInstalar)
            {
                case AccionInstalarDetalle.InstalarNormal:
                    bool esReinstalarSemantico = modulo.EstadoSd == EstadoSdModulo.ParcialmenteInstalado ||
                                                  modulo.EstadoSd == EstadoSdModulo.InstaladoVersionDesconocida;
                    BtnInstalarDetalle.Content = haySd
                        ? (esReinstalarSemantico ? "REINSTALAR EN SD" : "INSTALAR EN SD")
                        : modulo.TieneCache ? "REINSTALAR EN CACHÉ PC" : "DESCARGAR EN CACHÉ (PC)";
                    break;
                case AccionInstalarDetalle.InstalarVersionSeleccionada:
                    BtnInstalarDetalle.Content = haySd
                        ? $"INSTALAR v{acciones.VersionObjetivo} EN SD"
                        : $"DESCARGAR v{acciones.VersionObjetivo}";
                    break;
                case AccionInstalarDetalle.InstalarVersionSinEliminar:
                    BtnInstalarDetalle.Content = $"INSTALAR v{acciones.VersionObjetivo}";
                    break;
            }

            switch (acciones.TipoAccionActualizar)
            {
                case AccionActualizarDetalle.ActualizarNormal:
                    BtnActualizarDetalle.Content = "ACTUALIZAR";
                    break;
                case AccionActualizarDetalle.ActualizarAVersion:
                    BtnActualizarDetalle.Content = $"ACTUALIZAR A v{acciones.VersionObjetivo}";
                    break;
                case AccionActualizarDetalle.DegradarAVersion:
                    BtnActualizarDetalle.Content = $"DEGRADAR A v{acciones.VersionObjetivo}";
                    break;
            }
        }

        /// <summary>
        /// Actualiza el header de estado (badge centrado encima del t&#237;tulo + texto de
        /// versi&#243;n + acento ambiental de fondo) a partir de <see cref="ModuloConfig.EstadoSd"/>.
        /// &#218;nica fuente visual del estado del m&#243;dulo: la antigua badge lateral fue eliminada.
        /// </summary>
        private void ActualizarHeaderEstadoDetalle(ModuloConfig modulo)
        {
            Color colorEstado;
            string textoBadge;
            string textoVersion;

            switch (modulo.EstadoSd)
            {
                case EstadoSdModulo.Instalado:
                    colorEstado  = Color.FromArgb(255, 64, 192, 87);   // verde
                    textoBadge   = "INSTALADO";
                    textoVersion = !string.IsNullOrWhiteSpace(modulo.VersionInstalada) &&
                                   modulo.VersionInstalada is not ("No detectado" or "No instalado")
                        ? $"Versi\u00f3n instalada: {modulo.VersionInstalada}"
                        : string.Empty;
                    break;

                case EstadoSdModulo.ParcialmenteInstalado:
                    colorEstado  = Color.FromArgb(255, 245, 158, 11);  // ambar
                    textoBadge   = "PARCIAL";
                    textoVersion = !string.IsNullOrWhiteSpace(modulo.VersionInstalada) &&
                                   modulo.VersionInstalada is not ("No detectado" or "No instalado")
                        ? $"Versi\u00f3n detectada: {modulo.VersionInstalada}"
                        : "Versi\u00f3n detectada: --";
                    break;

                case EstadoSdModulo.InstaladoVersionDesconocida:
                    colorEstado  = Color.FromArgb(255, 255, 107, 0);   // naranja
                    textoBadge   = "VERSI\u00d3N NO RECONOCIDA";
                    textoVersion = "Versi\u00f3n instalada: No reconocida";
                    break;

                default: // NoInstalado
                    colorEstado  = Color.FromArgb(255, 160, 160, 176); // gris neutro
                    textoBadge   = "NO INSTALADO";
                    textoVersion = modulo.Versiones?.Count > 0
                        ? $"Versi\u00f3n recomendada: {modulo.Versiones[0].Version}"
                        : string.Empty;
                    break;
            }

            BadgeEstadoHeaderDetalle.Background     = new SolidColorBrush(Color.FromArgb(30, colorEstado.R, colorEstado.G, colorEstado.B));
            BadgeEstadoHeaderDetalle.BorderBrush     = new SolidColorBrush(Color.FromArgb(180, colorEstado.R, colorEstado.G, colorEstado.B));
            TxtEstadoHeaderDetalle.Text       = textoBadge;
            TxtEstadoHeaderDetalle.Foreground = new SolidColorBrush(colorEstado);
            TxtVersionDetalle.Text            = textoVersion;

            GlowHeaderStop1.Color = Color.FromArgb(20, colorEstado.R, colorEstado.G, colorEstado.B);
        }

        /// <summary>
        /// Diagnóstico de instalación parcial (DETALLE PREMIUM 3A): muestra la MISMA
        /// evidencia (rutas faltantes) que produjo <see cref="Core.DetectorVersionesLogic"/>
        /// para el estado <see cref="EstadoSdModulo.ParcialmenteInstalado"/> — no vuelve a
        /// analizar la SD, solo lee <see cref="ModuloConfig.ArchivosFaltantesDeteccion"/>.
        /// </summary>
        private void ActualizarDiagnosticoParcial(ModuloConfig modulo)
        {
            // Cerrar el detalle expandido al cambiar de módulo/refrescar.
            PanelDetallesDiagnostico.Visibility = Visibility.Collapsed;
            TxtBtnVerDetallesDiagnostico.Text   = "VER DETALLES";

            if (modulo.EstadoSd != EstadoSdModulo.ParcialmenteInstalado)
            {
                PanelDiagnosticoParcial.Visibility = Visibility.Collapsed;
                return;
            }

            PanelDiagnosticoParcial.Visibility = Visibility.Visible;

            bool versionConocida = !string.IsNullOrWhiteSpace(modulo.VersionInstalada) &&
                                    modulo.VersionInstalada is not ("No detectado" or "No instalado");

            if (versionConocida)
            {
                TxtDiagnosticoVersion.Text = $"Versi\u00f3n identificada: {modulo.VersionInstalada}";

                int n = modulo.ArchivosFaltantesDeteccion?.Count ?? 0;
                TxtDiagnosticoResumen.Text = n == 1
                    ? "Falta 1 elemento requerido."
                    : $"Faltan {n} elementos requeridos.";

                bool hayFaltantes = n > 0;
                BtnVerDetallesDiagnostico.Visibility = hayFaltantes ? Visibility.Visible : Visibility.Collapsed;
                ListaElementosFaltantes.ItemsSource  = hayFaltantes ? modulo.ArchivosFaltantesDeteccion : null;
            }
            else
            {
                TxtDiagnosticoVersion.Text = "Versi\u00f3n no determinada";
                TxtDiagnosticoResumen.Text = "Se detectaron componentes del m\u00f3dulo, pero no fue posible " +
                                             "determinar una versi\u00f3n completa.";

                // No hay una firma concreta identificada: no se puede afirmar de forma
                // fiable qué archivos faltan, así que no se muestra ninguna lista.
                BtnVerDetallesDiagnostico.Visibility = Visibility.Collapsed;
                ListaElementosFaltantes.ItemsSource  = null;
            }
        }

        private void BtnVerDetallesDiagnostico_Click(object sender, RoutedEventArgs e)
        {
            bool expandido = PanelDetallesDiagnostico.Visibility == Visibility.Visible;
            PanelDetallesDiagnostico.Visibility = expandido ? Visibility.Collapsed : Visibility.Visible;
            TxtBtnVerDetallesDiagnostico.Text   = expandido ? "VER DETALLES" : "OCULTAR DETALLES";
        }

        private void BtnCopiarDiagnostico_Click(object sender, RoutedEventArgs e)
        {
            if (_moduloActual == null) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine(_moduloActual.Nombre ?? string.Empty);
            sb.AppendLine("Estado: Instalaci\u00f3n incompleta");
            sb.AppendLine(TxtDiagnosticoVersion.Text);

            if (_moduloActual.ArchivosFaltantesDeteccion?.Count > 0)
            {
                sb.AppendLine("Elementos faltantes:");
                foreach (var ruta in _moduloActual.ArchivosFaltantesDeteccion)
                    sb.AppendLine($"- {ruta}");
            }

            try { Clipboard.SetText(sb.ToString()); } catch { /* portapapeles ocupado: ignorar */ }
        }

        private void RefrescarEstadoDetalle()
        {
            if (_moduloActual == null || _datosGist?.Modulos == null) return;

            var refrescado = _datosGist.Modulos.FirstOrDefault(m => m.Id == _moduloActual.Id);
            if (refrescado == null) return;

            // Limpiar selección de versión para que los botones muestren el estado real
            _versionSeleccionadaDetalle = null;
            _infoChipsVersiones.Clear();

            _moduloActual = refrescado;

            ActualizarHeaderEstadoDetalle(refrescado);
            ActualizarDiagnosticoParcial(refrescado);
            ActualizarBotonesDetalle(refrescado);
            RefrescarSeccionCache(refrescado);
            RellenarChipsVersiones(refrescado);
        }

        private void BtnVolver_Click(object sender, RoutedEventArgs e)
        {
            _moduloActual = null;
            if (_detalleDesdeAsistido)
            {
                _detalleDesdeAsistido = false;
                UiAnimaciones.OcultarDetalle(VistaDetalle, () =>
                {
                    VistaAsistida.Visibility = Visibility.Visible;
                    UiAnimaciones.FadeInCatalogo(VistaAsistida);
                });
            }
            else
            {
                UiAnimaciones.OcultarDetalle(VistaDetalle, () =>
                {
                    VistaCatalogo.Visibility    = Visibility.Visible;
                    PanelChipsFiltro.Visibility = Visibility.Visible;
                    UiAnimaciones.FadeInCatalogo(VistaCatalogo);
                });
            }
        }

        private async void BtnInstalar_Click(object sender, RoutedEventArgs e)
        {
            if (_moduloActual == null) return;

            // ¿Es el botón DEGRADAR (desinstala la versión actual antes de instalar la vieja)?
            bool esDegradacion = ReferenceEquals(sender, BtnActualizarDetalle) && _btnActualizarEsDegradacion;

            string? letraSD = (InfoSD.ComboDrives.SelectedItem as SDInfo)?.Letra;
            if (string.IsNullOrEmpty(letraSD))
            {
                // Sin SD: descargar a caché local (degradación sin SD no elimina nada)
                SwapVersionSeleccionadaAlFrente();
                await EjecutarInstalacionRapidaAsync(_moduloActual, string.Empty);
                RestaurarOrdenVersiones();
                RefrescarSeccionCache(_moduloActual);
                RellenarChipsVersiones(_moduloActual);
                ActualizarBotonesDetalle(_moduloActual);
                return;
            }

            string tituloOperacion = esDegradacion
                ? $"Degradando {_moduloActual.Nombre}"
                : $"Instalando {_moduloActual.Nombre}";
            var itemQueue = Servicios.Cola.AgregarItem(tituloOperacion);

            SwapVersionSeleccionadaAlFrente();
            try
            {
                _pantallaCarga.Mostrar(tituloOperacion);

                // Si es degradación: primero eliminar la versión superior de la SD
                if (esDegradacion)
                {
                    Servicios.Cola.ActualizarItem(itemQueue, 5, "Eliminando versión actual de la SD...");
                    await _cerebro.DesinstalarModuloAsync(_moduloActual, letraSD);
                }

                // Reportador compuesto: actualiza overlay Y cola
                var reportadorOverlay = _pantallaCarga.ObtenerReportador();
                var progreso = new Progress<EstadoProgreso>(p =>
                {
                    ((IProgress<EstadoProgreso>)reportadorOverlay).Report(p);
                    Servicios.Cola.ActualizarItem(itemQueue, p.Porcentaje, p.TareaActual);
                });

                var resultado = await _cerebro.InstalarModuloAsync(_moduloActual, letraSD, progreso, itemQueue.Token);

                if (resultado.Exito)
                {
                    await Task.Delay(1000);
                    _pantallaCarga.Ocultar();
                    Servicios.Cola.CompletarItem(itemQueue);

                    if (_catalogoModulos != null)
                        _cerebro.ActualizarEstadoCacheCatalogo(_catalogoModulos);

                    // Restaurar orden ANTES de refrescar para que los chips se construyan correctamente
                    RestaurarOrdenVersiones();

                    // Releer el estado de la SD directamente desde disco (sin red) ANTES de
                    // llamar a RefrescarEstadoDetalle, para no depender del chain async de
                    // ComboDrives_SelectionChanged que aún no ha terminado su SincronizarTodoAsync.
                    if (_catalogoModulos != null)
                        await _cerebro.RefrescarEstadosSinRedAsync(_catalogoModulos, letraSD);

                    await ActualizarListaUnidadesAsync();
                    RefrescarVistaActual();
                    RefrescarEstadoDetalle();

                    // ActualizarListaUnidadesAsync dispara ComboDrives_SelectionChanged que
                    // muestra los paneles del catálogo — restauramos el estado de la vista detalle
                    PanelChipsFiltro.Visibility   = Visibility.Collapsed;
                    PanelTituloSeccion.Visibility = Visibility.Collapsed;
                    VistaCatalogo.Visibility      = Visibility.Collapsed;

                    string msgExito = esDegradacion
                        ? $"¡{_moduloActual?.Nombre} se ha degradado a v{_versionSeleccionadaDetalle?.Version} correctamente!"
                        : $"¡{_moduloActual?.Nombre} se ha instalado correctamente!";
                    Dialogos.Info(msgExito, "Éxito");
                }
                else
                {
                    _pantallaCarga.Ocultar();
                    Servicios.Cola.ErrorItem(itemQueue, resultado.MensajeError);
                    Dialogos.Error($"Error durante la instalación:\n\n{resultado.MensajeError}", "Fallo");
                }
            }
            catch (OperationCanceledException)
            {
                _pantallaCarga.Ocultar();
                Servicios.Cola.CancelarItem(itemQueue);
                Dialogos.Info($"Instalación de {_moduloActual?.Nombre} cancelada.", "Cancelado");
            }
            catch (Exception ex)
            {
                _pantallaCarga.Ocultar();
                Servicios.Cola.ErrorItem(itemQueue, ex.Message);
                Dialogos.Error($"Excepción en la interfaz: {ex.Message}", "Error Crítico");
            }
            finally
            {
                RestaurarOrdenVersiones();
            }
        }

        /// <summary>Mueve la versión seleccionada a la posición 0 del listado y guarda el índice original.</summary>
        private void SwapVersionSeleccionadaAlFrente()
        {
            _idxOriginalVersionSeleccionada = -1;
            if (_moduloActual == null || _versionSeleccionadaDetalle == null) return;
            int idx = _moduloActual.Versiones.IndexOf(_versionSeleccionadaDetalle);
            if (idx <= 0) return;
            _idxOriginalVersionSeleccionada = idx;
            (_moduloActual.Versiones[0], _moduloActual.Versiones[idx]) =
                (_moduloActual.Versiones[idx], _moduloActual.Versiones[0]);
        }

        /// <summary>Restaura la versión seleccionada a su posición original usando el índice guardado.</summary>
        private void RestaurarOrdenVersiones()
        {
            int idx = _idxOriginalVersionSeleccionada;
            _idxOriginalVersionSeleccionada = -1;
            if (_moduloActual == null || idx <= 0 || idx >= _moduloActual.Versiones.Count) return;
            (_moduloActual.Versiones[0], _moduloActual.Versiones[idx]) =
                (_moduloActual.Versiones[idx], _moduloActual.Versiones[0]);
        }

        private async void BtnBorrar_Click(object sender, RoutedEventArgs e)
        {
            if (_moduloActual == null) return;

            string? letraSD = (InfoSD.ComboDrives.SelectedItem as SDInfo)?.Letra;
            if (string.IsNullOrEmpty(letraSD)) return;

            try
            {
                bool exito = await _cerebro.DesinstalarModuloAsync(_moduloActual, letraSD);

                if (exito)
                {
                    if (_catalogoModulos != null)
                        _cerebro.ActualizarEstadoCacheCatalogo(_catalogoModulos);

                    // Releer estado de SD desde disco antes de actualizar la vista
                    if (_catalogoModulos != null)
                        await _cerebro.RefrescarEstadosSinRedAsync(_catalogoModulos, letraSD);

                    await ActualizarListaUnidadesAsync();
                    RefrescarVistaActual();
                    RefrescarEstadoDetalle();

                    // Restaurar estado de la vista detalle
                    PanelChipsFiltro.Visibility   = Visibility.Collapsed;
                    PanelTituloSeccion.Visibility = Visibility.Collapsed;
                    VistaCatalogo.Visibility      = Visibility.Collapsed;

                    Dialogos.Info($"¡{_moduloActual?.Nombre} se ha eliminado!", "Éxito");
                }
                else
                {
                    Dialogos.Advertencia("Hubo un error al intentar borrar algunos archivos.");
                }
            }
            catch (Exception ex)
            {
                Dialogos.Error($"Error al eliminar: {ex.Message}");
            }
        }

        private void BtnAbrirUbicacion_Click(object sender, RoutedEventArgs e)
        {
            if (_moduloActual == null) return;

            string? letraSD = (InfoSD.ComboDrives.SelectedItem as SDInfo)?.Letra;
            if (string.IsNullOrEmpty(letraSD))
            {
                Dialogos.Advertencia("No hay ninguna SD seleccionada.");
                return;
            }

            try
            {
                string raizSD         = letraSD.TrimEnd('\\') + "\\";
                string carpetaDestino = raizSD;

                // Determinar qué versión usar para resolver la ubicación:
                // 1º versión seleccionada en el chip, 2º versión instalada, 3º cualquiera
                string versionRef = _versionSeleccionadaDetalle?.Version
                                 ?? _moduloActual.VersionInstalada
                                 ?? string.Empty;

                // Buscar la firma de detección que coincide con esa versión
                var firmaVersion = !string.IsNullOrEmpty(versionRef)
                    ? _moduloActual.FirmasDeteccion?.FirstOrDefault(f =>
                          string.Equals(f.Version, versionRef, StringComparison.OrdinalIgnoreCase))
                    : null;

                // Archivos a inspeccionar: los de la versión encontrada, o todos si no hay coincidencia
                var archivos = firmaVersion?.Archivos
                    ?? _moduloActual.FirmasDeteccion?
                           .SelectMany(f => f.Archivos ?? Enumerable.Empty<ArchivoCritico>())
                           .ToList();

                // Prioridad: primer archivo con SHA256 (identificador exacto de versión)
                var archivoSha = archivos?
                    .FirstOrDefault(a => !string.IsNullOrWhiteSpace(a.SHA256) &&
                                         !string.IsNullOrWhiteSpace(a.Ruta));

                if (archivoSha != null)
                {
                    string relativa    = archivoSha.Ruta.TrimStart('/', '\\').Replace('/', '\\');
                    string rutaArchivo = System.IO.Path.Combine(raizSD, relativa);
                    string? dir        = System.IO.Path.GetDirectoryName(rutaArchivo);
                    if (!string.IsNullOrEmpty(dir) && System.IO.Directory.Exists(dir))
                        carpetaDestino = dir;
                }
                else
                {
                    // Fallback: primer archivo sin SHA256
                    var primerArchivo = archivos?
                        .FirstOrDefault(a => !string.IsNullOrWhiteSpace(a.Ruta));

                    if (primerArchivo != null)
                    {
                        string relativa    = primerArchivo.Ruta.TrimStart('/', '\\').Replace('/', '\\');
                        string rutaArchivo = System.IO.Path.Combine(raizSD, relativa);
                        string? dir        = System.IO.File.Exists(rutaArchivo)
                            ? System.IO.Path.GetDirectoryName(rutaArchivo)
                            : (System.IO.Directory.Exists(rutaArchivo) ? rutaArchivo : null);
                        if (!string.IsNullOrEmpty(dir) && System.IO.Directory.Exists(dir))
                            carpetaDestino = dir;
                    }
                }

                // Abrir explorador seleccionando el archivo concreto si existe
                string? archivoFinal = archivoSha != null
                    ? System.IO.Path.Combine(raizSD,
                          archivoSha.Ruta.TrimStart('/', '\\').Replace('/', '\\'))
                    : null;

                if (archivoFinal != null && System.IO.File.Exists(archivoFinal))
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{archivoFinal}\"");
                else
                    System.Diagnostics.Process.Start("explorer.exe", carpetaDestino);
            }
            catch (Exception ex)
            {
                Dialogos.Error($"No se pudo abrir la ubicación: {ex.Message}");
            }
        }

        private void BtnSitioWeb_Click(object sender, RoutedEventArgs e)
        {
            if (_moduloActual == null || string.IsNullOrWhiteSpace(_moduloActual.UrlOficial)) return;

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName        = _moduloActual.UrlOficial,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Dialogos.Error($"No se pudo abrir el sitio web: {ex.Message}");
            }
        }
    }
}
