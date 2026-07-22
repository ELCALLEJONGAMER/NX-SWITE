using NX_Swite.Core.Configuracion;
using NX_Swite.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Net.Http;
using System.Threading.Tasks;

namespace NX_Swite.Core
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
    /// Servicio que descarga, cachea y reproduce los sonidos configurados en
    /// el Gist. Se accede v�a <see cref="Servicios.Sonidos"/>; no instanciar
    /// directamente fuera de ese contenedor.
    /// </summary>
    public sealed class GestorSonidos
    {
        // ?? Estado interno ??????????????????????????????????????

        private static readonly HttpClient _http = new();
        private string _rutaCache = string.Empty;
        private readonly Dictionary<EventoSonido, string> _rutasLocales = new();
        private DateTime _ultimoHover = DateTime.MinValue;
        private readonly List<System.Windows.Media.MediaPlayer> _playersActivos = new();
        // SoundPlayers pre-cargados en RAM para reproduccion instantanea (hover, click, navegacion)
        private SoundPlayer? _hoverPlayer;
        private SoundPlayer? _clickPlayer;
        private SoundPlayer? _navegacionPlayer;
        // MediaPlayer pre-cargado para hover cuando el archivo no es WAV PCM (ej: MP3)
        private System.Windows.Media.MediaPlayer? _hoverMediaPlayer;
        // MediaPlayer pre-cargado para click cuando el archivo no es WAV PCM (ej: MP3)
        private System.Windows.Media.MediaPlayer? _clickMediaPlayer;
        // MediaPlayer pre-cargado para navegacion cuando el archivo no es WAV PCM (ej: MP3)
        private System.Windows.Media.MediaPlayer? _navegacionMediaPlayer;

        public GestorSonidos() { }

        // ????????????????????????????????????????????????????????????????
        //  Configuraci�n inicial (llamar desde App o Splash)
        // ????????????????????????????????????????????????????????????????

        public void Configurar(string rutaCacheSonidos)
        {
            _rutaCache = rutaCacheSonidos ?? string.Empty;
        }

        // ????????????????????????????????????????????????????????????????
        //  Descarga y cach� de los WAVs del Gist
        // ????????????????????????????????????????????????????????????????

        /// <summary>
        /// Descarga los WAVs que a�n no est�n en cach� y registra sus rutas locales.
        /// Se llama despu�s de parsear el Gist.
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

            // Pre-cargar en RAM los sonidos de alta frecuencia
            PreCargarSoundPlayers();
        }

        private void PreCargarSoundPlayers()
        {
            _hoverPlayer      = CrearSoundPlayer(EventoSonido.Hover);
            _clickPlayer      = CrearSoundPlayer(EventoSonido.Click);
            _navegacionPlayer = CrearSoundPlayer(EventoSonido.Navegacion);

            if (_hoverPlayer      == null) PreCargarHoverMediaPlayer();
            if (_clickPlayer      == null) PreCargarClickMediaPlayer();
            if (_navegacionPlayer == null) PreCargarNavegacionMediaPlayer();
        }

        private void PreCargarHoverMediaPlayer()
        {
            if (!_rutasLocales.TryGetValue(EventoSonido.Hover, out var ruta) || !File.Exists(ruta)) return;
            try
            {
                _hoverMediaPlayer = new System.Windows.Media.MediaPlayer();
                _hoverMediaPlayer.Volume = Math.Clamp(ConfiguracionSonidos.Volumen, 0.0, 1.0);
                _hoverMediaPlayer.Open(new Uri(ruta, UriKind.Absolute));
            }
            catch { _hoverMediaPlayer = null; }
        }

        private void PreCargarClickMediaPlayer()
        {
            if (!_rutasLocales.TryGetValue(EventoSonido.Click, out var ruta) || !File.Exists(ruta)) return;
            try
            {
                _clickMediaPlayer = new System.Windows.Media.MediaPlayer();
                _clickMediaPlayer.Volume = Math.Clamp(ConfiguracionSonidos.Volumen, 0.0, 1.0);
                _clickMediaPlayer.Open(new Uri(ruta, UriKind.Absolute));
            }
            catch { _clickMediaPlayer = null; }
        }

        private void PreCargarNavegacionMediaPlayer()
        {
            if (!_rutasLocales.TryGetValue(EventoSonido.Navegacion, out var ruta) || !File.Exists(ruta)) return;
            try
            {
                _navegacionMediaPlayer = new System.Windows.Media.MediaPlayer();
                _navegacionMediaPlayer.Volume = Math.Clamp(ConfiguracionSonidos.Volumen, 0.0, 1.0);
                _navegacionMediaPlayer.Open(new Uri(ruta, UriKind.Absolute));
            }
            catch { _navegacionMediaPlayer = null; }
        }

        private SoundPlayer? CrearSoundPlayer(EventoSonido evento)
        {
            if (!_rutasLocales.TryGetValue(evento, out var ruta) || !File.Exists(ruta)) return null;
            try
            {
                byte[] bytes = File.ReadAllBytes(ruta);
                if (bytes.Length < 44) return null;

                // SoundPlayer solo acepta WAV PCM sin comprimir (audioFormat == 1)
                // Cualquier otro formato (ADPCM, MP3, IEEE float...) lanza 'wave header corrupt'
                short audioFormat = BitConverter.ToInt16(bytes, 20);
                if (audioFormat != 1) return null; // fallback a MediaPlayer

                byte[] ajustado = AjustarVolumenWav(bytes, ConfiguracionSonidos.Volumen);
                var    ms       = new MemoryStream(ajustado);
                var    sp       = new SoundPlayer(ms);
                sp.Load();
                return sp;
            }
            catch { return null; }
        }

        /// <summary>Escala las muestras PCM del WAV segun el volumen (0.0-1.0).</summary>
        private static byte[] AjustarVolumenWav(byte[] wav, double volumen)
        {
            float factor = (float)Math.Clamp(volumen, 0.0, 1.0);
            if (wav.Length < 44 || Math.Abs(factor - 1f) < 0.001f) return wav;

            // Leer formato del chunk fmt
            short audioFormat  = BitConverter.ToInt16(wav, 20);
            short bitsPerSample = BitConverter.ToInt16(wav, 34);
            if (audioFormat != 1) return wav; // solo PCM

            // Buscar el inicio del chunk "data"
            int dataOffset = 44;
            for (int i = 12; i < wav.Length - 8; i++)
            {
                if (wav[i] == 'd' && wav[i+1] == 'a' && wav[i+2] == 't' && wav[i+3] == 'a')
                { dataOffset = i + 8; break; }
            }

            byte[] result = (byte[])wav.Clone();

            if (bitsPerSample == 16)
            {
                for (int i = dataOffset; i < result.Length - 1; i += 2)
                {
                    short s = BitConverter.ToInt16(result, i);
                    int   v = (int)(s * factor);
                    short c = (short)Math.Clamp(v, short.MinValue, short.MaxValue);
                    result[i]     = (byte)(c & 0xFF);
                    result[i + 1] = (byte)((c >> 8) & 0xFF);
                }
            }
            else if (bitsPerSample == 8)
            {
                for (int i = dataOffset; i < result.Length; i++)
                {
                    int s = result[i] - 128;
                    result[i] = (byte)Math.Clamp((int)(s * factor) + 128, 0, 255);
                }
            }

            return result;
        }

        private async Task DescargarAsync(EventoSonido evento, string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;

            // Preservar la extensión real del archivo remoto (puede ser .wav, .mp3, etc.)
            string ext     = Path.GetExtension(new Uri(url).LocalPath).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext)) ext = ".wav";

            string rutaUrl = Path.Combine(_rutaCache, $"{evento}.url");

            // Si ya existe un archivo cacheado y la URL no cambió, reutilizarlo
            if (File.Exists(rutaUrl))
            {
                string urlGuardada = await File.ReadAllTextAsync(rutaUrl);
                if (urlGuardada == url)
                {
                    // Buscar el archivo con cualquier extensión compatible
                    string rutaExistente = BuscarArchivoSonido(evento);
                    if (rutaExistente != null && File.Exists(rutaExistente))
                    {
                        _rutasLocales[evento] = rutaExistente;
                        return;
                    }
                }
            }

            string ruta = Path.Combine(_rutaCache, $"{evento}{ext}");
            _rutasLocales[evento] = ruta;

            try
            {
                // Eliminar archivos anteriores del mismo evento con distinta extensión
                foreach (string antiguo in Directory.GetFiles(_rutaCache, $"{evento}.*"))
                {
                    string antigExt = Path.GetExtension(antiguo).ToLowerInvariant();
                    if (antigExt != ".url" && antiguo != ruta)
                        try { File.Delete(antiguo); } catch { }
                }

                byte[] bytes = await _http.GetByteArrayAsync(url);
                Directory.CreateDirectory(_rutaCache);
                await File.WriteAllBytesAsync(ruta, bytes);
                await File.WriteAllTextAsync(rutaUrl, url);
            }
            catch { /* Sin sonido en caso de fallo de red */ }
        }

        /// <summary>
        /// Busca en la carpeta de caché cualquier archivo de sonido para el evento dado
        /// (independientemente de extensión: .wav, .mp3, etc.).
        /// </summary>
        private string? BuscarArchivoSonido(EventoSonido evento)
        {
            if (!Directory.Exists(_rutaCache)) return null;
            foreach (string f in Directory.GetFiles(_rutaCache, $"{evento}.*"))
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext != ".url") return f;
            }
            return null;
        }

        // ????????????????????????????????????????????????????????????????
        //  Reproducci�n
        // ????????????????????????????????????????????????????????????????

        /// <summary>
        /// Reproduce el sonido asociado al evento, respetando master switch y toggles.
        /// No lanza excepciones � falla silenciosamente si el archivo no existe.
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

            // Hover: SoundPlayer (WAV PCM instantaneo) o MediaPlayer pre-cargado (MP3/otros)
            if (evento == EventoSonido.Hover)
            {
                if (_hoverPlayer != null) { _hoverPlayer.Play(); return; }
                if (_hoverMediaPlayer != null)
                {
                    _hoverMediaPlayer.Volume   = Math.Clamp(ConfiguracionSonidos.Volumen, 0.0, 1.0);
                    _hoverMediaPlayer.Position = TimeSpan.Zero;
                    _hoverMediaPlayer.Play();
                    return;
                }
            }

            // Click: SoundPlayer pre-cargado en RAM
            if (evento == EventoSonido.Click && _clickPlayer != null)
            { _clickPlayer.Play(); return; }

            // Click: MediaPlayer pre-cargado (formato no-PCM, ej: MP3)
            if (evento == EventoSonido.Click && _clickMediaPlayer != null)
            {
                _clickMediaPlayer.Volume   = Math.Clamp(ConfiguracionSonidos.Volumen, 0.0, 1.0);
                _clickMediaPlayer.Position = TimeSpan.Zero;
                _clickMediaPlayer.Play();
                return;
            }

            // Navegacion: SoundPlayer pre-cargado en RAM
            if (evento == EventoSonido.Navegacion && _navegacionPlayer != null)
            { _navegacionPlayer.Play(); return; }

            // Navegacion: MediaPlayer pre-cargado (formato no-PCM)
            if (evento == EventoSonido.Navegacion && _navegacionMediaPlayer != null)
            {
                _navegacionMediaPlayer.Volume   = Math.Clamp(ConfiguracionSonidos.Volumen, 0.0, 1.0);
                _navegacionMediaPlayer.Position = TimeSpan.Zero;
                _navegacionMediaPlayer.Play();
                return;
            }

            // Resto: MediaPlayer one-shot (soporta volumen, para sonidos poco frecuentes)
            try
            {
                var player = new System.Windows.Media.MediaPlayer();
                player.Volume = Math.Clamp(ConfiguracionSonidos.Volumen, 0.0, 1.0);

                lock (_playersActivos)
                    _playersActivos.Add(player);

                void Liberar()
                {
                    player.Close();
                    lock (_playersActivos)
                        _playersActivos.Remove(player);
                }

                player.MediaEnded += (_, _) => Liberar();
                player.MediaFailed += (_, _) => Liberar(); // evita memory leak si el archivo es invalido

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
        /// Indica si un sonido para este evento est� disponible en cach� local.
        /// </summary>
        public bool TieneCache(EventoSonido evento)
            => _rutasLocales.TryGetValue(evento, out var r) && File.Exists(r);
    }
}
