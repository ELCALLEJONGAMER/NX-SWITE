using System.Reflection;
using NX_Suite.Core;
using NX_Suite.Models;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace NX_Suite.UI.Controles
{
    public partial class PanelIzquierdo : UserControl
    {
        public event EventHandler? LogoInicioSolicitado;

        public static readonly DependencyProperty NombreProgramaProperty =
            DependencyProperty.Register(
                nameof(NombrePrograma),
                typeof(string),
                typeof(PanelIzquierdo),
                new PropertyMetadata(string.Empty, OnNombreProgramaChanged));

        public static readonly DependencyProperty LogoUrlProperty =
            DependencyProperty.Register(
                nameof(LogoUrl),
                typeof(string),
                typeof(PanelIzquierdo),
                new PropertyMetadata(string.Empty));

        public string NombrePrograma
        {
            get => (string)GetValue(NombreProgramaProperty);
            set => SetValue(NombreProgramaProperty, value);
        }

        public string LogoUrl
        {
            get => (string)GetValue(LogoUrlProperty);
            set => SetValue(LogoUrlProperty, value);
        }

        public PanelIzquierdo()
        {
            InitializeComponent();
            var ver = Assembly.GetExecutingAssembly().GetName().Version;
            TxtVersionApp.Text = ver != null ? $"v{ver.Major}.{ver.Minor}.{ver.Build}" : string.Empty;
        }

        public async Task AplicarBrandingAsync(BrandingConfig branding)
        {
            if (branding == null)
                return;

            NombrePrograma = branding.NombrePrograma ?? string.Empty;
            LogoUrl        = branding.LogoUrl        ?? string.Empty;

            bool tieneTexto = !string.IsNullOrWhiteSpace(NombrePrograma);

            TxtNombrePrograma.Visibility = tieneTexto ? Visibility.Visible : Visibility.Collapsed;
            BtnLogoPrograma.Visibility   = string.IsNullOrWhiteSpace(LogoUrl)
                ? Visibility.Collapsed
                : Visibility.Visible;

            CabeceraPrograma.Margin = tieneTexto
                ? new Thickness(0, 10, 0, 35)
                : new Thickness(0, 10, 0, 18);

            if (!string.IsNullOrWhiteSpace(LogoUrl))
                ImgLogoPrograma.Source = await CargarLogoAsync(LogoUrl);
        }

        private static Task<BitmapImage?> CargarLogoAsync(string url)
        {
            // Si ya está en caché local, cargamos desde disco de forma inmediata y síncrona.
            string? rutaLocal = Servicios.Iconos.ObtenerRutaLocal(url);
            if (rutaLocal != null)
            {
                try
                {
                    var bmpLocal = new BitmapImage();
                    bmpLocal.BeginInit();
                    bmpLocal.UriSource   = new Uri(rutaLocal);
                    bmpLocal.CacheOption = BitmapCacheOption.OnLoad;
                    bmpLocal.EndInit();
                    bmpLocal.Freeze();
                    return Task.FromResult<BitmapImage?>(bmpLocal);
                }
                catch
                {
                    // Si el archivo local está corrupto, caemos a la descarga remota.
                }
            }

            // No está en caché: descargamos y esperamos DownloadCompleted antes de asignar.
            var tcs = new TaskCompletionSource<BitmapImage?>(TaskCreationOptions.RunContinuationsAsynchronously);

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource        = new Uri(url);
            bmp.CreateOptions    = BitmapCreateOptions.IgnoreImageCache;
            bmp.CacheOption      = BitmapCacheOption.OnLoad;
            bmp.EndInit();

            if (bmp.IsDownloading)
            {
                bmp.DownloadCompleted += (_, _) =>
                {
                    try { bmp.Freeze(); } catch { /* ignorar si no se puede congelar */ }
                    tcs.TrySetResult(bmp);
                    // Guardar en disco para la próxima vez.
                    _ = Servicios.Iconos.DescargarSiNoExisteAsync(url);
                };
                bmp.DownloadFailed += (_, _) => tcs.TrySetResult(null);
            }
            else
            {
                try { bmp.Freeze(); } catch { /* ignorar */ }
                tcs.TrySetResult(bmp);
            }

            return tcs.Task;
        }

        private void BtnLogoPrograma_Click(object sender, RoutedEventArgs e)
            => LogoInicioSolicitado?.Invoke(this, EventArgs.Empty);

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

        private static void OnNombreProgramaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PanelIzquierdo panel)
            {
                panel.TxtNombrePrograma.Text = e.NewValue as string ?? string.Empty;
            }
        }
    }
}

