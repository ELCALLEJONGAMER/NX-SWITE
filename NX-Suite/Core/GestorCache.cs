using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using NX_Suite.Models;

namespace NX_Suite.Core
{
    public class GestorCache
    {
        public string RutaBovedaZips { get; private set; }
        public string RutaBovedaExtraccion { get; private set; }
        public string RutaCacheGist { get; private set; }
        public string RutaCacheIconos { get; private set; }
        public string RutaCacheSonidos { get; private set; }

        public GestorCache()
        {
            string carpetaAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            RutaBovedaZips       = Path.Combine(carpetaAppData, "NX-Suite", "Cache", "Zips");
            RutaBovedaExtraccion = Path.Combine(carpetaAppData, "NX-Suite", "Cache", "Extracted");
            RutaCacheGist        = Path.Combine(carpetaAppData, "NX-Suite", "Cache", "gist_cache.json");
            RutaCacheIconos      = Path.Combine(carpetaAppData, "NX-Suite", "Cache", "Icons");
            RutaCacheSonidos     = Path.Combine(carpetaAppData, "NX-Suite", "Cache", "Sounds");

            if (!Directory.Exists(RutaBovedaZips))       Directory.CreateDirectory(RutaBovedaZips);
            if (!Directory.Exists(RutaBovedaExtraccion)) Directory.CreateDirectory(RutaBovedaExtraccion);
            if (!Directory.Exists(RutaCacheSonidos))     Directory.CreateDirectory(RutaCacheSonidos);
            // RutaCacheIconos lo crea GestorIconos al inicializarse
        }

        // ── Caché del JSON del Gist ──────────────────────────────────────

        /// <summary>
        /// Guarda el JSON descargado en disco. Llamar tras cada descarga exitosa.
        /// </summary>
        public async Task GuardarJsonGistAsync(string jsonContent)
        {
            if (string.IsNullOrWhiteSpace(jsonContent)) return;
            try
            {
                await File.WriteAllTextAsync(RutaCacheGist, jsonContent);
            }
            catch { /* Si falla el guardado, no es crítico */ }
        }

        /// <summary>
        /// Carga el JSON guardado localmente. Retorna null si no existe o está corrupto.
        /// </summary>
        public async Task<string?> CargarJsonGistAsync()
        {
            try
            {
                if (!File.Exists(RutaCacheGist)) return null;
                return await File.ReadAllTextAsync(RutaCacheGist);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Indica si existe un JSON del Gist guardado localmente.
        /// </summary>
        public bool TieneCacheGist => File.Exists(RutaCacheGist);

        /// <summary>
        /// Retorna la fecha de la última descarga del Gist o null si no hay caché.
        /// </summary>
        public DateTime? FechaUltimaCacheGist =>
            File.Exists(RutaCacheGist) ? File.GetLastWriteTime(RutaCacheGist) : null;

        /// <summary>
        /// Indica si el caché del Gist existe y su antigüedad está dentro del TTL indicado.
        /// </summary>
        /// <param name="ttlHoras">Horas máximas de vida del caché. 0 = siempre expirado.</param>
        public bool CacheGistEsValido(double ttlHoras)
        {
            if (ttlHoras <= 0 || !TieneCacheGist) return false;
            DateTime? fecha = FechaUltimaCacheGist;
            return fecha.HasValue && (DateTime.Now - fecha.Value).TotalHours < ttlHoras;
        }

        public void ActualizarEstadoCache(IEnumerable<ModuloConfig> modulos)
        {
            if (modulos == null) return;

            foreach (var modulo in modulos)
            {
                if (modulo?.Versiones == null || modulo.Versiones.Count == 0)
                    continue;

                var version         = modulo.Versiones[0];
                string nombreZip    = ObtenerArchivoZip(version);
                string nombreCarpeta = ObtenerCarpetaExtraida(version);

                string rutaZip     = string.IsNullOrEmpty(nombreZip)
                                     ? string.Empty
                                     : Path.Combine(RutaBovedaZips, nombreZip);
                string rutaCarpeta = string.IsNullOrEmpty(nombreCarpeta)
                                     ? string.Empty
                                     : Path.Combine(RutaBovedaExtraccion, nombreCarpeta);

                modulo.RutaCacheZip     = rutaZip;
                modulo.RutaCacheCarpeta = rutaCarpeta;

                bool zipExiste     = !string.IsNullOrEmpty(rutaZip)     && File.Exists(rutaZip);
                bool carpetaExiste = !string.IsNullOrEmpty(rutaCarpeta) && Directory.Exists(rutaCarpeta);

                // También detectar archivos no comprimidos descargados directamente a Extracted
                bool archivoEnExtraccionExiste = !string.IsNullOrEmpty(nombreZip) &&
                                                 File.Exists(Path.Combine(RutaBovedaExtraccion, nombreZip));

                if (carpetaExiste || archivoEnExtraccionExiste)
                {
                    modulo.EstadoCache  = EstadoCacheModulo.EnCache;
                    modulo.TooltipCache = carpetaExiste
                        ? $"Extraído en caché: {nombreCarpeta}"
                        : $"Archivo en caché: {nombreZip}";
                    if (archivoEnExtraccionExiste && !carpetaExiste)
                        modulo.RutaCacheCarpeta = Path.Combine(RutaBovedaExtraccion, nombreZip);
                }
                else if (zipExiste)
                {
                    modulo.EstadoCache  = EstadoCacheModulo.ZipLocal;
                    modulo.TooltipCache = $"ZIP en caché: {nombreZip}";
                }
                else
                {
                    modulo.EstadoCache  = EstadoCacheModulo.NoDescargado;
                    modulo.TooltipCache = "No descargado";
                }

                modulo.EstaEnCache = modulo.EstadoCache != EstadoCacheModulo.NoDescargado;
            }
        }

        public bool BorrarCacheModulo(ModuloConfig modulo)
        {
            try
            {
                if (modulo.Versiones == null || modulo.Versiones.Count == 0) return false;

                var version = modulo.Versiones[0];

                string nombreZip = ObtenerArchivoZip(version);
                if (!string.IsNullOrEmpty(nombreZip))
                {
                    string rutaZip = Path.Combine(RutaBovedaZips, nombreZip);
                    if (File.Exists(rutaZip)) File.Delete(rutaZip);
                }

                string nombreCarpeta = ObtenerCarpetaExtraida(version);
                if (!string.IsNullOrEmpty(nombreCarpeta))
                {
                    string rutaCarpeta = Path.Combine(RutaBovedaExtraccion, nombreCarpeta);
                    if (Directory.Exists(rutaCarpeta)) Directory.Delete(rutaCarpeta, true);
                }

                modulo.EstadoCache  = EstadoCacheModulo.NoDescargado;
                modulo.EstaEnCache  = false;
                modulo.TooltipCache = "No cargado";

                return true;
            }
            catch
            {
                return false;
            }
        }

        // ── Helpers ─────────────────────────────────────────────────

        /// <summary>
        /// Lee "ArchivoDestino" del paso Descargar del pipeline.
        /// </summary>
        private static string ObtenerArchivoZip(ModuloVersion version)
            => LeerParametro(version, "Descargar", "ArchivoDestino");

        /// <summary>
        /// Lee "CarpetaDestinoTemp" del paso Extraer del pipeline.
        /// Si el JSON dice "Firmware.22.1.0", busca exactamente esa carpeta.
        /// </summary>
        private static string ObtenerCarpetaExtraida(ModuloVersion version)
            => LeerParametro(version, "Extraer", "CarpetaDestinoTemp");

        private static string LeerParametro(ModuloVersion version, string tipoAccion, string clave)
        {
            if (version?.PipelineInstalacion == null) return string.Empty;

            foreach (var paso in version.PipelineInstalacion)
            {
                if (!string.Equals(paso.TipoAccion, tipoAccion, StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    if (paso.Parametros.TryGetProperty(clave, out var prop))
                        return prop.GetString() ?? string.Empty;
                }
                catch (InvalidOperationException) { }
            }

            return string.Empty;
        }
    }
}