using System;
using System.Collections.Generic;
using System.IO;
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
            RutaBovedaZips = Path.Combine(carpetaAppData, "NX-Suite", "Cache", "Zips");
            RutaBovedaExtraccion = Path.Combine(carpetaAppData, "NX-Suite", "Cache", "Extracted");

            if (!Directory.Exists(RutaBovedaZips)) Directory.CreateDirectory(RutaBovedaZips);
            if (!Directory.Exists(RutaBovedaExtraccion)) Directory.CreateDirectory(RutaBovedaExtraccion);
        }

        public void ActualizarEstadoCache(IEnumerable<ModuloConfig> modulos)
        {
            if (modulos == null)
                return;

            foreach (var modulo in modulos)
            {
                if (modulo?.Versiones == null || modulo.Versiones.Count == 0)
                    continue;

                string version = modulo.Versiones[0].Version;
                string nombreArchivoZip = $"{modulo.Id}_v{version}.zip";
                string nombreCarpeta = $"{modulo.Id}_v{version}";

                string rutaZipLocal = Path.Combine(RutaBovedaZips, nombreArchivoZip);
                string rutaCarpetaLocal = Path.Combine(RutaBovedaExtraccion, nombreCarpeta);

                bool zipExiste = File.Exists(rutaZipLocal);
                bool carpetaExiste = Directory.Exists(rutaCarpetaLocal);

                modulo.RutaCacheZip = rutaZipLocal;
                modulo.RutaCacheCarpeta = rutaCarpetaLocal;

                if (zipExiste && carpetaExiste)
                {
                    modulo.EstadoCache = EstadoCacheModulo.Preparado;
                    modulo.TooltipCache = $"Cache local: {nombreCarpeta}";
                }
                else if (zipExiste)
                {
                    modulo.EstadoCache = EstadoCacheModulo.ZipLocal;
                    modulo.TooltipCache = $"ZIP local: {nombreArchivoZip}";
                }
                else
                {
                    modulo.EstadoCache = EstadoCacheModulo.NoDescargado;
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

                string nombreZip = $"{modulo.Id}_v{modulo.Versiones[0].Version}.zip";
                string rutaZip = Path.Combine(RutaBovedaZips, nombreZip);
                if (File.Exists(rutaZip)) File.Delete(rutaZip);

                string nombreCarpeta = $"{modulo.Id}_v{modulo.Versiones[0].Version}";
                string rutaCarpeta = Path.Combine(RutaBovedaExtraccion, nombreCarpeta);
                if (Directory.Exists(rutaCarpeta)) Directory.Delete(rutaCarpeta, true);

                modulo.EstadoCache = EstadoCacheModulo.NoDescargado;
                modulo.EstaEnCache = false;
                modulo.TooltipCache = "No descargado";

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}