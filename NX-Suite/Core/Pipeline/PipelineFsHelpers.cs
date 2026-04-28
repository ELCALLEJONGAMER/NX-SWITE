using System.IO;

namespace NX_Suite.Core.Pipeline
{
    /// <summary>
    /// Helpers de filesystem compartidos por varios pasos del pipeline
    /// (copia recursiva, conteo de archivos, normalizacion de rutas).
    /// </summary>
    internal static class PipelineFsHelpers
    {
        /// <summary>
        /// Cuenta recursivamente todos los archivos dentro de <paramref name="directorio"/>.
        /// Se usa antes de copiar para calcular el total y poder reportar progreso preciso.
        /// </summary>
        public static int ContarArchivos(string directorio)
        {
            if (!Directory.Exists(directorio)) return 0;

            int total = Directory.GetFiles(directorio).Length;
            foreach (string sub in Directory.GetDirectories(directorio))
                total += ContarArchivos(sub);
            return total;
        }

        /// <summary>
        /// Copia recursivamente <paramref name="origen"/> a <paramref name="destino"/>,
        /// creando el destino si no existe y sobrescribiendo archivos existentes.
        /// El callback <paramref name="onArchivoCopado"/> se invoca con la ruta absoluta
        /// de cada archivo recien copiado (util para reportar progreso).
        /// </summary>
        public static void CopiarDirectorio(
            string origen,
            string destino,
            System.Action<string>? onArchivoCopado = null)
        {
            if (!Directory.Exists(destino)) Directory.CreateDirectory(destino);

            foreach (string archivo in Directory.GetFiles(origen))
            {
                File.Copy(archivo, Path.Combine(destino, Path.GetFileName(archivo)), overwrite: true);
                onArchivoCopado?.Invoke(archivo);
            }

            foreach (string sub in Directory.GetDirectories(origen))
                CopiarDirectorio(sub, Path.Combine(destino, Path.GetFileName(sub)), onArchivoCopado);
        }

        /// <summary>
        /// Combina la letra raiz de la SD con una ruta relativa del JSON
        /// (que tipicamente empieza por '/').
        /// </summary>
        public static string RutaSDAbsoluta(string letraSD, string rutaRelativa)
            => Path.Combine(letraSD, rutaRelativa.TrimStart('/'));
    }
}
