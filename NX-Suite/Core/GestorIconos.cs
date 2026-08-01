using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace NX_Swite.Core
{
    /// <summary>
    /// Gestiona la descarga y cach� local de iconos remotos. Se accede v�a
    /// <see cref="Servicios.Iconos"/>; no instanciar directamente fuera de
    /// ese contenedor.
    /// </summary>
    public class GestorIconos
    {
        private static readonly HttpClient _client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(8)
        };

        // Tiempo m�ximo que GifIcon espera una descarga antes de mostrar nada
        // y dejar que la descarga siga en segundo plano.
        public static readonly TimeSpan TimeoutVisual = TimeSpan.FromSeconds(3);
        private readonly string _rutaCache;

        public GestorIconos(string rutaCache)
        {
            _rutaCache = rutaCache;
            Directory.CreateDirectory(_rutaCache);

            // Algunos CDN (icons8, etc.) bloquean el User-Agent por defecto de .NET.
            if (!_client.DefaultRequestHeaders.Contains("User-Agent"))
                _client.DefaultRequestHeaders.TryAddWithoutValidation(
                    "User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        }

        // ?? API p�blica ??????????????????????????????????????????????????

        /// <summary>
        /// Retorna la ruta local del icono si ya est� en cach�, o <c>null</c> si no.
        /// </summary>
        public string? ObtenerRutaLocal(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            string ruta = RutaArchivo(url);
            return File.Exists(ruta) ? ruta : null;
        }

        /// <summary>
        /// Descarga el icono y lo guarda en cach�. No hace nada si ya existe.
        /// </summary>
        public async Task DescargarSiNoExisteAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            string ruta = RutaArchivo(url);
            if (File.Exists(ruta)) return;

            try
            {
                byte[] datos = await _client.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(ruta, datos);
            }
            catch { /* Silencioso: el icono se cargar� desde la red igualmente */ }
        }

        /// <summary>
        /// Revalida en background un icono ya cacheado: descarga el contenido
        /// remoto y lo compara (hash SHA-256) contra la copia local. Si difiere,
        /// sobreescribe la cach� y retorna <c>true</c> para que el llamador
        /// recargue el <see cref="System.Windows.Media.Imaging.BitmapImage"/>.
        /// Pensado para casos donde la URL del Gist no cambia pero el archivo
        /// remoto (asset) s� — a diferencia de <see cref="DescargarSiNoExisteAsync"/>,
        /// que no vuelve a tocar la red si ya existe copia local.
        /// No hace nada (retorna <c>false</c>) si no hay conexi�n o el icono
        /// a�n no est� en cach� (en ese caso usar <see cref="DescargarSiNoExisteAsync"/>).
        /// </summary>
        public async Task<bool> RevalidarSiCambioAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            string ruta = RutaArchivo(url);
            if (!File.Exists(ruta)) return false;

            try
            {
                byte[] datosRemotos = await _client.GetByteArrayAsync(url);
                byte[] hashRemoto   = SHA256.HashData(datosRemotos);
                byte[] hashLocal    = SHA256.HashData(await File.ReadAllBytesAsync(ruta));

                if (hashRemoto.AsSpan().SequenceEqual(hashLocal)) return false;

                await File.WriteAllBytesAsync(ruta, datosRemotos);
                return true;
            }
            catch { return false; /* Sin red o error: se conserva la cach� actual */ }
        }

        /// <summary>
        /// Guarda bytes ya descargados en cach�. �til cuando el llamador ya
        /// hizo la descarga para evitar una segunda petici�n de red.
        /// </summary>
        public async Task GuardarEnCacheAsync(string url, byte[] datos)
        {
            if (string.IsNullOrWhiteSpace(url) || datos is null || datos.Length == 0) return;
            string ruta = RutaArchivo(url);
            try { await File.WriteAllBytesAsync(ruta, datos); } catch { }
        }

        /// <summary>
        /// Descarga el icono con un tiempo l�mite propio y lo devuelve,
        /// o null si falla / excede el timeout.
        /// </summary>
        public async Task<byte[]?> DescargarConTimeoutAsync(string url, TimeSpan timeout)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            using var cts = new System.Threading.CancellationTokenSource(timeout);
            try
            {
                return await _client.GetByteArrayAsync(url, cts.Token);
            }
            catch { return null; }
        }

        /// <summary>
        /// Elimina el icono del cach� local para forzar su re-descarga en el pr�ximo acceso.
        /// </summary>
        public void InvalidarCache(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            string ruta = RutaArchivo(url);
            try { if (File.Exists(ruta)) File.Delete(ruta); } catch { }
        }

        /// <summary>
        /// Elimina del caché local todos los iconos de la lista de URLs proporcionada.
        /// Útil para invalidar iconos en masa cuando el Gist se actualiza.
        /// </summary>
        public void InvalidarIconos(IEnumerable<string> urls)
        {
            foreach (var url in urls)
                InvalidarCache(url);
        }

        /// <summary>
        /// Descarga en paralelo una lista de URLs.
        /// </summary>
        public Task DescargarTodosAsync(IEnumerable<string> urls)
        {
            var tareas = urls
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(DescargarSiNoExisteAsync);

            return Task.WhenAll(tareas);
        }

        /// <summary>
        /// Pre-cachea en background todos los iconos declarados en la
        /// <see cref="NX_Swite.Models.ConfiguracionUI"/> para que est�n
        /// disponibles offline en la pr�xima sesi�n.
        /// </summary>
        public Task PreCachearIconosUiAsync(NX_Swite.Models.ConfiguracionUI cfg)
        {
            var urls = new[]
            {
                cfg.IconoCacheUrl,
                cfg.IconoEliminarUrl,
                cfg.IconoAgregarUrl,
                cfg.IconoVolverUrl,
                cfg.IconoSiguienteUrl,
                cfg.IconoPaginaAnteriorUrl,
                cfg.IconoPaginaSiguienteUrl,
                cfg.IconoZipUrl,
                cfg.IconoQueueUrl,
                cfg.IconoBellUrl,
                cfg.IconoMailUrl,
                cfg.IconoUpdateUrl,
                cfg.IconoMicroSDUrl,
                cfg.IconoPaintUrl,
                cfg.IconoInfoUrl,
                cfg.IconoEjectUrl,
                cfg.IconoConfigUrl,
                cfg.IconoCarpetaUrl,
                cfg.IconoArchivoUrl,
                cfg.IconoShieldUrl,
                cfg.IconoLogUrl,
                cfg.IconoRp2040Url,
            };

            return DescargarTodosAsync(urls);
        }

        // ?? Helpers ??????????????????????????????????????????????????????

        /// <summary>
        /// Genera un nombre de archivo �nico y estable para una URL dada.
        /// Formato: primeros 16 hex del SHA-256 de la URL + extensi�n original.
        /// </summary>
        private string RutaArchivo(string url)
        {
            string extension = string.Empty;
            try
            {
                extension = Path.GetExtension(new Uri(url).LocalPath);
            }
            catch { }

            if (string.IsNullOrEmpty(extension) || extension.Length > 5)
                extension = ".png";

            byte[] hash   = SHA256.HashData(Encoding.UTF8.GetBytes(url));
            string nombre = Convert.ToHexString(hash)[..16] + extension;
            return Path.Combine(_rutaCache, nombre);
        }
    }
}
