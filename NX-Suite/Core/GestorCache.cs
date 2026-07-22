using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using NX_Swite.Core.Configuracion;
using NX_Swite.Models;

namespace NX_Swite.Core
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
            RutaBovedaZips       = ConfiguracionLocal.RutaCacheZips;
            RutaBovedaExtraccion = ConfiguracionLocal.RutaCacheExtraccion;
            RutaCacheGist        = ConfiguracionLocal.RutaCacheGist;
            RutaCacheIconos      = ConfiguracionLocal.RutaCacheIconos;
            RutaCacheSonidos     = ConfiguracionLocal.RutaCacheSonidos;

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

        // ── ETag del Gist (sidecar junto al JSON) ────────────────────────

        private string RutaETagGist => RutaCacheGist + ".etag";

        /// <summary>
        /// Guarda el ETag devuelto por el servidor tras la última descarga exitosa.
        /// </summary>
        public async Task GuardarETagGistAsync(string etag)
        {
            if (string.IsNullOrWhiteSpace(etag)) return;
            try { await File.WriteAllTextAsync(RutaETagGist, etag); } catch { }
        }

        /// <summary>
        /// Carga el ETag guardado localmente, o null si no existe.
        /// </summary>
        public async Task<string?> CargarETagGistAsync()
        {
            try
            {
                if (!File.Exists(RutaETagGist)) return null;
                return (await File.ReadAllTextAsync(RutaETagGist)).Trim();
            }
            catch { return null; }
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

                // ── Actualizar estado de caché POR VERSIÓN ────────────────
                foreach (var ver in modulo.Versiones)
                {
                    string nombreZip     = ObtenerArchivoZip(ver);
                    string nombreCarpeta = ObtenerCarpetaExtraida(ver);

                    string rutaZip     = string.IsNullOrEmpty(nombreZip)
                                         ? string.Empty
                                         : Path.Combine(RutaBovedaZips, nombreZip);
                    string rutaCarpeta = string.IsNullOrEmpty(nombreCarpeta)
                                         ? string.Empty
                                         : Path.Combine(RutaBovedaExtraccion, nombreCarpeta);

                    bool zipEx = !string.IsNullOrEmpty(rutaZip) && File.Exists(rutaZip)
                                 && VersionSidecarCoincide(rutaZip, ver.Version);

                    // Detectar también archivo directo descargado a Extracted (sin ZIP)
                    bool archivoDirectoEx = !string.IsNullOrEmpty(nombreZip) &&
                                            File.Exists(Path.Combine(RutaBovedaExtraccion, nombreZip)) &&
                                            VersionSidecarCoincide(Path.Combine(RutaBovedaExtraccion, nombreZip), ver.Version);
                    bool carpetaEx = (!string.IsNullOrEmpty(rutaCarpeta) && Directory.Exists(rutaCarpeta))
                                     || archivoDirectoEx;

                    ver.TieneZipCache      = zipEx;
                    ver.TieneCarpetaCache  = carpetaEx;
                    ver.RutaCacheZipVer    = rutaZip;
                    ver.RutaCacheCarpetaVer = (carpetaEx && !Directory.Exists(rutaCarpeta) && archivoDirectoEx)
                        ? Path.Combine(RutaBovedaExtraccion, nombreZip)
                        : rutaCarpeta;
                }

                // ── Estado a nivel de módulo (usa Versiones[0] para la tarjeta del catálogo) ──
                var v0 = modulo.Versiones[0];
                modulo.RutaCacheZip     = v0.RutaCacheZipVer;
                modulo.RutaCacheCarpeta = v0.RutaCacheCarpetaVer;

                if (v0.TieneCarpetaCache)
                {
                    modulo.EstadoCache  = EstadoCacheModulo.EnCache;
                    modulo.TooltipCache = "Extraído en caché";
                }
                else if (v0.TieneZipCache)
                {
                    modulo.EstadoCache  = EstadoCacheModulo.ZipLocal;
                    modulo.TooltipCache = "ZIP en caché";
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

                bool eliminoAlgo = false;

                foreach (var version in modulo.Versiones)
                {
                    // 1. ZIP en bóveda de zips
                    string nombreZip = ObtenerArchivoZip(version);
                    if (!string.IsNullOrEmpty(nombreZip))
                    {
                        string rutaZip = Path.Combine(RutaBovedaZips, nombreZip);
                        if (File.Exists(rutaZip)) { File.Delete(rutaZip); eliminoAlgo = true; }
                        string rutaZipVer  = rutaZip + ".version";
                            if (File.Exists(rutaZipVer))  File.Delete(rutaZipVer);
                            // El sidecar .destino vive en RutaBovedaExtraccion; se elimina
                            // junto con la carpeta extraída en el bloque siguiente.
                    }

                    // 2. Carpeta extraída + sidecar .version + sidecar .destino
                    string nombreCarpeta = ObtenerCarpetaExtraida(version);
                    if (!string.IsNullOrEmpty(nombreCarpeta))
                    {
                        string rutaCarpeta = Path.Combine(RutaBovedaExtraccion, nombreCarpeta);
                        if (Directory.Exists(rutaCarpeta)) { Directory.Delete(rutaCarpeta, true); eliminoAlgo = true; }
                        string rutaCarpetaVer = rutaCarpeta + ".version";
                        if (File.Exists(rutaCarpetaVer)) File.Delete(rutaCarpetaVer);
                        // Eliminar el sidecar .destino (vive en RutaBovedaExtraccion,
                        // con nombre <archivoZip>.destino). Buscamos por contenido.
                        if (!string.IsNullOrEmpty(nombreZip))
                        {
                            string rutaDestinoSidecar = Path.Combine(RutaBovedaExtraccion, nombreZip + ".destino");
                            if (File.Exists(rutaDestinoSidecar)) File.Delete(rutaDestinoSidecar);
                        }
                    }

                    // 3. Archivo directo en carpeta de extracción (descarga sin zip)
                    if (!string.IsNullOrEmpty(nombreZip))
                    {
                        string rutaArchivoExtraccion = Path.Combine(RutaBovedaExtraccion, nombreZip);
                        if (File.Exists(rutaArchivoExtraccion)) { File.Delete(rutaArchivoExtraccion); eliminoAlgo = true; }
                        string rutaArchivoExtraccionVer = rutaArchivoExtraccion + ".version";
                        if (File.Exists(rutaArchivoExtraccionVer)) File.Delete(rutaArchivoExtraccionVer);
                    }
                }

                modulo.EstadoCache  = EstadoCacheModulo.NoDescargado;
                modulo.EstaEnCache  = false;
                modulo.TooltipCache = "No descargado";

                return eliminoAlgo;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Devuelve el peso total en bytes de todos los archivos ZIP en la bóveda de caché.
        /// Excluye los sidecars <c>.version</c>.
        /// </summary>
        public long CalcularPesoZips()
        {
            if (!Directory.Exists(RutaBovedaZips)) return 0;
            long total = 0;
            foreach (var f in Directory.EnumerateFiles(RutaBovedaZips))
                if (!f.EndsWith(".version", StringComparison.OrdinalIgnoreCase))
                    total += new FileInfo(f).Length;
            return total;
        }

        /// <summary>
        /// Devuelve el peso total en bytes de todo el contenido extraído en la bóveda de extracción.
        /// Excluye los sidecars <c>.version</c>.
        /// </summary>
        public long CalcularPesoExtraccion()
        {
            if (!Directory.Exists(RutaBovedaExtraccion)) return 0;
            long total = 0;
            foreach (var f in Directory.EnumerateFiles(RutaBovedaExtraccion, "*", SearchOption.AllDirectories))
                if (!f.EndsWith(".version", StringComparison.OrdinalIgnoreCase)
                    && !f.EndsWith(".destino", StringComparison.OrdinalIgnoreCase))
                    total += new FileInfo(f).Length;
            return total;
        }

        /// <summary>
        /// Borra TODOS los archivos y carpetas de ambas bóvedas (ZIPs y extracción),
        /// incluyendo sidecars <c>.version</c> y archivos huérfanos de módulos
        /// renombrados o eliminados del catálogo remoto.
        /// </summary>
        public void LimpiarTodaLaBoveda()
        {
            BorrarContenidoCarpeta(RutaBovedaZips);
            BorrarContenidoCarpeta(RutaBovedaExtraccion);
        }

        private static void BorrarContenidoCarpeta(string ruta)
        {
            if (!Directory.Exists(ruta)) return;
            foreach (var f in Directory.EnumerateFiles(ruta))
                try { File.Delete(f); } catch { }
            foreach (var d in Directory.EnumerateDirectories(ruta))
                try { Directory.Delete(d, true); } catch { }
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

        /// <summary>
        /// Comprueba que el sidecar <c>&lt;rutaArchivo&gt;.version</c> existe y
        /// contiene exactamente <paramref name="versionEsperada"/>.
        /// Si el sidecar no existe se devuelve <c>false</c> (caché sin versión
        /// conocida se trata como obsoleta para forzar re-descarga).
        /// </summary>
        private static bool VersionSidecarCoincide(string rutaArchivo, string versionEsperada)
        {
            if (string.IsNullOrEmpty(versionEsperada)) return true; // sin versión en JSON → no validar
            string rutaSidecar = rutaArchivo + ".version";
            if (!File.Exists(rutaSidecar)) return false;
            try
            {
                string versionCacheada = File.ReadAllText(rutaSidecar).Trim();
                return string.Equals(versionCacheada, versionEsperada, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }
    }
}