using Microsoft.Win32;
using NX_Suite.Services;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NX_Suite
{
    /// <summary>
    /// MainWindow — Overlay visor de log de sesiones.
    /// Abre un overlay inline (PanelLogOverlay) con todas las sesiones coloreadas
    /// por nivel. Botones: copiar texto completo al portapapeles y guardar archivo.
    /// </summary>
    public partial class MainWindow
    {
        // ?? Apertura / cierre ????????????????????????????????????????????

        private void BtnLog_Click(object sender, RoutedEventArgs e)
        {
            CargarSesionesLog();
            MostrarOverlayLog();
        }

        private void BtnCerrarLog_Click(object sender, RoutedEventArgs e)
            => OcultarOverlayLog();

        private void PanelLog_BackdropClick(object sender, MouseButtonEventArgs e)
            => OcultarOverlayLog();

        // ?? Botones de acción ????????????????????????????????????????????

        private void BtnCopiarTextoLog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string texto = Logger.ObtenerTextoCompleto();
                if (!string.IsNullOrEmpty(texto))
                    Clipboard.SetText(texto);
            }
            catch { }
        }

        private void BtnLimpiarLog_Click(object sender, RoutedEventArgs e)
        {
            Logger.LimpiarLog();
            Logger.IniciarSesion();
            CargarSesionesLog();
        }

        private void BtnGuardarArchivoLog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string descargas = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                descargas = Path.Combine(descargas, "Downloads");

                var dlg = new SaveFileDialog
                {
                    Title            = "Guardar log de NX-Suite",
                    FileName         = $"NX-Suite-log-{DateTime.Now:yyyy-MM-dd}.log",
                    DefaultExt       = ".log",
                    Filter           = "Archivos de log (*.log)|*.log|Archivos de texto (*.txt)|*.txt",
                    InitialDirectory = Directory.Exists(descargas) ? descargas : string.Empty
                };

                if (dlg.ShowDialog() == true)
                    File.Copy(Core.Configuracion.ConfiguracionLocal.RutaLog, dlg.FileName, overwrite: true);
            }
            catch (Exception ex)
            {
                UI.Dialogos.Error($"No se pudo guardar el log: {ex.Message}");
            }
        }

        // ?? Carga de sesiones en el panel ????????????????????????????????

        private void CargarSesionesLog()
        {
            PanelSesionesLog.Children.Clear();

            var sesiones = Logger.ObtenerSesiones();
            if (sesiones.Count == 0)
            {
                PanelSesionesLog.Children.Add(new TextBlock
                {
                    Text       = "El log está vacío.",
                    Foreground = new SolidColorBrush(Color.FromRgb(0x70, 0x70, 0x80)),
                    FontSize   = 12,
                    Margin     = new Thickness(0, 20, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                return;
            }

            bool primera = true;
            foreach (var sesion in sesiones)
            {
                PanelSesionesLog.Children.Add(CrearBloqueSession(sesion, expandido: primera));
                primera = false;
            }
        }

        private UIElement CrearBloqueSession(SesionLog sesion, bool expandido)
        {
            // Color de cabecera: ámbar si tiene errores, cian si no
            Color colorCabecera = sesion.TieneErrores
                ? Color.FromRgb(0xFF, 0xD5, 0x4A)
                : Color.FromRgb(0x00, 0xD2, 0xFF);

            // ?? Cabecera colapsable ??????????????????????????????????????
            var txtFecha = new TextBlock
            {
                Text       = sesion.Titulo,
                FontSize   = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(colorCabecera),
                VerticalAlignment = VerticalAlignment.Center
            };

            string badgeTxt   = sesion.TieneErrores ? "?  CON ERRORES" : $"{sesion.Lineas.Count} entradas";
            Color  badgeColor = sesion.TieneErrores
                ? Color.FromArgb(200, 0xFF, 0x55, 0x55)
                : Color.FromArgb(140, 0x70, 0x70, 0x80);

            var badge = new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding      = new Thickness(6, 2, 6, 2),
                Margin       = new Thickness(10, 0, 0, 0),
                Background   = new SolidColorBrush(Color.FromArgb(30,
                    badgeColor.R, badgeColor.G, badgeColor.B)),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text       = badgeTxt,
                    FontSize   = 9,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(badgeColor)
                }
            };

            // Chevron como Path geometry — independiente de fuente instalada
            var chevronPath = new System.Windows.Shapes.Path
            {
                Data              = Geometry.Parse("M 0,0 L 6,5 L 12,0"),   // apuntando abajo = expandido
                Stroke            = new SolidColorBrush(
                    Color.FromArgb(160, colorCabecera.R, colorCabecera.G, colorCabecera.B)),
                StrokeThickness   = 1.8,
                StrokeStartLineCap= PenLineCap.Round,
                StrokeEndLineCap  = PenLineCap.Round,
                StrokeLineJoin    = PenLineJoin.Round,
                Fill              = Brushes.Transparent,
                Width             = 12,
                Height            = 5,
                Stretch           = Stretch.None,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment   = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
                RenderTransform = expandido
                    ? Transform.Identity
                    : new RotateTransform(-90, 6, 2.5)   // girado -90° = apuntando a la derecha = colapsado
            };

            // Alias para usarlo en el toggle de click
            var chevron = chevronPath;

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(txtFecha, 0);
            Grid.SetColumn(badge,    1);
            Grid.SetColumn(chevron,  2);
            headerGrid.Children.Add(txtFecha);
            headerGrid.Children.Add(badge);
            headerGrid.Children.Add(chevron);

            var header = new Border
            {
                Padding    = new Thickness(12, 8, 12, 8),
                Background = new SolidColorBrush(Color.FromArgb(20,
                    colorCabecera.R, colorCabecera.G, colorCabecera.B)),
                CornerRadius = new CornerRadius(8, 8, 0, 0),
                Cursor       = Cursors.Hand,
                Child        = headerGrid
            };

            // ?? Cuerpo de líneas ?????????????????????????????????????????
            var cuerpo = new Border
            {
                Background   = new SolidColorBrush(Color.FromArgb(12, 255, 255, 255)),
                CornerRadius = new CornerRadius(0, 0, 8, 8),
                Padding      = new Thickness(12, 8, 12, 8),
                Visibility   = expandido ? Visibility.Visible : Visibility.Collapsed
            };

            var stackLineas = new StackPanel();

            if (sesion.Lineas.Count == 0)
            {
                stackLineas.Children.Add(new TextBlock
                {
                    Text       = "Sin entradas en esta sesión.",
                    Foreground = new SolidColorBrush(Color.FromRgb(0x70, 0x70, 0x80)),
                    FontSize   = 10,
                    Margin     = new Thickness(0, 2, 0, 2)
                });
            }
            else
            {
                foreach (var linea in sesion.Lineas)
                    stackLineas.Children.Add(CrearFilaLinea(linea));
            }

            cuerpo.Child = stackLineas;

            // ?? Toggle al hacer click en cabecera ?????????????????????????
            header.MouseLeftButtonDown += (_, _) =>
            {
                bool visible = cuerpo.Visibility == Visibility.Visible;
                cuerpo.Visibility  = visible ? Visibility.Collapsed : Visibility.Visible;
                chevron.RenderTransform = visible
                    ? new RotateTransform(-90, 6, 2.5)
                    : Transform.Identity;
            };

            // ?? Bloque completo ??????????????????????????????????????????
            var bloque = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            bloque.Children.Add(header);
            bloque.Children.Add(cuerpo);
            return bloque;
        }

        private static UIElement CrearFilaLinea(LineaLog linea)
        {
            Color colorNivel = linea.Nivel switch
            {
                "OK"    or "OK   " => Color.FromRgb(0x4C, 0xAF, 0x50),
                "WARN"  or "WARN " => Color.FromRgb(0xFF, 0xD5, 0x4A),
                "ERROR"            => Color.FromRgb(0xFF, 0x55, 0x55),
                _                  => Color.FromRgb(0xA0, 0xA0, 0xB0)   // INFO / resto
            };

            var badge = new Border
            {
                CornerRadius = new CornerRadius(3),
                Padding      = new Thickness(4, 1, 4, 1),
                Margin       = new Thickness(0, 0, 8, 0),
                MinWidth     = 42,
                Background   = new SolidColorBrush(Color.FromArgb(30,
                    colorNivel.R, colorNivel.G, colorNivel.B)),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text                = linea.Nivel.Trim(),
                    FontSize            = 8,
                    FontWeight          = FontWeights.Bold,
                    Foreground          = new SolidColorBrush(colorNivel),
                    HorizontalAlignment = HorizontalAlignment.Center
                }
            };

            var txtMensaje = new TextBlock
            {
                Text         = linea.Mensaje,
                FontSize     = 10,
                Foreground   = new SolidColorBrush(colorNivel),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };

            var fila = new Grid { Margin = new Thickness(0, 1, 0, 1) };
            fila.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(badge,      0);
            Grid.SetColumn(txtMensaje, 1);
            fila.Children.Add(badge);
            fila.Children.Add(txtMensaje);
            return fila;
        }

        // ?? Animaciones ??????????????????????????????????????????????????

        private void MostrarOverlayLog()
        {
            AplicarBlurFondo(true);
            PanelLogOverlay.Visibility = Visibility.Visible;
            PanelLogOverlay.Opacity    = 0;

            var panelBorder = PanelLogContenido;
            panelBorder.RenderTransformOrigin = new Point(0.5, 0.5);
            panelBorder.RenderTransform = new ScaleTransform(0.96, 0.96);

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
            var scaleX = new DoubleAnimation(0.96, 1.0, TimeSpan.FromMilliseconds(200))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            var scaleY = new DoubleAnimation(0.96, 1.0, TimeSpan.FromMilliseconds(200))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };

            PanelLogOverlay.BeginAnimation(OpacityProperty, fadeIn);
            panelBorder.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
            panelBorder.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
        }

        private void OcultarOverlayLog()
        {
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(160));
            fadeOut.Completed += (_, _) =>
            {
                PanelLogOverlay.Visibility = Visibility.Collapsed;
                AplicarBlurFondo(false);
            };
            PanelLogOverlay.BeginAnimation(OpacityProperty, fadeOut);
        }
    }
}
