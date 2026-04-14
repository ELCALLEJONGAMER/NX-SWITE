using System;
using System.IO;
using NX_Suite.Models;

namespace NX_Suite.Core
{
    public class DetectorVersionesLogic
    {
        private readonly SHA256Logic _shaTool = new SHA256Logic();

        public (string Version, EstadoSdModulo EstadoSd) DeterminarEstadoInstalacion(string rutaRaizSD, ModuloConfig modulo)
        {
            if (modulo?.FirmasDeteccion == null || modulo.FirmasDeteccion.Count == 0)
                return ("Desconocido", EstadoSdModulo.NoInstalado);

            bool existeAlgunaEvidencia = false;

            foreach (var firma in modulo.FirmasDeteccion)
            {
                if (firma?.Archivos == null || firma.Archivos.Count == 0)
                    continue;

                bool firmaCoincide = true;
                bool algunaRutaExiste = false;

                foreach (var archivoFirma in firma.Archivos)
                {
                    string rutaCompleta = Path.Combine(
                        rutaRaizSD,
                        archivoFirma.Ruta.Replace('/', Path.DirectorySeparatorChar));

                    if (!File.Exists(rutaCompleta))
                    {
                        firmaCoincide = false;
                        continue;
                    }

                    algunaRutaExiste = true;
                    existeAlgunaEvidencia = true;

                    string hashActual = _shaTool.ObtenerHashArchivo(rutaCompleta);

                    if (hashActual == "archivo_no_encontrado" ||
                        hashActual == "error_lectura" ||
                        !hashActual.Equals(archivoFirma.SHA256, StringComparison.OrdinalIgnoreCase))
                    {
                        firmaCoincide = false;
                    }
                }

                if (firmaCoincide && algunaRutaExiste)
                    return (firma.Version, EstadoSdModulo.Instalado);
            }

            return existeAlgunaEvidencia
                ? ("Desconocido", EstadoSdModulo.ParcialmenteInstalado)
                : ("No instalado", EstadoSdModulo.NoInstalado);
        }

        public string DeterminarVersionInstalada(string rutaRaizSD, ModuloConfig modulo)
        {
            return DeterminarEstadoInstalacion(rutaRaizSD, modulo).Version;
        }
    }
}