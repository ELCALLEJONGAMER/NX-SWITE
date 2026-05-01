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
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace NX_Suite
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
        private List<(Border Chip, Rectangle Barra, Color ColorBase, bool EsSoloDeteccion, ModuloVersion Version)> _infoChipsVersiones = new();

        private void AbrirDetalleModulo(ModuloConfig modulo, bool desdeAsistido = false)
        {
            if (modulo == null) return;
            _detalleDesdeAsistido = desdeAsistido;

            _versionSeleccionadaDetalle = null;
            _infoChipsVersiones.Clear();

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
            RellenarChipsVersiones(modulo);

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
            bool haySd         = !string.IsNullOrEmpty((InfoSD.ComboDrives.SelectedItem as SDInfo)?.Letra);
            bool tieneSitioWeb = !string.IsNullOrWhiteSpace(modulo.UrlOficial);
            bool instalado     = modulo.EstaInstaladoEnSd;

            BtnSitioWebDetalle.Visibility = tieneSitioWeb ? Visibility.Visible : Visibility.Collapsed;

            var verSel = _versionSeleccionadaDetalle;

            if (verSel == null)
            {
                // Sin selección de chip: comportamiento original
                _btnActualizarEsDegradacion         = false;
                bool tieneUpdate = modulo.TieneActualizacion;
                BtnInstalarDetalle.Visibility       = instalado ? Visibility.Collapsed : Visibility.Visible;
                BtnActualizarDetalle.Visibility     = (instalado && tieneUpdate) ? Visibility.Visible : Visibility.Collapsed;
                BtnBorrarDetalle.Visibility         = instalado ? Visibility.Visible : Visibility.Collapsed;
                BtnAbrirUbicacionDetalle.Visibility = instalado ? Visibility.Visible : Visibility.Collapsed;

                BtnActualizarDetalle.Content = "ACTUALIZAR";
                if (!instalado)
                    BtnInstalarDetalle.Content = haySd ? "INSTALAR EN SD"
                        : modulo.TieneCache ? "REINSTALAR EN CACHÉ PC" : "DESCARGAR EN CACHÉ (PC)";
                return;
            }

            // ?? Con versión seleccionada: lógica inteligente ??
            if (verSel.SoloDeteccion)
            {
                // Versión bloqueada: solo se puede eliminar si es la instalada
                _btnActualizarEsDegradacion         = false;
                bool esLaInstalada = instalado &&
                    string.Equals(verSel.Version, modulo.VersionInstalada, StringComparison.OrdinalIgnoreCase);
                BtnInstalarDetalle.Visibility       = Visibility.Collapsed;
                BtnActualizarDetalle.Visibility     = Visibility.Collapsed;
                BtnBorrarDetalle.Visibility         = esLaInstalada ? Visibility.Visible : Visibility.Collapsed;
                BtnAbrirUbicacionDetalle.Visibility = esLaInstalada ? Visibility.Visible : Visibility.Collapsed;
                return;
            }

            bool esVersionInstalada = instalado &&
                string.Equals(verSel.Version, modulo.VersionInstalada, StringComparison.OrdinalIgnoreCase);

            // Índice en la lista: posición 0 = más reciente
            int idxSel = modulo.Versiones.IndexOf(verSel);
            int idxIns = instalado
                ? modulo.Versiones.FindIndex(v =>
                    string.Equals(v.Version, modulo.VersionInstalada, StringComparison.OrdinalIgnoreCase))
                : -1;

            bool esUpgrade   = instalado && !esVersionInstalada && idxSel >= 0 && idxIns >= 0 && idxSel < idxIns;
            bool esDowngrade = instalado && !esVersionInstalada && idxSel >= 0 && idxIns >= 0 && idxSel > idxIns;

            if (esVersionInstalada)
            {
                // La versión seleccionada ES la instalada en la SD
                _btnActualizarEsDegradacion         = false;
                BtnInstalarDetalle.Visibility       = Visibility.Collapsed;
                BtnActualizarDetalle.Visibility     = Visibility.Collapsed;
                BtnBorrarDetalle.Visibility         = Visibility.Visible;
                BtnAbrirUbicacionDetalle.Visibility = Visibility.Visible;
            }
            else if (esUpgrade)
            {
                // Versión seleccionada es más nueva que la instalada ? actualizar
                _btnActualizarEsDegradacion         = false;
                BtnInstalarDetalle.Visibility       = Visibility.Collapsed;
                BtnActualizarDetalle.Visibility     = Visibility.Visible;
                BtnActualizarDetalle.Content        = $"ACTUALIZAR A v{verSel.Version}";
                BtnBorrarDetalle.Visibility         = Visibility.Collapsed;
                BtnAbrirUbicacionDetalle.Visibility = Visibility.Collapsed;
            }
            else if (esDowngrade)
            {
                // Versión seleccionada es más antigua que la instalada:
                // BtnActualizarDetalle = DEGRADAR (desinstala la actual + instala la vieja)
                // BtnInstalarDetalle   = INSTALAR  (solo instala, sin eliminar la actual)
                _btnActualizarEsDegradacion         = true;
                BtnActualizarDetalle.Visibility     = Visibility.Visible;
                BtnActualizarDetalle.Content        = $"DEGRADAR A v{verSel.Version}";
                BtnInstalarDetalle.Visibility       = Visibility.Visible;
                BtnInstalarDetalle.Content          = $"INSTALAR v{verSel.Version}";
                BtnBorrarDetalle.Visibility         = Visibility.Collapsed;
                BtnAbrirUbicacionDetalle.Visibility = Visibility.Collapsed;
            }
            else
            {
                // Módulo no instalado ? instalar versión seleccionada
                _btnActualizarEsDegradacion         = false;
                BtnInstalarDetalle.Visibility       = Visibility.Visible;
                BtnInstalarDetalle.Content          = haySd
                    ? $"INSTALAR v{verSel.Version} EN SD"
                    : $"DESCARGAR v{verSel.Version}";
                BtnActualizarDetalle.Visibility     = Visibility.Collapsed;
                BtnBorrarDetalle.Visibility         = Visibility.Collapsed;
                BtnAbrirUbicacionDetalle.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Busca el módulo actual en los datos recién sincronizados y refresca
        /// el badge de estado y los botones de acción sin reiniciar animaciones.
        /// </summary>
        private void RellenarChipsVersiones(ModuloConfig modulo)
        {
            PanelChipsVersiones.Children.Clear();
            _infoChipsVersiones.Clear();

            if (modulo.Versiones == null || modulo.Versiones.Count == 0)
            {
                TxtTituloVersiones.Visibility = Visibility.Collapsed;
                return;
            }

            TxtTituloVersiones.Text       = "VERSIONES Y CACHÉ";
            TxtTituloVersiones.Visibility = Visibility.Visible;

            string versionInstalada = modulo.VersionInstalada ?? string.Empty;
            bool   parcial          = modulo.EstadoSd == EstadoSdModulo.ParcialmenteInstalado;
            bool   hayAlgoInstalado = modulo.EstaInstaladoEnSd || parcial;

            // ?? Posición 1: versión más reciente (Versiones[0]) — siempre arriba ??
            var latest = modulo.Versiones[0];
            bool latestEsInstalada = hayAlgoInstalado &&
                string.Equals(latest.Version, versionInstalada, StringComparison.OrdinalIgnoreCase);
            PanelChipsVersiones.Children.Add(
                CrearYRegistrarChip(latest, modulo, hayAlgoInstalado, parcial, versionInstalada));

            // ?? Posición 2: versión instalada (solo si es diferente de la latest) ??
            if (hayAlgoInstalado && !latestEsInstalada && !string.IsNullOrEmpty(versionInstalada))
            {
                var verInstalada = modulo.Versiones.FirstOrDefault(v =>
                    string.Equals(v.Version, versionInstalada, StringComparison.OrdinalIgnoreCase));
                if (verInstalada != null)
                    PanelChipsVersiones.Children.Add(
                        CrearYRegistrarChip(verInstalada, modulo, hayAlgoInstalado, parcial, versionInstalada));
            }

            // ?? Posiciones siguientes: resto de versiones descargables (sin latest ni instalada) ??
            const int MaxVisible = 5;
            var restantes = modulo.Versiones
                .Skip(1)
                .Where(v => !v.SoloDeteccion &&
                            !string.Equals(v.Version, versionInstalada, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var visibles = restantes.Take(MaxVisible).ToList();
            var ocultos  = restantes.Skip(MaxVisible).ToList();

            foreach (var ver in visibles)
                PanelChipsVersiones.Children.Add(CrearYRegistrarChip(ver, modulo, hayAlgoInstalado, parcial, versionInstalada));

            // ?? Botón "VER MÁS" si hay ocultos ??
            if (ocultos.Count > 0)
            {
                var btnVerMas = new Border
                {
                    CornerRadius    = new CornerRadius(7),
                    BorderThickness = new Thickness(1),
                    BorderBrush     = new SolidColorBrush(Color.FromArgb(60,  80, 80, 100)),
                    Background      = new SolidColorBrush(Color.FromArgb(10,  80, 80, 100)),
                    Padding         = new Thickness(8, 5, 8, 5),
                    Margin          = new Thickness(0, 0, 0, 5),
                    Cursor          = Cursors.Hand,
                    Child = new TextBlock
                    {
                        Text                = $"VER {ocultos.Count} M\u00c1S...",
                        FontSize            = 10,
                        FontWeight          = FontWeights.SemiBold,
                        Foreground          = new SolidColorBrush(Color.FromArgb(160, 160, 160, 180)),
                        HorizontalAlignment = HorizontalAlignment.Center
                    }
                };

                var capOcultos = ocultos;
                btnVerMas.MouseLeftButtonDown += (_, _) =>
                {
                    PanelChipsVersiones.Children.Remove(btnVerMas);
                    foreach (var ver in capOcultos)
                        PanelChipsVersiones.Children.Add(
                            CrearYRegistrarChip(ver, modulo, hayAlgoInstalado, parcial, versionInstalada));
                };

                PanelChipsVersiones.Children.Add(btnVerMas);
            }
        }

        private UIElement CrearYRegistrarChip(ModuloVersion ver, ModuloConfig modulo,
            bool hayAlgoInstalado, bool parcial, string versionInstalada)
        {
            bool esInstalada     = hayAlgoInstalado &&
                string.Equals(ver.Version, versionInstalada, StringComparison.OrdinalIgnoreCase);
            bool esLatest        = modulo.Versiones.Count > 0 &&
                                   ReferenceEquals(modulo.Versiones[0], ver);
            bool esSoloDeteccion = ver.SoloDeteccion;
            bool esUpdateTarget  = esLatest && modulo.TieneActualizacion;

            // ?? Colores según estado ??
            Color borderColor;
            Color badgeColor = Color.FromArgb(0, 0, 0, 0);
            string? badgeText = null;

            if (esSoloDeteccion)
            {
                borderColor = Color.FromArgb(70,  112, 112, 128);
                badgeText   = "BLOQUEADO";
                badgeColor  = Color.FromArgb(200, 112, 112, 128);
            }
            else if (esInstalada && parcial)
            {
                borderColor = Color.FromArgb(200, 245, 158,  11);
                badgeText   = "REINSTALAR";
                badgeColor  = Color.FromArgb(255, 245, 158,  11);
            }
            else if (esInstalada)
            {
                borderColor = Color.FromArgb(200,  64, 192,  87);
                badgeText   = "INSTALADO";
                badgeColor  = Color.FromArgb(255,  64, 192,  87);
            }
            else if (esUpdateTarget)
            {
                borderColor = Color.FromArgb(200, 245, 158,  11);
                badgeText   = "ACTUALIZAR";
                badgeColor  = Color.FromArgb(255, 245, 158,  11);
            }
            else if (esLatest)
            {
                borderColor = Color.FromArgb(80,  245, 158,  11);
                badgeColor  = Colors.Transparent;
            }
            else
            {
                borderColor = Color.FromArgb(80,   42,  42,  53);
                badgeColor  = Colors.Transparent;
            }

            // ?? Barra indicadora lateral (oculta hasta que el chip sea seleccionado) ??
            var barra = new Rectangle
            {
                Width             = 5,
                RadiusX           = 2.5,
                RadiusY           = 2.5,
                Fill              = new SolidColorBrush(Colors.Transparent),
                VerticalAlignment = VerticalAlignment.Stretch,
                Margin            = new Thickness(0, 0, 8, 0)
            };

            // ?? Texto de versión + badge ??
            var fila = new Grid();
            fila.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var txtVer = new TextBlock
            {
                Text              = $"v{ver.Version}",
                FontSize          = 11,
                FontWeight        = FontWeights.Bold,
                Foreground        = new SolidColorBrush(esSoloDeteccion
                    ? Color.FromArgb(130, 255, 255, 255)
                    : Colors.White),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(txtVer, 0);
            fila.Children.Add(txtVer);

            if (!string.IsNullOrEmpty(badgeText))
            {
                var badge = new Border
                {
                    CornerRadius        = new CornerRadius(4),
                    Padding             = new Thickness(4, 2, 4, 2),
                    Margin              = new Thickness(6, 0, 0, 0),
                    Background          = new SolidColorBrush(
                        Color.FromArgb(35, badgeColor.R, badgeColor.G, badgeColor.B)),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment   = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text       = badgeText,
                        FontSize   = 8,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(badgeColor)
                    }
                };
                Grid.SetColumn(badge, 1);
                fila.Children.Add(badge);
            }

            // ?? Layout interno: [barra | stackContent(fila + fila de caché opcional)] ??
            var stackContent = new StackPanel();
            stackContent.Children.Add(fila);

            bool tieneCache = ver.TieneZipCache || ver.TieneCarpetaCache;
            if (tieneCache)
            {
                var capVer = ver;

                // Separador fino entre version info y filas de caché
                stackContent.Children.Add(new Rectangle
                {
                    Height = 1,
                    Fill   = new SolidColorBrush(Color.FromArgb(35, 255, 255, 255)),
                    Margin = new Thickness(0, 5, 0, 4)
                });

                string iconoZip     = Core.Configuracion.ConfiguracionRemota.Ui?.IconoZipUrl     ?? string.Empty;
                string iconoCarpeta = Core.Configuracion.ConfiguracionRemota.Ui?.IconoCacheUrl   ?? string.Empty;

                if (ver.TieneZipCache)
                {
                    string tam = ObtenerTamanoSincrono(ver.RutaCacheZipVer, esZip: true);
                    stackContent.Children.Add(CrearItemCacheEmbebido(
                        "ZIP", iconoZip,
                        Color.FromArgb(240, 255, 175,  30),
                        Color.FromArgb( 28, 255, 165,   0),
                        tam,
                        new Thickness(0, 0, 0, 0),
                        () => EliminarCacheVersion(capVer.RutaCacheZipVer, esZip: true)));
                }

                if (ver.TieneCarpetaCache)
                {
                    string tam    = ObtenerTamanoSincrono(ver.RutaCacheCarpetaVer, esZip: false);
                    var    margen = ver.TieneZipCache ? new Thickness(0, 3, 0, 0) : new Thickness(0);
                    stackContent.Children.Add(CrearItemCacheEmbebido(
                        "Extraído", iconoCarpeta,
                        Color.FromArgb(240, 100, 220, 100),
                        Color.FromArgb( 28, 100, 220, 100),
                        tam,
                        margen,
                        () => EliminarCacheVersion(capVer.RutaCacheCarpetaVer, esZip: false)));
                }
            }

            var chipGrid = new Grid();
            chipGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            chipGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            chipGrid.Children.Add(barra);
            Grid.SetColumn(stackContent, 1);
            chipGrid.Children.Add(stackContent);

            var chip = new Border
            {
                CornerRadius    = new CornerRadius(7),
                BorderThickness = new Thickness(1.5),
                BorderBrush     = new SolidColorBrush(borderColor),
                Background      = new SolidColorBrush(
                    Color.FromArgb(18, borderColor.R, borderColor.G, borderColor.B)),
                Padding         = new Thickness(8, 5, 8, 5),
                Margin          = new Thickness(0, 0, 0, 5),
                ClipToBounds    = true,
                Cursor          = esSoloDeteccion ? Cursors.Arrow : Cursors.Hand,
                Child           = chipGrid
            };

            _infoChipsVersiones.Add((chip, barra, borderColor, esSoloDeteccion, ver));

            if (!esSoloDeteccion)
            {
                var capturedVer = ver;
                chip.MouseLeftButtonDown += (_, _) => SeleccionarVersionChip(capturedVer);
            }

            return chip;
        }

        /// <summary>
        /// Calcula el tamaño de un archivo o carpeta de caché de forma síncrona.
        /// </summary>
        private static string ObtenerTamanoSincrono(string ruta, bool esZip)
        {
            try
            {
                if (esZip && System.IO.File.Exists(ruta))
                    return FormatBytes(new System.IO.FileInfo(ruta).Length);
                if (!esZip)
                {
                    if (System.IO.Directory.Exists(ruta))
                    {
                        long total = new System.IO.DirectoryInfo(ruta)
                            .GetFiles("*", System.IO.SearchOption.AllDirectories)
                            .Sum(f => f.Length);
                        return FormatBytes(total);
                    }
                    if (System.IO.File.Exists(ruta))
                        return FormatBytes(new System.IO.FileInfo(ruta).Length);
                }
            }
            catch { }
            return string.Empty;
        }

        /// <summary>
        /// Crea una fila de caché embebida en el chip: icono + tipo + tamaño + botón borrar.
        /// </summary>
        private FrameworkElement CrearItemCacheEmbebido(
            string titulo, string iconUrl, Color colorTexto, Color colorFondo,
            string tamano, Thickness margen, Action onDelete)
        {
            // Contenedor con fondo semitransparente del color del tipo de caché
            var contenedor = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background   = new SolidColorBrush(colorFondo),
                Padding      = new Thickness(7, 5, 6, 5),
                Margin       = margen
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });                  // icono
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // info
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });                  // btn borrar

            // ?? Icono (é zip o carpeta) ??
            var icono = new Image
            {
                Width             = 18,
                Height            = 18,
                Stretch           = Stretch.Uniform,
                Opacity           = 0.90,
                Margin            = new Thickness(0, 0, 7, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            if (!string.IsNullOrEmpty(iconUrl))
            {
                try
                {
                    string? rutaLocal = Core.Servicios.Iconos.ObtenerRutaLocal(iconUrl);
                    icono.Source = new BitmapImage(new Uri(rutaLocal ?? iconUrl));
                }
                catch { }
            }
            Grid.SetColumn(icono, 0);
            grid.Children.Add(icono);

            // ?? Panel de texto: nombre + tamaño ??
            var infoStack = new StackPanel
            {
                Orientation       = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Center
            };
            infoStack.Children.Add(new TextBlock
            {
                Text       = titulo,
                FontSize   = 9,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(colorTexto)
            });
            if (!string.IsNullOrEmpty(tamano))
            {
                infoStack.Children.Add(new TextBlock
                {
                    Text       = tamano,
                    FontSize   = 8,
                    Foreground = new SolidColorBrush(
                        Color.FromArgb(180, colorTexto.R, colorTexto.G, colorTexto.B))
                });
            }
            Grid.SetColumn(infoStack, 1);
            grid.Children.Add(infoStack);

            // ?? Botón borrar con icono de eliminar ??
            var btnDel = new Button
            {
                Background        = Brushes.Transparent,
                BorderThickness   = new Thickness(0),
                Cursor            = Cursors.Hand,
                Padding           = new Thickness(4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            string iconoElimUrl = Core.Configuracion.ConfiguracionRemota.Ui?.IconoEliminarUrl ?? string.Empty;
            if (!string.IsNullOrEmpty(iconoElimUrl))
            {
                var imgDel = new Image { Width = 22, Height = 22, Stretch = Stretch.Uniform, Opacity = 0.75 };
                try
                {
                    string? rutaLocal = Core.Servicios.Iconos.ObtenerRutaLocal(iconoElimUrl);
                    imgDel.Source = new BitmapImage(new Uri(rutaLocal ?? iconoElimUrl));
                }
                catch { }
                btnDel.Content = imgDel;
            }
            else
            {
                btnDel.Content = new TextBlock
                {
                    Text       = "?",
                    FontSize   = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 80, 80))
                };
            }

            btnDel.Click += (_, _) => onDelete();
            Grid.SetColumn(btnDel, 2);
            grid.Children.Add(btnDel);

            contenedor.Child = grid;
            // Detener la propagación del clic hacia el chip de versión sin bloquear el Click del botón
            contenedor.MouseLeftButtonDown += (_, ev) => ev.Handled = true;
            return contenedor;
        }

        /// <summary>
        /// Elimina el archivo o carpeta de caché indicado y refresca la vista.
        /// </summary>
        private void EliminarCacheVersion(string ruta, bool esZip)
        {
            if (string.IsNullOrEmpty(ruta) || _moduloActual == null) return;
            try
            {
                if (esZip)
                {
                    if (System.IO.File.Exists(ruta)) System.IO.File.Delete(ruta);
                }
                else
                {
                    if (System.IO.Directory.Exists(ruta)) System.IO.Directory.Delete(ruta, true);
                    else if (System.IO.File.Exists(ruta)) System.IO.File.Delete(ruta);
                }
                if (_catalogoModulos != null)
                    _cerebro.ActualizarEstadoCacheCatalogo(_catalogoModulos);
                RefrescarSeccionCache(_moduloActual);
                RellenarChipsVersiones(_moduloActual);
            }
            catch (Exception ex)
            {
                Dialogos.Error($"Error al eliminar caché: {ex.Message}");
            }
        }

        private void SeleccionarVersionChip(ModuloVersion ver)
        {
            _versionSeleccionadaDetalle = ver;

            // Actualizar visual de todos los chips: barra + borde + fondo
            foreach (var (chip, barra, colorBase, esSoloDeteccion, chipVer) in _infoChipsVersiones)
            {
                bool seleccionado = ReferenceEquals(chipVer, ver);
                if (seleccionado)
                {
                    chip.BorderThickness = new Thickness(2);
                    chip.BorderBrush     = new SolidColorBrush(
                        Color.FromArgb(255, colorBase.R, colorBase.G, colorBase.B));
                    chip.Background      = new SolidColorBrush(
                        Color.FromArgb(45, colorBase.R, colorBase.G, colorBase.B));
                    barra.Fill           = new SolidColorBrush(
                        Color.FromArgb(255, colorBase.R, colorBase.G, colorBase.B));
                }
                else
                {
                    byte alpha = esSoloDeteccion ? (byte)70 : (byte)55;
                    chip.BorderThickness = new Thickness(1.5);
                    chip.BorderBrush     = new SolidColorBrush(
                        Color.FromArgb(alpha, colorBase.R, colorBase.G, colorBase.B));
                    chip.Background      = new SolidColorBrush(
                        Color.FromArgb(10, colorBase.R, colorBase.G, colorBase.B));
                    barra.Fill           = new SolidColorBrush(Colors.Transparent);
                }
            }

            // Actualizar texto de versión en el banner
            TxtVersionDetalle.Text = $"v{ver.Version}";

            // Refrescar panel de caché para la versión seleccionada
            if (_moduloActual != null)
                RefrescarSeccionCache(_moduloActual);

            // Refrescar botones con la nueva versión seleccionada
            if (_moduloActual != null)
                ActualizarBotonesDetalle(_moduloActual);
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
            RellenarChipsVersiones(refrescado);
        }

        private void RefrescarSeccionCache(ModuloConfig modulo)
        {
            if (modulo == null) return;

            // Versión de referencia: seleccionada > instalada > latest
            ModuloVersion? verRef = _versionSeleccionadaDetalle;
            if (verRef == null && !string.IsNullOrEmpty(modulo.VersionInstalada) &&
                modulo.VersionInstalada is not ("No detectado" or "No instalado"))
            {
                verRef = modulo.Versiones?.FirstOrDefault(v =>
                    string.Equals(v.Version, modulo.VersionInstalada, StringComparison.OrdinalIgnoreCase));
            }
            verRef ??= modulo.Versiones?.FirstOrDefault();

            // Actualizar rutas del módulo para que cualquier referencia externa funcione
            modulo.RutaCacheZip     = verRef?.RutaCacheZipVer     ?? string.Empty;
            modulo.RutaCacheCarpeta = verRef?.RutaCacheCarpetaVer ?? string.Empty;

            // Las filas de ZIP/Carpeta del panel ya no se usan — el cache está embebido en los chips
            FilaCacheZip.Visibility     = Visibility.Collapsed;
            FilaCacheCarpeta.Visibility = Visibility.Collapsed;

            // El panel se muestra solo si hay versiones (para los chips)
            bool tieneVersiones = modulo.Versiones?.Count > 0;
            PanelCacheDetalle.Visibility = tieneVersiones ? Visibility.Visible : Visibility.Collapsed;
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
                RellenarChipsVersiones(_moduloActual);
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
                RellenarChipsVersiones(_moduloActual);
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
                        _cerebro.RefrescarEstadosSinRed(_catalogoModulos, letraSD);

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
                        _cerebro.RefrescarEstadosSinRed(_catalogoModulos, letraSD);

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
