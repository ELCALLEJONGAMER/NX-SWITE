using System.IO;
using NX_Swite.Core.Configuracion;
using NX_Swite.Services;

namespace NX_Swite.Core
{
    /// <summary>
    /// Copia de seguridad bruta de <c>switch/prod.keys</c>, <c>switch/title.keys</c> y la carpeta
    /// <c>atmosphere/automatic_backups</c> completa antes de una operación destructiva.
    /// No depende del serial ni de la verificación de llaves: protege también SDs
    /// con varias consolas o con prod.keys sin BISKEYS. Al restaurar nunca
    /// sobrescribe archivos que ya existan en la SD.
    /// </summary>
    internal static class InstantaneaLlaves
    {
        private static readonly string[] ArchivosSwitch = { "prod.keys", "title.keys" };
        private const string CarpetaBackups = "atmosphere/automatic_backups";

        /// <summary>True si la SD contiene algo que proteger.</summary>
        public static bool HayAlgoQueProteger(string letraSD) =>
            ArchivosSwitch.Any(a => File.Exists(Path.Combine(letraSD, "switch", a))) ||
            (Directory.Exists(Path.Combine(letraSD, CarpetaBackups)) &&
             Directory.EnumerateFiles(Path.Combine(letraSD, CarpetaBackups), "*", SearchOption.AllDirectories).Any());

        /// <summary>
        /// Copia los archivos a <c>Documentos\NX-Swite\Respaldos\_Instantaneas\{fecha}</c>.
        /// Devuelve la carpeta creada, o null si no había nada o falló la copia.
        /// </summary>
        public static string? Crear(string letraSD)
        {
            if (!HayAlgoQueProteger(letraSD)) return null;

            string destino = Path.Combine(ConfiguracionLocal.RutaRespaldosLlaves, "_Instantaneas",
                DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            try
            {
                foreach (var a in ArchivosSwitch)
                {
                    string origen = Path.Combine(letraSD, "switch", a);
                    if (!File.Exists(origen)) continue;
                    Directory.CreateDirectory(Path.Combine(destino, "switch"));
                    File.Copy(origen, Path.Combine(destino, "switch", a), true);
                }

                string carpeta = Path.Combine(letraSD, CarpetaBackups);
                if (Directory.Exists(carpeta))
                    CopiarCarpeta(carpeta, Path.Combine(destino, CarpetaBackups), sobrescribir: true);

                Logger.Info($"[InstantaneaLlaves] Copia creada en {destino}");
                return destino;
            }
            catch (Exception ex)
            {
                Logger.Warning($"[InstantaneaLlaves] No se pudo crear la copia: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Espera a que la SD esté accesible y copia de vuelta los archivos que falten.
        /// Devuelve el número de archivos restaurados, o -1 si la SD no apareció o hubo errores.
        /// </summary>
        public static async Task<int> RestaurarAsync(string carpeta, string letraSD, int timeoutMs = 20_000)
        {
            for (int t = 0; t < timeoutMs && !Directory.Exists(letraSD); t += 500)
                await Task.Delay(500);
            if (!Directory.Exists(letraSD))
            {
                Logger.Warning($"[InstantaneaLlaves] La SD {letraSD} no está disponible; copia conservada en {carpeta}");
                return -1;
            }

            return await Task.Run(() =>
            {
                try
                {
                    int n = CopiarCarpeta(carpeta, letraSD, sobrescribir: false);
                    Logger.Info($"[InstantaneaLlaves] {n} archivo(s) restaurados en {letraSD}");
                    return n;
                }
                catch (Exception ex)
                {
                    Logger.Warning($"[InstantaneaLlaves] Restauración incompleta: {ex.Message}. Copia en {carpeta}");
                    return -1;
                }
            });
        }

        private static int CopiarCarpeta(string origen, string destino, bool sobrescribir)
        {
            int n = 0;
            foreach (var f in Directory.EnumerateFiles(origen, "*", SearchOption.AllDirectories))
            {
                string rel  = Path.GetRelativePath(origen, f);
                string dst  = Path.Combine(destino, rel);
                if (!sobrescribir && File.Exists(dst)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                File.Copy(f, dst, sobrescribir);
                n++;
            }
            return n;
        }
    }
}
