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
    /// MainWindow — Detalle de módulo: chips de versiones, disponibilidad offline, caché por versión y selección de versión.
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>
        /// Reconstruye los chips de versiones del módulo y actualiza el header de estado
        /// y los botones de acción sin reiniciar animaciones.
        /// </summary>
        private void RellenarChipsVersiones(ModuloConfig modulo)
        {
            PanelChipsVersiones.Children.Clear();
            _infoChipsVersiones.Clear();

            if (modulo.Versiones == null || modulo.Versiones.Count == 0)
            {
                PanelCabeceraVersiones.Visibility = Visibility.Collapsed;
                return;
            }

            PanelCabeceraVersiones.Visibility = Visibility.Visible;

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
            {
                var capVer = ver;

                // Separador fino entre version info y estado de Caché
                stackContent.Children.Add(new Rectangle
                {
                    Height = 1,
                    Fill   = new SolidColorBrush(Color.FromArgb(35, 255, 255, 255)),
                    Margin = new Thickness(0, 5, 0, 4)
                });

                stackContent.Children.Add(CrearFilaDisponibilidadOffline(capVer, tieneCache));
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
        /// Crea la fila resumen de disponibilidad offline de una versión: un punto de
        /// estado (disponible/no disponible), un detalle secundario (ZIP · extraído) y,
        /// si hay Caché, un botón compacto para borrarla. Reemplaza las dos filas grandes
        /// de ZIP/extraído por un solo indicador agregado de menor peso visual.
        /// </summary>
        private FrameworkElement CrearFilaDisponibilidadOffline(ModuloVersion ver, bool tieneCache)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });                     // punto
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // texto
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });                     // btn borrar

            Color colorEstado = tieneCache
                ? Color.FromArgb(255, 100, 220, 100)
                : Color.FromArgb(140, 112, 112, 128);

            var punto = new Ellipse
            {
                Width             = 7,
                Height            = 7,
                Fill              = new SolidColorBrush(colorEstado),
                Margin            = new Thickness(0, 0, 7, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(punto, 0);
            grid.Children.Add(punto);

            var infoStack = new StackPanel
            {
                Orientation       = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Center
            };
            infoStack.Children.Add(new TextBlock
            {
                Text       = tieneCache ? "Disponible offline" : "No disponible localmente",
                FontSize   = 9,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(
                    tieneCache ? Color.FromArgb(220, 200, 240, 200) : Color.FromArgb(150, 160, 160, 175))
            });

            if (tieneCache)
            {
                var partesDetalle = new List<string>();
                if (ver.TieneZipCache)      partesDetalle.Add("ZIP");
                if (ver.TieneCarpetaCache)  partesDetalle.Add("Extra\u00eddo");
                string detalle = string.Join(" \u00b7 ", partesDetalle);

                if (!string.IsNullOrEmpty(detalle))
                {
                    infoStack.Children.Add(new TextBlock
                    {
                        Text       = detalle,
                        FontSize   = 8,
                        Foreground = new SolidColorBrush(Color.FromArgb(130, 160, 160, 175))
                    });
                }
            }

            Grid.SetColumn(infoStack, 1);
            grid.Children.Add(infoStack);

            if (tieneCache)
            {
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
                    var imgDel = new Image { Width = 18, Height = 18, Stretch = Stretch.Uniform, Opacity = 0.65 };
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
                        Text       = "\u2715",
                        FontSize   = 10,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 80, 80))
                    };
                }

                var capVer = ver;
                btnDel.Click += (_, _) => EliminarCacheCompletaVersion(capVer);
                Grid.SetColumn(btnDel, 2);
                grid.Children.Add(btnDel);
            }

            var contenedor = new Border { Child = grid };
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
            string tipo = esZip ? "ZIP" : "Extraído";
            string nombreModulo = _moduloActual.Nombre;
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
                Logger.Info($"[{nombreModulo}] Caché {tipo} eliminada desde detalle ? {ruta}");
                if (_catalogoModulos != null)
                    _cerebro.ActualizarEstadoCacheCatalogo(_catalogoModulos);
                RefrescarSeccionCache(_moduloActual);
                RellenarChipsVersiones(_moduloActual);
            }
            catch (Exception ex)
            {
                Logger.Error($"[{nombreModulo}] Error al eliminar caché {tipo} desde detalle", ex);
                Dialogos.Error($"Error al eliminar caché: {ex.Message}");
            }
        }

        /// <summary>
        /// Elimina toda la Caché local (ZIP y/o carpeta extraída) de una versión concreta
        /// y refresca inmediatamente el estado visual de la tarjeta, sin necesidad de
        /// reabrir el detalle ni reescanear toda la SD.
        /// </summary>
        private void EliminarCacheCompletaVersion(ModuloVersion ver)
        {
            if (ver == null || _moduloActual == null) return;
            string nombreModulo = _moduloActual.Nombre;
            try
            {
                if (!string.IsNullOrEmpty(ver.RutaCacheZipVer) && System.IO.File.Exists(ver.RutaCacheZipVer))
                    System.IO.File.Delete(ver.RutaCacheZipVer);

                if (!string.IsNullOrEmpty(ver.RutaCacheCarpetaVer))
                {
                    if (System.IO.Directory.Exists(ver.RutaCacheCarpetaVer))
                        System.IO.Directory.Delete(ver.RutaCacheCarpetaVer, true);
                    else if (System.IO.File.Exists(ver.RutaCacheCarpetaVer))
                        System.IO.File.Delete(ver.RutaCacheCarpetaVer);
                }

                Logger.Info($"[{nombreModulo}] Caché de v{ver.Version} eliminada desde detalle");

                // Refresco puntual: recalcula el estado de Caché de todas las versiones
                // del catálogo (barato) y reconstruye solo las tarjetas de este detalle.
                if (_catalogoModulos != null)
                    _cerebro.ActualizarEstadoCacheCatalogo(_catalogoModulos);
                RefrescarSeccionCache(_moduloActual);
                RellenarChipsVersiones(_moduloActual);
            }
            catch (Exception ex)
            {
                Logger.Error($"[{nombreModulo}] Error al eliminar Caché de v{ver.Version} desde detalle", ex);
                Dialogos.Error($"Error al eliminar caché: {ex.Message}");
            }
        }

        /// <summary>
        /// Centra el grupo de capturas cuando cabe dentro del viewport, o lo alinea a la
        /// izquierda (permitiendo scroll horizontal) cuando el contenido excede el ancho
        /// disponible. Se recalcula en cada cambio de tamaño del ScrollViewer o del propio
        /// contenido (por ejemplo, tras cargar las Imágenes).
        /// </summary>
        private void ActualizarAlineacionCapturas()
        {
            if (ContenedorCapturas == null || ScrollCapturas == null) return;

            double anchoContenido = ListaScreenshots.ActualWidth;
            double anchoViewport  = ScrollCapturas.ViewportWidth > 0
                ? ScrollCapturas.ViewportWidth
                : ScrollCapturas.ActualWidth;

            bool cabe = anchoViewport <= 0 || anchoContenido <= anchoViewport;
            ContenedorCapturas.HorizontalAlignment = cabe
                ? HorizontalAlignment.Center
                : HorizontalAlignment.Left;
        }

        private void ScrollCapturas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ActualizarAlineacionCapturas();
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
    }
}
