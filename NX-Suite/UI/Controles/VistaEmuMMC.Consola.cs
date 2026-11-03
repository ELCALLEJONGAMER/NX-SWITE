using NX_Swite.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NX_Swite.UI.Controles
{
    /// <summary>Cabecera «Tu consola»: perfiles con avatar y almacenamiento de la emuMMC.</summary>
    public partial class VistaEmuMMC
    {
        public static IMultiValueConverter Proporcion { get; } = new ConvProporcion();

        private sealed class ConvProporcion : IMultiValueConverter
        {
            public object Convert(object[] v, Type t, object p, CultureInfo c)
                => v.Length == 2 && v[0] is double pct && v[1] is double ancho ? Math.Max(0, ancho * Math.Clamp(pct, 0, 100) / 100) : 0d;
            public object[] ConvertBack(object v, Type[] t, object p, CultureInfo c) => throw new NotSupportedException();
        }

        public sealed class ItemPerfil
        {
            public string        Apodo        { get; init; } = string.Empty;
            public ImageSource?  Imagen       { get; init; }
            public Brush         Color        { get; init; } = Brushes.Gray;
            public int           Orden        { get; init; }
            public bool          Seleccionado { get; set; }
            public PerfilEmuMmc  Perfil       { get; init; } = new();
            public string        Inicial => string.IsNullOrEmpty(Apodo) ? "?" : Apodo[..1].ToUpperInvariant();
            public Visibility    VisibilidadAvatar => Imagen == null ? Visibility.Collapsed : Visibility.Visible;
        }

        public sealed class ItemEspacio
        {
            public string Nombre     { get; init; } = string.Empty;
            public string Detalle    { get; init; } = string.Empty;
            public double Porcentaje { get; init; }
            public Brush  Color      { get; init; } = Brushes.Cyan;
        }

        private static readonly string[] ColoresPerfil = { "#00D2FF", "#B388FF", "#FF4081", "#00E676", "#FFB300", "#FF6E40", "#40C4FF", "#EEFF41" };

        private List<ItemPerfil> _perfiles = new();

        // Siluetas lineales (viewBox ~ 240x100) de cada familia de consola.
        private const string SiluetaNormal =
            "M10 20a10 10 0 0 1 10-10h22v80H20a10 10 0 0 1-10-10z M42 10h156v80H42z M198 10h22a10 10 0 0 1 10 10v60a10 10 0 0 1-10 10h-22z " +
            "M52 18h136v64H52z M26 30a5 5 0 1 0 0.01 0 M26 60h0.01 M214 50a5 5 0 1 0 0.01 0 M214 30h0.01";
        private const string SiluetaOled =
            "M10 20a10 10 0 0 1 10-10h22v80H20a10 10 0 0 1-10-10z M42 10h156v80H42z M198 10h22a10 10 0 0 1 10 10v60a10 10 0 0 1-10 10h-22z " +
            "M46 14h148v72H46z M26 30a5 5 0 1 0 0.01 0 M26 60h0.01 M214 50a5 5 0 1 0 0.01 0 M214 30h0.01 M60 90h120v6H60z";
        private const string SiluetaLite =
            "M30 10h180a28 40 0 0 1 0 80H30a28 40 0 0 1 0-80z M64 18h112v64H64z " +
            "M34 34a6 6 0 1 0 0.01 0 M30 62h12 M36 56v12 M206 56a6 6 0 1 0 0.01 0 M200 30h0.01 M212 30h0.01";

        /// <summary>Ajusta el fondo de la tarjeta de perfil al modelo detectado por el serial.</summary>
        private void AplicarModeloConsola(string? serial)
        {
            var entrada = ModeloSwitchTable.Resolver(serial);
            string modelo = entrada?.Modelo ?? string.Empty;
            (string silueta, string izq, string der, string etiqueta) = modelo switch
            {
                var m when m.Contains("Lite") => (SiluetaLite, "#FFD54F", "#4DD0E1", "SWITCH LITE"),
                var m when m.Contains("OLED") => (SiluetaOled, "#FFFFFF", "#B388FF", "SWITCH OLED"),
                "" => (SiluetaNormal, "#00D2FF", "#FF4081", string.Empty),
                _ => (SiluetaNormal, "#00D2FF", "#FF4081", modelo.Replace("Nintendo ", string.Empty).ToUpperInvariant()),
            };
            SiluetaConsola.Data = Geometry.Parse(silueta);
            var cIzq = (Color)ColorConverter.ConvertFromString(izq);
            var cDer = (Color)ColorConverter.ConvertFromString(der);
            ColorConsolaIzq.Color = cIzq;
            ColorConsolaDer.Color = cDer;
            GlowConsola.Color = cIzq;
            FondoPerfil.GradientStops[0].Color = Color.FromArgb(0x22, cIzq.R, cIzq.G, cIzq.B);
            FondoPerfil.GradientStops[2].Color = Color.FromArgb(0x22, cDer.R, cDer.G, cDer.B);
            TxtModeloConsola.Text = entrada == null ? etiqueta : $"{etiqueta} · {entrada.Region.ToUpperInvariant()}";
            ChipModelo.Visibility = string.IsNullOrEmpty(etiqueta) ? Visibility.Collapsed : Visibility.Visible;

            var pulso = new System.Windows.Media.Animation.DoubleAnimation(0.16, 0.3, TimeSpan.FromSeconds(2.6))
            {
                AutoReverse = true,
                RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
                EasingFunction = new System.Windows.Media.Animation.SineEase(),
            };
            FondoConsola.BeginAnimation(OpacityProperty, pulso);
        }

        private void LimpiarConsola()
        {
            _perfiles = new();
            ListaPerfiles.ItemsSource = null;
            ListaEspacio.ItemsSource = null;
            TxtSinEspacio.Visibility = Visibility.Visible;
            TxtPerfilNombre.Text = "Perfiles de la emuMMC";
            TxtPerfilDetalle.Text = "Lee la emuMMC para ver tus perfiles y tu almacenamiento.";
            TxtBtnLeerConsola.Text = "Leer consola";
        }

        private async void BtnLeerConsola_Click(object sender, RoutedEventArgs e) => await CargarConsolaAsync();

        private async Task CargarConsolaAsync()
        {
            string? letra = ObtenerLetraSD?.Invoke();
            int? disco = ObtenerDiscoFisico?.Invoke();
            if (string.IsNullOrWhiteSpace(letra) || disco == null) return;

            BtnLeerConsola.IsEnabled = false;
            TxtBtnLeerConsola.Text = "Leyendo…";
            TxtPerfilDetalle.Text = "Descifrando la emuMMC (solo lectura)…";
            try
            {
                var (perfiles, user, system) = await Task.Run(() =>
                {
                    using var l = EmuMmcSavesLogic.Abrir(letra, disco.Value);
                    return (l.LeerPerfilesCompletos(), l.EspacioUser(), l.EspacioSystem());
                });

                _perfiles = perfiles.Select((p, i) => new ItemPerfil
                {
                    Apodo  = p.Apodo,
                    Imagen = CargarImagen(p.Avatar),
                    Color  = Pincel(ColoresPerfil[i % ColoresPerfil.Length]),
                    Orden  = perfiles.Count - i,
                    Perfil = p,
                }).ToList();
                if (_perfiles.Count > 0) _perfiles[0].Seleccionado = true;
                ListaPerfiles.ItemsSource = _perfiles;
                MostrarPerfil(_perfiles.FirstOrDefault());

                var espacio = new List<ItemEspacio> { Espacio("Memoria de la consola", user, "#00D2FF") };
                if (system != null && system.Total > 0) espacio.Add(Espacio("Sistema", system, "#B388FF"));
                try
                {
                    var d = new DriveInfo(letra);
                    espacio.Add(Espacio("microSD", new EspacioParticion { Total = d.TotalSize, Libre = d.AvailableFreeSpace }, "#00E676"));
                }
                catch { }
                ListaEspacio.ItemsSource = espacio;
                TxtSinEspacio.Visibility = Visibility.Collapsed;
                TxtBtnLeerConsola.Text = "Volver a leer";
            }
            catch (Exception ex)
            {
                Services.Logger.Error("[emuMMC] No se pudo leer la consola", ex);
                TxtPerfilDetalle.Text = ex.Message;
                TxtBtnLeerConsola.Text = "Reintentar";
            }
            finally
            {
                BtnLeerConsola.IsEnabled = true;
            }
        }

        private void MostrarPerfil(ItemPerfil? p)
        {
            if (p == null)
            {
                TxtPerfilNombre.Text = "Sin perfiles";
                TxtPerfilDetalle.Text = "No se encontraron perfiles de usuario en la emuMMC.";
                return;
            }
            TxtPerfilNombre.Text = p.Apodo;
            TxtPerfilDetalle.Text = _perfiles.Count == 1
                ? "1 perfil en esta consola"
                : $"{_perfiles.Count} perfiles en esta consola · haz clic en un avatar para cambiar";
        }

        private void Perfil_Click(object sender, MouseButtonEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not ItemPerfil p) return;
            foreach (var x in _perfiles) x.Seleccionado = x == p;
            ListaPerfiles.ItemsSource = null;
            ListaPerfiles.ItemsSource = _perfiles;
            MostrarPerfil(p);
        }

        private static ItemEspacio Espacio(string nombre, EspacioParticion e, string color) => new()
        {
            Nombre     = nombre,
            Detalle    = e.Total > 0
                ? $"{EmuMmcMigracionLogic.FormatearTamano(e.Libre)} libres de {EmuMmcMigracionLogic.FormatearTamano(e.Total)}"
                : "—",
            Porcentaje = e.Porcentaje,
            Color      = Pincel(color),
        };

        private static ImageSource? CargarImagen(byte[]? datos)
        {
            if (datos == null || datos.Length == 0) return null;
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.DecodePixelWidth = 152;
                bmp.StreamSource = new MemoryStream(datos);
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch { return null; }
        }
    }
}
