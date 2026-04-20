using NX_Suite.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace NX_Suite.Core
{
    /// <summary>
    /// Eventos de sonido disponibles en la app.
    /// Cada uno tiene su propio bool en <see cref="ConfiguracionSonidos"/>.
    /// </summary>
    public enum EventoSonido
    {
        Intro,
        Cerrar,
        Click,
        Hover,
        Instalar,
        Exito,
        Error,
        Navegacion
    }

    /// <summary>
    /// Servicio singleton que descarga, cachea y reproduce los sonidos configurados en el Gist.
    /// </summary>
    public sealed class GestorSonidos
    {
        // ?? Singleton ????????????????????????????????????????????????????

        private static GestorSonidos? _instancia;
        public static GestorSonidos Instancia => _instancia ??= new GestorSonidos();

        // ?? Estado interno ???????????????????????????????????????????????

        private static readonly HttpClient _http = new();
        private string _rutaCache = string.Empty;
        private readonly Dictionary<EventoSonido, string> _rutasLocales = new();
        private DateTime _ultimoHover = DateTime.MinValue;
        private readonly List<System.Windows.Media.MediaPlayer> _playersActivos = new();

        private GestorSonidos() { }

        // ????????????????????????????????????????????????????????????????
        //  Configuración inicial (llamar desde App o Splash)
        // ????????????????????????????????????????????????????????????????

        public void Configurar(string rutaCacheSonidos)
        {
            _rutaCache = rutaCacheSonidos ?? string.Empty;
        }

        // ????????????????????????????????????????????????????????????????
        //  Descarga y caché de los WAVs del Gist
        // ????????????????????????????????????????????????????????????????

        /// <summary>
        /// Descarga los WAVs que aún no estén en caché y registra sus rutas locales.
        /// Se llama después de parsear el Gist.
        /// </summary>
        public async Task InicializarAsync(SonidosConfig config)
        {
            if (string.IsNullOrWhiteSpace(_rutaCache)) return;

            await DescargarAsync(EventoSonido.Intro,      config.Intro);
            await DescargarAsync(EventoSonido.Cerrar,     config.Cerrar);
            await DescargarAsync(EventoSonido.Click,      config.Click);
            await DescargarAsync(EventoSonido.Hover,      config.Hover);
            await DescargarAsync(EventoSonido.Instalar,   config.Instalar);
            await DescargarAsync(EventoSonido.Exito,      config.Exito);
            await DescargarAsync(EventoSonido.Error,      config.Error);
            await DescargarAsync(EventoSonido.Navegacion, config.Navegacion);
        }

        private async Task DescargarAsync(EventoSonido evento, string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;

            string ruta = Path.Combine(_rutaCache, $"{evento}.wav");
            _rutasLocales[evento] = ruta;

            if (File.Exists(ruta)) return; // ya en caché

            try
            {
                byte[] bytes = await _http.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(ruta, bytes);
            }
            catch { /* Sin sonido en caso de fallo de red */ }
        }

        // ????????????????????????????????????????????????????????????????
        //  Reproducción
        // ????????????????????????????????????????????????????????????????

        /// <summary>
        /// Reproduce el sonido asociado al evento, respetando master switch y toggles.
        /// No lanza excepciones — falla silenciosamente si el archivo no existe.
        /// </summary>
        public void Reproducir(EventoSonido evento)
        {
            if (!ConfiguracionSonidos.SonidosActivos) return;
            if (!EstaHabilitado(evento))               return;

            // Anti-spam para hover
            if (evento == EventoSonido.Hover)
            {
                if ((DateTime.Now - _ultimoHover).TotalMilliseconds < ConfiguracionSonidos.RetardoHoverMs)
                    return;
                _ultimoHover = DateTime.Now;
            }

            if (!_rutasLocales.TryGetValue(evento, out var ruta) || !File.Exists(ruta)) return;

            try
            {
                var player = new System.Windows.Media.MediaPlayer();
                player.Volume = Math.Clamp(ConfiguracionSonidos.Volumen, 0.0, 1.0);

                lock (_playersActivos)
                    _playersActivos.Add(player);

                player.MediaEnded += (_, _) =>
                {
                    player.Close();
                    lock (_playersActivos)
                        _playersActivos.Remove(player);
                };

                player.MediaOpened += (_, _) => player.Play();
                player.Open(new Uri(ruta, UriKind.Absolute));
            }
            catch { }
        }

        // ????????????????????????????????????????????????????????????????
        //  Helpers
        // ????????????????????????????????????????????????????????????????

        /// <summary>
        /// Consulta el bool individual de <see cref="ConfiguracionSonidos"/> para el evento dado.
        /// </summary>
        private static bool EstaHabilitado(EventoSonido evento) => evento switch
        {
            EventoSonido.Intro      => ConfiguracionSonidos.Intro,
            EventoSonido.Cerrar     => ConfiguracionSonidos.Cerrar,
            EventoSonido.Click      => ConfiguracionSonidos.Click,
            EventoSonido.Hover      => ConfiguracionSonidos.Hover,
            EventoSonido.Instalar   => ConfiguracionSonidos.Instalar,
            EventoSonido.Exito      => ConfiguracionSonidos.Exito,
            EventoSonido.Error      => ConfiguracionSonidos.Error,
            EventoSonido.Navegacion => ConfiguracionSonidos.Navegacion,
            _                       => false
        };

        /// <summary>
        /// Indica si un sonido para este evento está disponible en caché local.
        /// </summary>
        public bool TieneCache(EventoSonido evento)
            => _rutasLocales.TryGetValue(evento, out var r) && File.Exists(r);
    }
}
