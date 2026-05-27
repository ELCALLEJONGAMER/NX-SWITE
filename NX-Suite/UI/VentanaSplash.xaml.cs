using NX_Suite.Core;
using NX_Suite.Core.Configuracion;
using NX_Suite.Network;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace NX_Suite.UI
{
    public partial class VentanaSplash : Window
    {
        public VentanaSplash()
        {
            InitializeComponent();
            Loaded += VentanaSplash_Loaded;
        }

        private async void VentanaSplash_Loaded(object sender, RoutedEventArgs e)
        {
            var gestorCache = new GestorCache();

            // 0. Cargar preferencias del usuario y aplicarlas antes que todo lo demás
            var preferencias = await Servicios.Preferencias.CargarAsync();
            GestorPreferencias.AplicarSonido(preferencias.Sonido);

            // 1. Si el intro ya est
            bool introReproducidoYa = Servicios.Sonidos.TieneCache(EventoSonido.Intro);
            DateTime horaInicioIntro = DateTime.Now;
            if (introReproducidoYa)
                Servicios.Sonidos.Reproducir(EventoSonido.Intro);

            // 2. Descargar Gist en background (sin delay mínimo artificial)
            var tareaGist = Task.Run(async () =>
            {
                var parser = new GistParser(gestorCache);
                return await parser.ObtenerTodoElGistAsync(ConfiguracionLocal.UrlGistPrincipal);
            });

            await tareaGist;
            var datos = tareaGist.Result;

            // 3. Logo: esperar a que cargue (máx 5 s) antes de continuar
            var tareaLogo    = CargarLogoAsync(datos?.GlobalBranding?.LogoUrl);
            var tareaTimeout = Task.Delay(TimeSpan.FromSeconds(5));
            await Task.WhenAny(tareaLogo, tareaTimeout);

            // 4. Descargar sonidos (logo se carga en paralelo)
            if (datos?.Sonidos != null)
                await Servicios.Sonidos.InicializarAsync(datos.Sonidos);

            // 5. Primera vez: reproducir intro ahora que ya está en caché
            if (!introReproducidoYa)
            {
                Servicios.Sonidos.Reproducir(EventoSonido.Intro);
                horaInicioIntro = DateTime.Now;
            }

            // 6. Dar tiempo al intro para sonar (máx 3 s desde que empezó)
            const int IntroMinMs = 3000;
            int transcurrido = (int)(DateTime.Now - horaInicioIntro).TotalMilliseconds;
            if (transcurrido < IntroMinMs)
                await Task.Delay(IntroMinMs - transcurrido);

            await FadeOutAsync();

            var main = new MainWindow();
            Application.Current.MainWindow = main;
            main.Show();
            Close();
        }

        private Task CargarLogoAsync(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return Task.CompletedTask;

            var tcs = new TaskCompletionSource();
            try
            {
                BitmapImage bmp;

                // Si ya está en caché local, cargamos desde disco de forma síncrona.
                string? rutaLocal = Servicios.Iconos.ObtenerRutaLocal(url);
                if (rutaLocal != null)
                {
                    bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource   = new Uri(rutaLocal);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    ImgLogo.Source     = bmp;
                    ImgLogo.Visibility = Visibility.Visible;
                    AnimarEntradaLogo();
                    tcs.TrySetResult();
                    return tcs.Task;
                }

                // No está en caché: descargamos y esperamos DownloadCompleted.
                bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource     = new Uri(url);
                bmp.CacheOption   = BitmapCacheOption.OnLoad;
                bmp.CreateOptions = BitmapCreateOptions.None;
                bmp.EndInit();

                void Mostrar()
                {
                    try { bmp.Freeze(); } catch { /* ignorar */ }
                    ImgLogo.Source     = bmp;
                    ImgLogo.Visibility = Visibility.Visible;
                    _ = Servicios.Iconos.DescargarSiNoExisteAsync(url);
                    AnimarEntradaLogo();
                    tcs.TrySetResult();
                }

                if (!bmp.IsDownloading)
                    Mostrar();
                else
                {
                    bmp.DownloadCompleted += (_, _) => Mostrar();
                    bmp.DownloadFailed    += (_, _) => tcs.TrySetResult();
                }
            }
            catch { tcs.TrySetResult(); }

            return tcs.Task;
        }

        private void AnimarEntradaLogo()
        {
            var duracion = new Duration(TimeSpan.FromMilliseconds(480));
            var ease     = new CubicEase { EasingMode = EasingMode.EaseOut };

            // Fade-in: 0 ? 1
            var fadeIn = new DoubleAnimation(0, 1, duracion) { EasingFunction = ease };
            ImgLogo.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            // Zoom: 0.88 ? 1.0 en ambos ejes
            var scaleX = new DoubleAnimation(0.88, 1.0, duracion) { EasingFunction = ease };
            var scaleY = new DoubleAnimation(0.88, 1.0, duracion) { EasingFunction = ease };
            EscalaLogo.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
            EscalaLogo.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
        }

        private Task FadeOutAsync()
        {
            var tcs = new TaskCompletionSource();
            var anim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(400));
            anim.Completed += (_, _) => tcs.SetResult();
            BeginAnimation(OpacityProperty, anim);
            return tcs.Task;
        }
    }
}
