using System.IO;

namespace NX_Suite.Core.Pipeline
{
    /// <summary>
    /// Helpers de filesystem compartidos por varios pasos del pipeline
    /// (copia recursiva, normalización de rutas relativas del JSON).
    /// </summary>
    internal static class PipelineFsHelpers
    {
        /// <summary>
        /// Copia recursivamente <paramref name="origen"/> a <paramref name="destino"/>,
        /// creando el destino si no existe y sobrescribiendo archivos existentes.
        /// </summary>
        public static void CopiarDirectorio(string origen, string destino)
        {
            if (!Directory.Exists(destino)) Directory.CreateDirectory(destino);

            foreach (string file in Directory.GetFiles(origen))
                File.Copy(file, Path.Combine(destino, Path.GetFileName(file)), true);

            foreach (string dir in Directory.GetDirectories(origen))
                CopiarDirectorio(dir, Path.Combine(destino, Path.GetFileName(dir)));
        }

        /// <summary>
        /// Combina la letra raíz de la SD con una ruta relativa del JSON
        /// (que típicamente empieza por '/').
        /// </summary>
        public static string RutaSDAbsoluta(string letraSD, string rutaRelativa)
            => Path.Combine(letraSD, rutaRelativa.TrimStart('/'));
    }
}
