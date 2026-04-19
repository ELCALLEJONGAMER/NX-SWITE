using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using NX_Suite.Models;

namespace NX_Suite.Core
{
    public class GestorCache
    {
        public string RutaBovedaZips { get; private set; }
        public string RutaBovedaExtraccion { get; private set; }

        public GestorCache()
        {
            string carpetaAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            RutaBovedaZips       = Path.Combine(carpetaAppData, "NX-Suite", "Cache", "Zips");
            RutaBovedaExtraccion = Path.Combine(carpetaAppData, "NX-Suite", "Cache", "Extracted");

            if (!Directory.Exists(RutaBovedaZips))       Directory.CreateDirectory(RutaBovedaZips);
            if (!Directory.Exists(RutaBovedaExtraccion)) Directory.CreateDirectory(RutaBovedaExtraccion);
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

                if (carpetaExiste)
                {
                    modulo.EstadoCache  = EstadoCacheModulo.Preparado;
                    modulo.TooltipCache = $"Cache lista: {nombreCarpeta}";
                }
                else if (zipExiste)
                {
                    modulo.EstadoCache  = EstadoCacheModulo.ZipLocal;
                    modulo.TooltipCache = $"ZIP local: {nombreZip}";
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