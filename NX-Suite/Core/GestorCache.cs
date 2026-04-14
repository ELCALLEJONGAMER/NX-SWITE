using System;
using System.IO;
using System.Collections.Generic;
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

            // Asegurar que las carpetas existan desde el inicio
            if (!Directory.Exists(RutaBovedaZips)) Directory.CreateDirectory(RutaBovedaZips);
            if (!Directory.Exists(RutaBovedaExtraccion)) Directory.CreateDirectory(RutaBovedaExtraccion);
        }

        /// <summary>
        /// Recorre una lista de módulos y enciende o apaga su indicador de "En Caché"
        /// basándose en si el archivo ZIP existe en la PC.
        /// </summary>
        public void ActualizarEstadoCache(IEnumerable<ModuloConfig> modulos)
        {
            if (modulos == null) return;

            foreach (var modulo in modulos)
            {
                if (modulo.Versiones != null && modulo.Versiones.Count > 0)
                {
                    string nombreArchivoZip = $"{modulo.Id}_v{modulo.Versiones[0].Version}.zip";
                    string rutaZipLocal = Path.Combine(RutaBovedaZips, nombreArchivoZip);

                    modulo.EstaEnCache = File.Exists(rutaZipLocal);
                }
            }
        }

        /// <summary>
        /// Borra el ZIP y la carpeta descomprimida de un módulo específico.
        /// </summary>
        public bool BorrarCacheModulo(ModuloConfig modulo)
        {
            try
            {
                if (modulo.Versiones == null || modulo.Versiones.Count == 0) return false;

                // 1. Borrar el ZIP
                string nombreZip = $"{modulo.Id}_v{modulo.Versiones[0].Version}.zip";
                string rutaZip = Path.Combine(RutaBovedaZips, nombreZip);
                if (File.Exists(rutaZip)) File.Delete(rutaZip);

                // 2. Borrar la carpeta descomprimida
                string nombreCarpeta = $"{modulo.Id}_v{modulo.Versiones[0].Version}";
                string rutaCarpeta = Path.Combine(RutaBovedaExtraccion, nombreCarpeta);
                if (Directory.Exists(rutaCarpeta)) Directory.Delete(rutaCarpeta, true);

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}