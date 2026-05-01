using NX_Suite.Core;
using NX_Suite.Core.Configuracion;
using NX_Suite.Network;
using System;
using System.Threading.Tasks;
using System.Windows;
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

            // 1. Si el intro ya está en caché ? reproducir AHORA, al inicio
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

            // 3. Logo: fire-and-forget — aparece en cuanto el PNG descarga
            _ = CargarLogoAsync(datos?.GlobalBranding?.LogoUrl);

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
                var bmp = new BitmapImage(new Uri(url));

                void Mostrar()
                {
                    ImgLogo.Source     = bmp;
                    ImgLogo.Visibility = Visibility.Visible;
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
