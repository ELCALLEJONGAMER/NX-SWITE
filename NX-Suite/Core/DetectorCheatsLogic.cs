using NX_Swite.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NX_Swite.Core
{
    /// <summary>
    /// Detecta cheats instalados en la SD inspeccionando únicamente
    /// <c>/atmosphere/contents/&lt;TITLE_ID&gt;/cheats/</c>. No calcula hash
    /// ni lee el contenido de los archivos: solo comprueba que la carpeta
    /// exista y contenga al menos un archivo.
    ///
    /// Se ejecuta una única vez al preparar la actualización (ver
    /// <c>MainWindow.LimpiezaSD.cs</c>) y NO depende de Internet: es puro
    /// análisis del sistema de archivos local.
    /// </summary>
    public class DetectorCheatsLogic
    {
        /// <summary>
        /// Analiza <c>{letraSD}\atmosphere\contents\</c> y devuelve un
        /// <see cref="CheatDetectado"/> por cada TitleId con carpeta
        /// <c>cheats</c> no vacía. El nombre del juego se resuelve vía
        /// <paramref name="resolverNombre"/> (típicamente
        /// <see cref="NX_Swite.Services.TitleDbService.ResolverNombre"/>),
        /// que ya debe estar preparado antes de llamar aquí.
        /// </summary>
        public List<CheatDetectado> Detectar(string letraSD, Func<string, string> resolverNombre)
        {
            var resultado = new List<CheatDetectado>();

            try
            {
                string rutaContents = Path.Combine(letraSD, "atmosphere", "contents");
                if (!Directory.Exists(rutaContents)) return resultado;

                foreach (var carpetaTitulo in Directory.EnumerateDirectories(rutaContents))
                {
                    string titleId = Path.GetFileName(carpetaTitulo);
                    if (!EsTitleIdValido(titleId)) continue;

                    string rutaCheats = Path.Combine(carpetaTitulo, "cheats");
                    if (!Directory.Exists(rutaCheats)) continue;

                    int cantidad = Directory.EnumerateFiles(rutaCheats).Count();
                    if (cantidad == 0) continue;

                    string titleIdNormalizado = titleId.ToUpperInvariant();
                    string nombre = resolverNombre(titleIdNormalizado);

                    resultado.Add(new CheatDetectado
                    {
                        TitleId = titleIdNormalizado,
                        NombreJuego = nombre,
                        CantidadArchivos = cantidad,
                        RutaRelativa = $"atmosphere/contents/{titleIdNormalizado}/cheats"
                    });
                }
            }
            catch
            {
                // No crítico: si falla la enumeración, simplemente no se detectan cheats.
            }

            return resultado;
        }

        private static bool EsTitleIdValido(string titleId)
        {
            if (string.IsNullOrEmpty(titleId) || titleId.Length != 16) return false;
            foreach (char c in titleId)
            {
                bool esHex = (c >= '0' && c <= '9') ||
                             (c >= 'a' && c <= 'f') ||
                             (c >= 'A' && c <= 'F');
                if (!esHex) return false;
            }
            return true;
        }
    }
}
