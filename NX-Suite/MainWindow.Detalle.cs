using NX_Suite.Core;
using NX_Suite.Hardware;
using NX_Suite.Models;
using NX_Suite.UI;
using NX_Suite.UI.Controles;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NX_Suite
{
    /// <summary>
    /// MainWindow — Vista de detalle de un módulo: cabecera, badges, screenshots,
    /// caché local y todos los botones de acción (Instalar / Borrar /
    /// Abrir ubicación / Sitio web / Limpiar caché).
    /// </summary>
    public partial class MainWindow
    {
        private void AbrirDetalleModulo(ModuloConfig modulo, bool desdeAsistido = false)
        {
            if (modulo == null) return;
            _detalleDesdeAsistido = desdeAsistido;

            _moduloActual = modulo;

            // ?? Textos básicos ??
            TxtTituloDetalle.Text  = modulo.Nombre ?? string.Empty;
            TxtDescDetalle.Text    = modulo.Descripcion ?? string.Empty;
            TxtVersionDetalle.Text = modulo.Versiones?.Count > 0
                ? $"Versión: {modulo.Versiones[0].Version}"
                : "Versión: --";

            // ?? Badge de estado ??
            if (modulo.EstaInstaladoEnSd)
            {
                BadgeEstadoDetalle.Background  = new SolidColorBrush(Color.FromArgb(30, 0, 210, 100));
                BadgeEstadoDetalle.BorderBrush = new SolidColorBrush(Color.FromArgb(180, 0, 210, 100));
                BadgeEstadoDetalle.BorderThickness = new Thickness(1);
                TxtEstadoDetalle.Text       = "? INSTALADO";
                TxtEstadoDetalle.Foreground = new SolidColorBrush(Color.FromArgb(255, 0, 210, 100));
                TxtVersionInstaladaDetalle.Text = !string.IsNullOrWhiteSpace(modulo.VersionInstalada) &&
                                                   modulo.VersionInstalada is not ("No detectado" or "No instalado")
                    ? $"v{modulo.VersionInstalada} instalada"
                    : string.Empty;
            }
            else
            {
                BadgeEstadoDetalle.Background  = new SolidColorBrush(Color.FromArgb(30, 189, 0, 255));
                BadgeEstadoDetalle.BorderBrush = new SolidColorBrush(Color.FromArgb(120, 189, 0, 255));
                BadgeEstadoDetalle.BorderThickness = new Thickness(1);
                TxtEstadoDetalle.Text       = "? NO INSTALADO";
                TxtEstadoDetalle.Foreground = new SolidColorBrush(Color.FromArgb(255, 189, 0, 255));
                TxtVersionInstaladaDetalle.Text = string.Empty;
            }

            // ?? Etiquetas ??
            ListaEtiquetasDetalle.ItemsSource = modulo.Etiquetas?.Count > 0 ? modulo.Etiquetas : null;

            // ?? Screenshots ??
            bool tieneScreenshots = modulo.ScreenshotsUrl?.Count > 0;
            PanelScreenshots.Visibility  = tieneScreenshots ? Visibility.Visible : Visibility.Collapsed;
            if (tieneScreenshots) ListaScreenshots.ItemsSource = modulo.ScreenshotsUrl;

            // ?? Cache section ??
            RefrescarSeccionCache(modulo);

            // ?? Imagen del icono ??
            if (!string.IsNullOrEmpty(modulo.IconoUrl))
            {
                try
                {
                    string? rutaLocal = Core.Servicios.Iconos.ObtenerRutaLocal(modulo.IconoUrl);
                    string uriStr     = rutaLocal ?? modulo.IconoUrl;
                    var bmp = new BitmapImage(new Uri(uriStr));
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
                    BrushBannerDetalle.ImageSource = new BitmapImage(new Uri(uriStr));
                }
                catch { BrushBannerDetalle.ImageSource = null; }
            }
            else BrushBannerDetalle.ImageSource = null;

            // ?? Visibilidad inteligente de botones ??
            ActualizarBotonesDetalle(modulo);

            VistaCatalogo.Visibility    = Visibility.Collapsed;
            VistaAsistida.Visibility    = Visibility.Collapsed;
            PanelChipsFiltro.Visibility = Visibility.Collapsed;
            PanelTituloSeccion.Visibility = Visibility.Collapsed;
            UiAnimaciones.MostrarDetalle(VistaDetalle);
            BtnCerrarPaneles_Click(null, null);
        }

        private void ActualizarBotonesDetalle(ModuloConfig modulo)
        {
            bool instalado     = modulo.EstaInstaladoEnSd;
            bool tieneUpdate   = modulo.TieneActualizacion;
            bool tieneSitioWeb = !string.IsNullOrWhiteSpace(modulo.UrlOficial);

            BtnInstalarDetalle.Visibility        = instalado ? Visibility.Collapsed : Visibility.Visible;
            BtnActualizarDetalle.Visibility      = (instalado && tieneUpdate) ? Visibility.Visible : Visibility.Collapsed;
            BtnBorrarDetalle.Visibility          = instalado ? Visibility.Visible : Visibility.Collapsed;
            BtnAbrirUbicacionDetalle.Visibility  = instalado ? Visibility.Visible : Visibility.Collapsed;
            BtnSitioWebDetalle.Visibility        = tieneSitioWeb ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Busca el módulo actual en los datos recién sincronizados y refresca
        /// el badge de estado y los botones de acción sin reiniciar animaciones.
        /// </summary>
        private void RefrescarEstadoDetalle()
        {
            if (_moduloActual == null || _datosGist?.Modulos == null) return;

            var refrescado = _datosGist.Modulos.FirstOrDefault(m => m.Id == _moduloActual.Id);
            if (refrescado == null) return;

            _moduloActual = refrescado;

            // Actualizar badge
            if (refrescado.EstaInstaladoEnSd)
            {
                BadgeEstadoDetalle.Background  = new SolidColorBrush(Color.FromArgb(30, 0, 210, 100));
                BadgeEstadoDetalle.BorderBrush = new SolidColorBrush(Color.FromArgb(180, 0, 210, 100));
                BadgeEstadoDetalle.BorderThickness = new Thickness(1);
                TxtEstadoDetalle.Text       = "? INSTALADO";
                TxtEstadoDetalle.Foreground = new SolidColorBrush(Color.FromArgb(255, 0, 210, 100));
                TxtVersionInstaladaDetalle.Text = !string.IsNullOrWhiteSpace(refrescado.VersionInstalada) &&
                                                   refrescado.VersionInstalada is not ("No detectado" or "No instalado")
                    ? $"v{refrescado.VersionInstalada} instalada"
                    : string.Empty;
            }
            else
            {
                BadgeEstadoDetalle.Background  = new SolidColorBrush(Color.FromArgb(30, 189, 0, 255));
                BadgeEstadoDetalle.BorderBrush = new SolidColorBrush(Color.FromArgb(120, 189, 0, 255));
                BadgeEstadoDetalle.BorderThickness = new Thickness(1);
                TxtEstadoDetalle.Text       = "? NO INSTALADO";
                TxtEstadoDetalle.Foreground = new SolidColorBrush(Color.FromArgb(255, 189, 0, 255));
                TxtVersionInstaladaDetalle.Text = string.Empty;
            }

            ActualizarBotonesDetalle(refrescado);
            RefrescarSeccionCache(refrescado);
        }

        private void RefrescarSeccionCache(ModuloConfig modulo)
        {
            if (modulo == null) return;
            bool zipExiste     = !string.IsNullOrEmpty(modulo.RutaCacheZip)
                                 && System.IO.File.Exists(modulo.RutaCacheZip);
            bool carpetaExiste = !string.IsNullOrEmpty(modulo.RutaCacheCarpeta)
                                 && (System.IO.Directory.Exists(modulo.RutaCacheCarpeta)
                                     || System.IO.File.Exists(modulo.RutaCacheCarpeta));

            FilaCacheZip.Visibility      = zipExiste     ? Visibility.Visible : Visibility.Collapsed;
            FilaCacheCarpeta.Visibility  = carpetaExiste ? Visibility.Visible : Visibility.Collapsed;
            PanelCacheDetalle.Visibility = (zipExiste || carpetaExiste) ? Visibility.Visible : Visibility.Collapsed;
            TxtTamanoZip.Text      = zipExiste     ? "…" : string.Empty;
            TxtTamanoCarpeta.Text  = carpetaExiste ? "…" : string.Empty;

            if (zipExiste || carpetaExiste)
                _ = ComputarTamanosCacheAsync(modulo, zipExiste, carpetaExiste);
        }

        private async Task ComputarTamanosCacheAsync(ModuloConfig modulo, bool zipExiste, bool carpetaExiste)
        {
            string tamZip = string.Empty, tamCarpeta = string.Empty;
            await Task.Run(() =>
            {
                if (zipExiste && System.IO.File.Exists(modulo.RutaCacheZip))
                    tamZip = FormatBytes(new System.IO.FileInfo(modulo.RutaCacheZip).Length);
                if (carpetaExiste)
                {
                    if (System.IO.Directory.Exists(modulo.RutaCacheCarpeta))
                    {
                        long total = new System.IO.DirectoryInfo(modulo.RutaCacheCarpeta)
                            .GetFiles("*", System.IO.SearchOption.AllDirectories)
                            .Sum(f => f.Length);
                        tamCarpeta = FormatBytes(total);
                    }
                    else if (System.IO.File.Exists(modulo.RutaCacheCarpeta))
                        tamCarpeta = FormatBytes(new System.IO.FileInfo(modulo.RutaCacheCarpeta).Length);
                }
            });
            TxtTamanoZip.Text     = string.IsNullOrEmpty(tamZip)     ? string.Empty : $"({tamZip})";
            TxtTamanoCarpeta.Text = string.IsNullOrEmpty(tamCarpeta) ? string.Empty : $"({tamCarpeta})";
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1_073_741_824L) return $"{bytes / 1_073_741_824.0:F1} GB";
            if (bytes >= 1_048_576L)     return $"{bytes / 1_048_576.0:F1} MB";
            if (bytes >= 1024L)          return $"{bytes / 1024.0:F0} KB";
            return $"{bytes} B";
        }

        private void BtnBorrarCacheZip_Click(object sender, RoutedEventArgs e)
        {
            if (_moduloActual == null || string.IsNullOrEmpty(_moduloActual.RutaCacheZip)) return;
            try
            {
                if (System.IO.File.Exists(_moduloActual.RutaCacheZip))
                    System.IO.File.Delete(_moduloActual.RutaCacheZip);
                if (_catalogoModulos != null) _cerebro.ActualizarEstadoCacheCatalogo(_catalogoModulos);
                RefrescarSeccionCache(_moduloActual);
            }
            catch (Exception ex)
            {
                Dialogos.Error($"Error al borrar ZIP: {ex.Message}");
            }
        }

        private void BtnBorrarCacheCarpeta_Click(object sender, RoutedEventArgs e)
        {
            if (_moduloActual == null || string.IsNullOrEmpty(_moduloActual.RutaCacheCarpeta)) return;
            try
            {
                if (System.IO.Directory.Exists(_moduloActual.RutaCacheCarpeta))
                    System.IO.Directory.Delete(_moduloActual.RutaCacheCarpeta, true);
                else if (System.IO.File.Exists(_moduloActual.RutaCacheCarpeta))
                    System.IO.File.Delete(_moduloActual.RutaCacheCarpeta);
                if (_catalogoModulos != null) _cerebro.ActualizarEstadoCacheCatalogo(_catalogoModulos);
                RefrescarSeccionCache(_moduloActual);
            }
            catch (Exception ex)
            {
                Dialogos.Error($"Error al borrar caché extraído: {ex.Message}");
            }
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
                    VistaCatalogo.Visibility      = Visibility.Visible;
                    PanelChipsFiltro.Visibility   = Visibility.Visible;
                    if (_mundoSeleccionado != null)
                        PanelTituloSeccion.Visibility = Visibility.Visible;
                    UiAnimaciones.FadeInCatalogo(VistaCatalogo);
                });
            }
        }

        private async void BtnInstalar_Click(object sender, RoutedEventArgs e)
        {
            if (_moduloActual == null) return;

            string? letraSD = (InfoSD.ComboDrives.SelectedItem as SDInfo)?.Letra;
            if (string.IsNullOrEmpty(letraSD))
            {
                Dialogos.Advertencia("No hay ninguna SD seleccionada para instalar.");
                return;
            }

            var itemQueue = Servicios.Cola.AgregarItem($"Instalando {_moduloActual.Nombre}");

            try
            {
                _pantallaCarga.Mostrar($"Instalando {_moduloActual.Nombre}");

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

                    await ActualizarListaUnidadesAsync();
                    RefrescarVistaActual();
                    RefrescarEstadoDetalle();

                    Dialogos.Info($"¡{_moduloActual?.Nombre} se ha instalado correctamente!", "Éxito");
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
        }

        private async void BtnBorrar_Click(object sender, RoutedEventArgs e)
        {
            if (_moduloActual == null) return;

            if (!Dialogos.Confirmar(
                    $"¿Estás seguro de que deseas eliminar {_moduloActual.Nombre} de la SD?",
                    "Confirmar Eliminación", MessageBoxImage.Warning))
                return;

            string? letraSD = (InfoSD.ComboDrives.SelectedItem as SDInfo)?.Letra;
            if (string.IsNullOrEmpty(letraSD)) return;

            try
            {
                bool exito = await _cerebro.DesinstalarModuloAsync(_moduloActual, letraSD);

                if (exito)
                {
                    if (_catalogoModulos != null)
                        _cerebro.ActualizarEstadoCacheCatalogo(_catalogoModulos);

                    await ActualizarListaUnidadesAsync();
                    RefrescarVistaActual();
                    RefrescarEstadoDetalle();

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
                // letraSD viene en formato "H:\" desde DriveInfo.GetDrives(), no agregar separadores extra
                string raizSD        = letraSD.TrimEnd('\\') + "\\";
                string carpetaDestino = raizSD;

                // 1. Prioridad: primer archivo con SHA256 en FirmasDeteccion
                var archivoSha = _moduloActual.FirmasDeteccion?
                    .SelectMany(f => f.Archivos ?? Enumerable.Empty<ArchivoCritico>())
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
                // 2. Fallback: primer archivo de FirmasDeteccion sin SHA256
                else
                {
                    var primerArchivo = _moduloActual.FirmasDeteccion?
                        .SelectMany(f => f.Archivos ?? Enumerable.Empty<ArchivoCritico>())
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

                // Seleccionar el archivo concreto en el explorador si existe
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
