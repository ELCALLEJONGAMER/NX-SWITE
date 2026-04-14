using System;
using System.IO;
using System.Collections.Generic;
using NX_Suite.Models;

namespace NX_Suite.Core
{
    public class DetectorVersionesLogic
    {
        // 🚨 Usamos tu herramienta existente para no repetir código
        private readonly SHA256Logic _shaTool = new SHA256Logic();

        /// <summary>
        /// Analiza la SD para determinar qué versión de un módulo específico tiene instalada.
        /// </summary>
        public string DeterminarVersionInstalada(string rutaRaizSD, ModuloConfig modulo)
        {
            // 1. Si NO hay firmas, no podemos investigar. 
            // En lugar de decir "Versión no rastreable", devolvemos "Desconocido"
            if (modulo.FirmasDeteccion == null || modulo.FirmasDeteccion.Count == 0)
                return "Desconocido";

            // 2. Verificamos si al menos el primer archivo de la primera firma existe
            var primeraFirma = modulo.FirmasDeteccion[0];
            if (primeraFirma.Archivos != null && primeraFirma.Archivos.Count > 0)
            {
                string rutaVerificacion = Path.Combine(rutaRaizSD, primeraFirma.Archivos[0].Ruta.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(rutaVerificacion))
                    return "No instalado";
            }

            // 3. Si el archivo existe, el laboratorio (SHA256) hace el cotejo
            foreach (var firma in modulo.FirmasDeteccion)
            {
                bool coincidenciaTotal = true;
                foreach (var archivoFirma in firma.Archivos)
                {
                    string rutaCompleta = Path.Combine(rutaRaizSD, archivoFirma.Ruta.Replace('/', Path.DirectorySeparatorChar));
                    string hashActual = _shaTool.ObtenerHashArchivo(rutaCompleta);

                    if (hashActual == "archivo_no_encontrado" || hashActual == "error_lectura" ||
                        !hashActual.Equals(archivoFirma.SHA256, StringComparison.OrdinalIgnoreCase))
                    {
                        coincidenciaTotal = false;
                        break;
                    }
                }

                if (coincidenciaTotal) return firma.Version;
            }

            // Si no coincide con ninguna firma oficial del JSON:
            return "Desconocido";
        }
    }
}