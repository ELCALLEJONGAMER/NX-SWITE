using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace NX_Suite.Core
{
    /// <summary>
    /// Gestiona la descarga y caché local de iconos remotos. Se accede vía
    /// <see cref="Servicios.Iconos"/>; no instanciar directamente fuera de
    /// ese contenedor.
    /// </summary>
    public class GestorIconos
    {
        private static readonly HttpClient _client = new HttpClient();
        private readonly string _rutaCache;

        public GestorIconos(string rutaCache)
        {
            _rutaCache = rutaCache;
            Directory.CreateDirectory(_rutaCache);
        }

        // ?? API pública ??????????????????????????????????????????????????

        /// <summary>
        /// Retorna la ruta local del icono si ya está en caché, o <c>null</c> si no.
        /// </summary>
        public string? ObtenerRutaLocal(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            string ruta = RutaArchivo(url);
            return File.Exists(ruta) ? ruta : null;
        }

        /// <summary>
        /// Descarga el icono y lo guarda en caché. No hace nada si ya existe.
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
            catch { /* Silencioso: el icono se cargará desde la red igualmente */ }
        }

        /// <summary>
        /// Descarga en paralelo una lista de URLs. Útil para pre-cargar tras sincronizar.
        /// </summary>
        public Task DescargarTodosAsync(IEnumerable<string> urls)
        {
            var tareas = urls
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(DescargarSiNoExisteAsync);

            return Task.WhenAll(tareas);
        }

        // ?? Helpers ??????????????????????????????????????????????????????

        /// <summary>
        /// Genera un nombre de archivo único y estable para una URL dada.
        /// Formato: primeros 16 hex del SHA-256 de la URL + extensión original.
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
