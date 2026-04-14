using System;
using System.IO;
using System.Security.Cryptography;

namespace NX_Suite.Core
{
    public class SHA256Logic
    {
        /// <summary>
        /// Calcula el hash SHA256 de un archivo y lo devuelve como una cadena hexadecimal.
        /// </summary>
        /// <param name="rutaArchivo">Ruta completa al archivo en la SD o PC.</param>
        /// <returns>Hash en minúsculas o mensaje de error.</returns>
        public string ObtenerHashArchivo(string rutaArchivo)
        {
            if (!File.Exists(rutaArchivo))
                return "archivo_no_encontrado";

            try
            {
                using (var sha256 = SHA256.Create())
                {
                    using (var stream = File.Open(rutaArchivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        byte[] hashBytes = sha256.ComputeHash(stream);
                        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                    }
                }
            }
            catch (Exception)
            {
                return "error_lectura";
            }
        }

        /// <summary>
        /// Compara el hash de un archivo local contra el hash esperado de la nube.
        /// </summary>
        public bool ValidarIntegridad(string rutaLocal, string hashEsperado)
        {
            string hashReal = ObtenerHashArchivo(rutaLocal);
            return string.Equals(hashReal, hashEsperado, StringComparison.OrdinalIgnoreCase);
        }
    }
}