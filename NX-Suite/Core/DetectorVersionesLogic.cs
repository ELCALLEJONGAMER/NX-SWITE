using System;
using System.Collections.Generic;
using System.IO;
using NX_Suite.Models;

namespace NX_Suite.Core
{
    public class DetectorVersionesLogic
    {
        private readonly SHA256Logic _shaTool = new SHA256Logic();

        public (string Version, EstadoSdModulo EstadoSd) DeterminarEstadoInstalacion(string rutaRaizSD, ModuloConfig modulo)
        {
            if (modulo == null)
                return ("Desconocido", EstadoSdModulo.NoInstalado);

            modulo.ArchivosFaltantesDeteccion = new List<string>();

            if (modulo.FirmasDeteccion == null || modulo.FirmasDeteccion.Count == 0)
                return ("Desconocido", EstadoSdModulo.NoInstalado);

            foreach (var firma in modulo.FirmasDeteccion)
            {
                if (firma?.Archivos == null || firma.Archivos.Count == 0)
                    continue;

                // ── Fase 1: SHA256 como identificador de versión ─────────
                // Si algún archivo tiene SHA256 definido y no coincide,
                // esta firma no corresponde a la versión instalada → saltar.
                bool firmaIdentificada = true;
                foreach (var archivoFirma in firma.Archivos)
                {
                    if (string.IsNullOrWhiteSpace(archivoFirma.SHA256))
                        continue;

                    string rutaOriginal = archivoFirma.Ruta ?? string.Empty;
                    string rutaRelativa = rutaOriginal.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                    string rutaCompleta = Path.Combine(rutaRaizSD, rutaRelativa);

                    string hashActual = _shaTool.ObtenerHashArchivo(rutaCompleta);

                    if (hashActual == "archivo_no_encontrado" ||
                        hashActual == "error_lectura" ||
                        !hashActual.Equals(archivoFirma.SHA256, StringComparison.OrdinalIgnoreCase))
                    {
                        firmaIdentificada = false;
                        break;
                    }
                }

                if (!firmaIdentificada)
                    continue; // Este firma no es la versión instalada, probar la siguiente

                // ── Fase 2: Archivos de presencia como verificador de integridad ──
                // La versión fue identificada (SHA256 ok o no había SHA256).
                // Ahora verificamos que todos los archivos estén presentes.
                var archivosFaltantes = new List<string>();

                foreach (var archivoFirma in firma.Archivos)
                {
                    string rutaOriginal = archivoFirma.Ruta ?? string.Empty;
                    string rutaRelativa = rutaOriginal.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                    string rutaCompleta = Path.Combine(rutaRaizSD, rutaRelativa);

                    if (!ExisteRuta(rutaCompleta, rutaOriginal))
                        archivosFaltantes.Add(rutaOriginal);
                }

                modulo.ArchivosFaltantesDeteccion = archivosFaltantes;

                if (archivosFaltantes.Count == 0)
                    return (firma.Version, EstadoSdModulo.Instalado);
                else
                    return (firma.Version, EstadoSdModulo.ParcialmenteInstalado);
            }

            return ("No instalado", EstadoSdModulo.NoInstalado);
        }

        public string DeterminarVersionInstalada(string rutaRaizSD, ModuloConfig modulo)
        {
            return DeterminarEstadoInstalacion(rutaRaizSD, modulo).Version;
        }

        private static bool ExisteRuta(string rutaCompleta, string rutaOriginal)
        {
            if (rutaOriginal.EndsWith("/", StringComparison.Ordinal) ||
                rutaOriginal.EndsWith("\\", StringComparison.Ordinal))
            {
                return Directory.Exists(rutaCompleta);
            }

            return File.Exists(rutaCompleta) || Directory.Exists(rutaCompleta);
        }
    }
}