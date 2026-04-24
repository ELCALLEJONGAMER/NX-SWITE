using Microsoft.Win32;
using NX_Suite.Core;
using NX_Suite.Hardware;
using NX_Suite.Models;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SharpImage = SixLabors.ImageSharp.Image;
using WpfColor  = System.Windows.Media.Color;

namespace NX_Suite.UI
{
    public partial class VentanaPersonalizacion : Window
    {
        // ?? Definicion de assets Hekate ????????????????????????????????????
        private record AssetInfo(string Key, string NombreArchivo, string SubPath, int Ancho, int Alto);

        private static readonly AssetInfo[] Assets =
        {
            new("bootlogo",   "bootlogo.bmp",   "bootloader",      720,  1280),
            new("background", "background.bmp", "bootloader/res",  1280, 720),
            new("emummc",     "emummc.bmp",     "bootloader/res",  256,  256),
            new("stock",      "stock.bmp",      "bootloader/res",  256,  256),
            new("sysnand",    "sysnand.bmp",    "bootloader/res",  256,  256),
        };

        // ?? Estado: key -> ruta de origen ?????????????????????????????????
        private readonly Dictionary<string, string> _imagenes = new();

        // ?? SD seleccionada ???????????????????????????????????????????????
        private string? _letraSD;

        // ?? Encoder BMP 32-bit ????????????????????????????????????????????
        private static readonly BmpEncoder Bmp32 = new()
        {
            BitsPerPixel = BmpBitsPerPixel.Pixel32
        };

        // ?? Slots: key -> controles XAML ??????????????????????????????????
        private Dictionary<string, (Border slot, StackPanel empty,
            System.Windows.Controls.Image preview,
            TextBlock estado, Button quitar)> _slots = null!;

        // ?? Estado NYX colores ?????????????????????????????????????????????
        private int    _nyxThemecolor = 0;
        private string _hexAcento     = "#FFFFFF";
        private string _iniValueFondo  = "0b0b0b";  // valor exacto para nyx.ini

        // ?? Chips de preset seleccionados ????????????????????????????????
        private Border? _chipAcentoActivo;
        private Border? _chipFondoActivo;

        public VentanaPersonalizacion()
        {
            InitializeComponent();
            InicializarSlots();
            CargarUnidadesSD();
            CargarPresetsAcento();
            CargarPresetsFondo();
        }

        /// <summary>Genera los chips de themecolor desde ConfiguracionUI y selecciona el primero.</summary>
        private void CargarPresetsAcento()
        {
            PanelPresetsAcento.Children.Clear();
            var presets = UIConfigService.NyxColors.Themecolors;
            foreach (var p in presets)
            {
                var chip = CrearChipPreset(p.HexRgb, $"{p.Nombre}  (themecolor={p.Valor})");
                chip.Tag = p;
                chip.MouseLeftButtonDown += ChipAcento_Click;
                PanelPresetsAcento.Children.Add(chip);
            }
            // Seleccionar el primero por defecto
            if (PanelPresetsAcento.Children.Count > 0 &&
                PanelPresetsAcento.Children[0] is Border primero)
                ChipAcento_Click(primero, null!);
        }

        /// <summary>Genera los chips de themebg desde ConfiguracionUI y selecciona el primero.</summary>
        private void CargarPresetsFondo()
        {
            PanelPresetsFondo.Children.Clear();
            var presets = UIConfigService.NyxColors.Themebgs;
            foreach (var p in presets)
            {
                var chip = CrearChipPreset(p.HexRgb, $"{p.Nombre}  (themebg={p.IniValue})");
                chip.Tag = p;
                chip.MouseLeftButtonDown += ChipFondo_Click;
                PanelPresetsFondo.Children.Add(chip);
            }
            if (PanelPresetsFondo.Children.Count > 0 &&
                PanelPresetsFondo.Children[0] is Border primero)
                ChipFondo_Click(primero, null!);
        }

        private static Border CrearChipPreset(string hexRgb, string tooltip)
        {
            var brush = new SolidColorBrush(
                (WpfColor)ColorConverter.ConvertFromString(hexRgb));
            return new Border
            {
                Width           = 30,
                Height          = 30,
                CornerRadius    = new CornerRadius(7),
                Background      = brush,
                BorderBrush     = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(2),
                Cursor          = System.Windows.Input.Cursors.Hand,
                Margin          = new Thickness(0, 0, 5, 5),
                ToolTip         = tooltip,
            };
        }

        private void ChipAcento_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is not Border chip || chip.Tag is not NyxColorPreset p) return;

            // Deseleccionar chip anterior
            if (_chipAcentoActivo is not null)
                _chipAcentoActivo.BorderBrush = System.Windows.Media.Brushes.Transparent;

            _chipAcentoActivo = chip;
            chip.BorderBrush  = System.Windows.Media.Brushes.White;

            _nyxThemecolor = p.Valor;
            _hexAcento     = p.HexRgb;

            AplicarColorAMuestra(MuestraAcento, p.HexRgb);
            if (TxtNombreAcento       is not null) TxtNombreAcento.Text       = p.Nombre;
            if (TxtPreviewThemecolor  is not null) TxtPreviewThemecolor.Text  = $"themecolor={p.Valor}";
            if (TxtPreviewThemecolorIni is not null) TxtPreviewThemecolorIni.Text = $"themecolor={p.Valor}";
        }

        private void ChipFondo_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is not Border chip || chip.Tag is not NyxFondoPreset p) return;

            if (_chipFondoActivo is not null)
                _chipFondoActivo.BorderBrush = System.Windows.Media.Brushes.Transparent;

            _chipFondoActivo = chip;
            chip.BorderBrush = System.Windows.Media.Brushes.White;

            _iniValueFondo = p.IniValue;

            AplicarColorAMuestra(MuestraFondo, p.HexRgb);
            if (TxtNombreFondo        is not null) TxtNombreFondo.Text        = p.Nombre;
            if (TxtPreviewThemebg     is not null) TxtPreviewThemebg.Text     = $"themebg={p.IniValue}";
            if (TxtPreviewThemebgIni  is not null) TxtPreviewThemebgIni.Text  = $"themebg={p.IniValue}";
        }

        private void InicializarSlots()
        {
            _slots = new()
            {
                ["bootlogo"]   = (SlotBootlogo,   EmptyBootlogo,   PreviewBootlogo,   EstadoBootlogo,   QuitarBootlogo),
                ["background"] = (SlotBackground, EmptyBackground, PreviewBackground, EstadoBackground, QuitarBackground),
                ["emummc"]     = (SlotEmummc,     EmptyEmummc,     PreviewEmummc,     EstadoEmummc,     QuitarEmummc),
                ["stock"]      = (SlotStock,      EmptyStock,      PreviewStock,      EstadoStock,      QuitarStock),
                ["sysnand"]    = (SlotSysnand,    EmptySysnand,    PreviewSysnand,    EstadoSysnand,    QuitarSysnand),
            };
        }

        // ?? Top bar ???????????????????????????????????????????????????????
        private void TopBar_Drag(object sender, MouseButtonEventArgs e) => DragMove();
        private void BtnCerrar_Click(object sender, RoutedEventArgs e)  => Close();

        // ?? Unidades SD ???????????????????????????????????????????????????
        private void CargarUnidadesSD()
        {
            var disk     = new DiskMaster();
            var unidades = disk.ObtenerUnidadesRemovibles();
            ComboSD.ItemsSource        = unidades;
            ComboSD.DisplayMemberPath  = "FullName";
            if (unidades.Count > 0)
                ComboSD.SelectedIndex = 0;
        }

        private void ComboSD_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _letraSD = (ComboSD.SelectedItem as SDInfo)?.Letra; // ya viene como "H:\\"
            ActualizarBotonesSD();
        }

        // ?? Drag & Drop por slot ??????????????????????????????????????????
        private void Slot_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy : DragDropEffects.None;
            if (sender is Border b) b.BorderBrush = System.Windows.Media.Brushes.CornflowerBlue;
            e.Handled = true;
        }

        private void Slot_DragLeave(object sender, DragEventArgs e)
        {
            if (sender is Border b) b.BorderBrush = System.Windows.Media.Brushes.Transparent;
        }

        private void Slot_Drop(object sender, DragEventArgs e)
        {
            if (sender is not Border b) return;
            b.BorderBrush = System.Windows.Media.Brushes.Transparent;
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var archivos = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (archivos?.Length > 0 && b.Tag is string key)
                CargarImagen(key, archivos[0]);
        }

        private void Slot_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border b || b.Tag is not string key) return;
            var dlg = new OpenFileDialog
            {
                Title  = "Seleccionar imagen",
                Filter = "Imagenes|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp;*.tiff"
            };
            if (dlg.ShowDialog() == true) CargarImagen(key, dlg.FileName);
        }

        // ?? Cargar imagen en slot ?????????????????????????????????????????
        private void CargarImagen(string key, string ruta)
        {
            if (!_slots.TryGetValue(key, out var c)) return;
            var asset = Assets.First(a => a.Key == key);

            try
            {
                using var img = SharpImage.Load(ruta);
                bool ok = img.Width == asset.Ancho && img.Height == asset.Alto;

                _imagenes[key] = ruta;

                // Preview
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource   = new Uri(ruta);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                c.preview.Source      = bmp;
                c.empty.Visibility    = Visibility.Collapsed;
                c.preview.Visibility  = Visibility.Visible;
                c.quitar.Visibility   = Visibility.Visible;

                string dimOrig = $"{img.Width}x{img.Height}";
                c.estado.Text       = ok
                    ? $"{dimOrig} ?"
                    : $"{dimOrig} ? se reescalara a {asset.Ancho}x{asset.Alto}";
                c.estado.Foreground = ok
                    ? System.Windows.Media.Brushes.LimeGreen
                    : System.Windows.Media.Brushes.Gold;

                ActualizarResumen();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo cargar la imagen:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ?? Quitar imagen de slot ?????????????????????????????????????????
        private void BtnQuitar_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string key) return;
            if (!_slots.TryGetValue(key, out var c)) return;

            _imagenes.Remove(key);
            c.preview.Source     = null;
            c.empty.Visibility   = Visibility.Visible;
            c.preview.Visibility = Visibility.Collapsed;
            c.quitar.Visibility  = Visibility.Collapsed;
            c.estado.Text        = "Sin imagen";
            c.estado.Foreground  = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(64, 64, 80));

            ActualizarResumen();
        }

        // ?? Resumen y estado del boton copiar ?????????????????????????????
        private void ActualizarResumen()
        {
            int total = _imagenes.Count;
            TxtResumen.Text     = total == 0
                ? "Carga al menos una imagen para copiar a la SD"
                : $"{total} imagen(es) lista(s) para copiar a la SD";
            BtnCopiarSD.IsEnabled = total > 0 && !string.IsNullOrEmpty(_letraSD);
            TxtLog.Visibility     = Visibility.Collapsed;
        }

        private void ActualizarBotonesSD()
        {
            BtnCopiarSD.IsEnabled = _imagenes.Count > 0 && !string.IsNullOrEmpty(_letraSD);
        }

        // ?? Preparacion de imagen segun tipo de asset ?????????????????????
        /// <summary>
        /// Aplica la transformacion correcta segun el asset:
        /// - Bootlogo (720x1280 portrait): si la fuente es landscape, rota 90° CW
        ///   y luego ajusta con padding negro para no distorsionar el contenido.
        /// - Resto de assets: resize con padding para mantener proporcion.
        /// </summary>
        private static void AdaptarImagenParaAsset(SixLabors.ImageSharp.Image img, AssetInfo asset)
        {
            bool esBootlogo = asset.Key == "bootlogo";

            if (esBootlogo)
            {
                // Si la imagen fuente es apaisada (landscape), rotar 90° CW
                // para que el contenido quede orientado correctamente en el boot.
                if (img.Width > img.Height)
                    img.Mutate(x => x.Rotate(RotateMode.Rotate270));
            }

            // Redimensionar manteniendo proporcion + padding negro
            if (img.Width != asset.Ancho || img.Height != asset.Alto)
            {
                img.Mutate(x => x.Resize(new SixLabors.ImageSharp.Processing.ResizeOptions
                {
                    Size    = new SixLabors.ImageSharp.Size(asset.Ancho, asset.Alto),
                    Mode    = SixLabors.ImageSharp.Processing.ResizeMode.Pad,
                    Sampler = SixLabors.ImageSharp.Processing.KnownResamplers.Lanczos3,
                    PadColor = SixLabors.ImageSharp.Color.Black,
                }));
            }
        }

        // ?? Copiar a SD ???????????????????????????????????????????????????
        private async void BtnCopiarSD_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_letraSD))
            {
                MessageBox.Show("Selecciona una unidad SD primero.", "Sin SD",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var errores  = new List<string>();
            var copiados = new List<string>();

            foreach (var (key, ruta) in _imagenes)
            {
                var asset = Assets.First(a => a.Key == key);
                try
                {
                    using var img = SharpImage.Load<Rgba32>(ruta);
                    AdaptarImagenParaAsset(img, asset);

                    string dirDestino = Path.Combine(
                        _letraSD!,
                        asset.SubPath.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(dirDestino);

                    string destino = Path.Combine(dirDestino, asset.NombreArchivo);
                    using var stream = File.OpenWrite(destino);
                    img.Save(stream, Bmp32);
                    copiados.Add($"  {asset.NombreArchivo}");
                }
                catch (Exception ex)
                {
                    errores.Add($"  {asset.NombreArchivo}: {ex.Message}");
                }
            }

            await EscribirNyxIniAsync(copiados, errores);

            var sb = new System.Text.StringBuilder();
            if (copiados.Count > 0)
                sb.AppendLine($"Copiados ({copiados.Count}): " + string.Join(", ", copiados.Select(c => c.Trim())));
            if (errores.Count > 0)
                sb.AppendLine("Errores: " + string.Join(", ", errores.Select(c => c.Trim())));

            TxtLog.Text       = sb.ToString().Trim();
            TxtLog.Foreground = errores.Count > 0
                ? System.Windows.Media.Brushes.Gold
                : System.Windows.Media.Brushes.LimeGreen;
            TxtLog.Visibility = Visibility.Visible;
        }

        // ?? NYX ini ???????????????????????????????????????????????????????
        /// <summary>
        /// Escribe o actualiza themecolor y themebg en /bootloader/nyx.ini.
        /// themecolor ? RGB565 en hexadecimal sin prefijo.
        /// themebg    ? RGB888 en hexadecimal sin prefijo (6 dígitos).
        /// </summary>
        private async Task EscribirNyxIniAsync(List<string> copiados, List<string> errores)
        {
            if (string.IsNullOrEmpty(_letraSD)) return;
            try
            {
                string dirBoot = Path.Combine(_letraSD!, "bootloader");
                Directory.CreateDirectory(dirBoot);
                string rutaIni = Path.Combine(dirBoot, "nyx.ini");

                var ini = new Core.HekateIniManager(rutaIni);
                await ini.LoadAsync();

                ini.SetValue("config", "themecolor", _nyxThemecolor.ToString());
                ini.SetValue("config", "themebg",    _iniValueFondo);

                await ini.SaveAsync();
                copiados.Add("nyx.ini");
            }
            catch (Exception ex)
            {
                errores.Add($"nyx.ini: {ex.Message}");
            }
        }

        // Tabla de referencia NYX: (grados HSV ? valor themecolor)
        // Puntos calibrados desde la UI real de NYX.
        private static readonly (double Hue, int Nyx)[] NyxHueTable =
        {
            (  0.0,   2),  // Rojo
            ( 30.0,  23),  // Naranja
            ( 45.0,  33),  // Naranja amarillo
            ( 60.0,  54),  // Amarillo
            (120.0, 124),  // Verde
            (240.0, 231),  // Azul
            (270.0, 261),  // Morado
            (285.0, 280),  // Purpura
            (300.0, 291),  // Rosa
            (340.0, 341),  // Rosa rojizo
            (360.0, 359),  // Rojo (fin del circulo)
        };

        /// <summary>
        /// #RRGGBB ? themecolor NYX (0-359).
        /// Usa interpolacion lineal por tramos sobre puntos calibrados de la UI de NYX.
        /// Colores acromaticos (baja saturacion) ? 0 (blanco NYX).
        /// </summary>
        private static string HexToThemecolor(string hex)
        {
            var (r, g, b) = ParseHex(hex);
            double maxC  = Math.Max(r, Math.Max(g, b));
            double minC  = Math.Min(r, Math.Min(g, b));
            if ((maxC - minC) / 255.0 < 0.10) return "0"; // acromatico ? blanco NYX
            double hue = RgbToHue(r, g, b);
            return HsvHueToNyx(hue).ToString();
        }

        /// <summary>Interpola el hue HSV (0-360) al valor themecolor de NYX usando la tabla calibrada.</summary>
        private static int HsvHueToNyx(double hue)
        {
            hue = ((hue % 360) + 360) % 360;
            var pts = NyxHueTable;
            if (hue <= pts[0].Hue)   return pts[0].Nyx;
            if (hue >= pts[^1].Hue)  return pts[^1].Nyx;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                if (hue >= pts[i].Hue && hue < pts[i + 1].Hue)
                {
                    double t = (hue - pts[i].Hue) / (pts[i + 1].Hue - pts[i].Hue);
                    return (int)Math.Round(pts[i].Nyx + t * (pts[i + 1].Nyx - pts[i].Nyx));
                }
            }
            return pts[^1].Nyx;
        }

        /// <summary>
        /// Convierte un valor NYX themecolor (0-359) al color de visualizacion correspondiente.
        /// NYX=0 ? blanco. El resto usa interpolacion inversa de la tabla.
        /// </summary>
        private static (byte R, byte G, byte B) NyxToDisplayColor(int nyx)
        {
            if (nyx <= 0) return (255, 255, 255); // blanco NYX
            double hue = NyxToHsvHue(nyx);
            return HsvToRgb(hue, 1.0, 1.0);
        }

        /// <summary>Interpolacion inversa: valor NYX (0-359) ? grados HSV (0-360).</summary>
        private static double NyxToHsvHue(int nyx)
        {
            var pts = NyxHueTable;
            if (nyx <= pts[0].Nyx)  return pts[0].Hue;
            if (nyx >= pts[^1].Nyx) return pts[^1].Hue;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                if (nyx >= pts[i].Nyx && nyx < pts[i + 1].Nyx)
                {
                    double t = (double)(nyx - pts[i].Nyx) / (pts[i + 1].Nyx - pts[i].Nyx);
                    return pts[i].Hue + t * (pts[i + 1].Hue - pts[i].Hue);
                }
            }
            return pts[^1].Hue;
        }

        /// <summary>Extrae el Hue (0-360) de un color RGB. Retorna 0 para colores acromáticos (blanco/gris/negro).</summary>
        private static double RgbToHue(byte r, byte g, byte b)
        {
            double rf = r / 255.0, gf = g / 255.0, bf = b / 255.0;
            double max   = Math.Max(rf, Math.Max(gf, bf));
            double min   = Math.Min(rf, Math.Min(gf, bf));
            double delta = max - min;
            if (delta < 1e-10) return 0; // acromatico ? blanco en NYX
            double h;
            if      (max == rf) h = 60 * (((gf - bf) / delta + 6) % 6); // +6 evita modulo negativo en C#
            else if (max == gf) h = 60 * (((bf - rf) / delta) + 2);
            else                h = 60 * (((rf - gf) / delta) + 4);
            return h;
        }

        /// <summary>
        /// #RRGGBB ? NYX themebg.  Cada byte del hex ES el valor NYX decimal directamente.
        /// NYX rango: 11 (0x0B = negro) a 100 (0x64 = gris brillante).
        /// Ejemplo: #640B0B ? rojo seco | #0B640B ? verde hoja | #0B0B64 ? azul
        /// </summary>
        private static string HexToThemebg(string hex)
        {
            var (r, g, b) = ParseHex(hex);
            // El byte del hex = valor NYX decimal. Clamp a rango válido 11-100.
            int rn = Math.Max(11, Math.Min(100, (int)r));
            int gn = Math.Max(11, Math.Min(100, (int)g));
            int bn = Math.Max(11, Math.Min(100, (int)b));
            return $"{rn:x2}{gn:x2}{bn:x2}";
        }

        private static (byte r, byte g, byte b) ParseHex(string hex)
        {
            hex = hex.TrimStart('#');
            if (hex.Length != 6) return (0, 0, 0);
            byte r = Convert.ToByte(hex[0..2], 16);
            byte g = Convert.ToByte(hex[2..4], 16);
            byte b = Convert.ToByte(hex[4..6], 16);
            return (r, g, b);
        }

        // ?? Barra espectral acento (tipo Hekate) ??????????????????????????
        private void BarraAcento_Click(object sender, MouseButtonEventArgs e) { }
        private void BarraAcento_Move(object sender, MouseEventArgs e) { }

        // ?? Sliders R/G/B fondo estilo Hekate ????????????????????????????
        private void BarraFondoR_Click(object sender, MouseButtonEventArgs e) { }
        private void BarraFondoR_Move(object sender, MouseEventArgs e) { }
        private void BarraFondoG_Click(object sender, MouseButtonEventArgs e) { }
        private void BarraFondoG_Move(object sender, MouseEventArgs e) { }
        private void BarraFondoB_Click(object sender, MouseButtonEventArgs e) { }
        private void BarraFondoB_Move(object sender, MouseEventArgs e) { }

        private static (byte R, byte G, byte B) HsvToRgb(double h, double s, double v)
        {
            h = ((h % 360) + 360) % 360;
            double c = v * s;
            double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
            double m = v - c;
            (double r, double g, double b) = h switch
            {
                < 60  => (c, x, 0d),
                < 120 => (x, c, 0d),
                < 180 => (0d, c, x),
                < 240 => (0d, x, c),
                < 300 => (x, 0d, c),
                _     => (c, 0d, x),
            };
            return ((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
        }

        private async void BtnGuardarNyx_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_letraSD))
            {
                TxtLogNyx.Text       = "Selecciona una SD primero.";
                TxtLogNyx.Foreground = System.Windows.Media.Brushes.Gold;
                TxtLogNyx.Visibility = Visibility.Visible;
                return;
            }
            BtnGuardarNyx.IsEnabled = false;
            var copiados = new List<string>();
            var errores  = new List<string>();
            await EscribirNyxIniAsync(copiados, errores);
            TxtLogNyx.Text = errores.Count > 0
                ? $"Error: {errores[0]}"
                : "nyx.ini guardado correctamente";
            TxtLogNyx.Foreground = errores.Count > 0
                ? System.Windows.Media.Brushes.Gold
                : System.Windows.Media.Brushes.LimeGreen;
            TxtLogNyx.Visibility    = Visibility.Visible;
            BtnGuardarNyx.IsEnabled = true;
        }

        private static void AplicarColorAMuestra(Border muestra, string hex)
        {
            try
            {
                var color = (WpfColor)ColorConverter.ConvertFromString(hex);
                muestra.Background = new SolidColorBrush(color);
            }
            catch { }
        }

        private static bool IsValidHex(string hex)
        {
            foreach (char c in hex.TrimStart('#'))
                if (!Uri.IsHexDigit(c)) return false;
            return true;
        }
    }
}
