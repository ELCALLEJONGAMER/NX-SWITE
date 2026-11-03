using System.IO;
using NX_Swite.Core.Configuracion;
using NX_Swite.Services;

namespace NX_Swite.Core
{
    /// <summary>
    /// Copia de seguridad de las carpetas <c>atmosphere/contents/&lt;TITLE_ID&gt;/cheats</c>
    /// antes de preparar una actualización y restauración posterior a la SD.
    /// La copia queda en <c>Documentos\NX-Swite\Respaldos\_Trucos\{fecha}</c> y nunca se
    /// borra automáticamente. Al restaurar no sobrescribe archivos existentes.
    /// </summary>
    internal static class RespaldoTrucos
    {
        private const string CarpetaContents = "atmosphere/contents";

        /// <summary>
        /// Copia todas las carpetas <c>cheats</c> de la SD. Devuelve la carpeta creada,
        /// o null si no había trucos o la copia falló.
        /// </summary>
        public static string? Crear(string letraSD)
        {
            string contents = Path.Combine(letraSD, CarpetaContents);
            if (!Directory.Exists(contents)) return null;

            string destino = Path.Combine(ConfiguracionLocal.RutaRespaldosLlaves, "_Trucos",
                DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            try
            {
                int n = 0;
                foreach (var titulo in Directory.EnumerateDirectories(contents))
                {
                    string cheats = Path.Combine(titulo, "cheats");
                    if (!Directory.Exists(cheats)) continue;
                    n += CopiarCarpeta(cheats,
                        Path.Combine(destino, CarpetaContents, Path.GetFileName(titulo), "cheats"),
                        sobrescribir: true);
                }

                if (n == 0) return null;
                Logger.Info($"[RespaldoTrucos] {n} archivo(s) copiados en {destino}");
                return destino;
            }
            catch (Exception ex)
            {
                Logger.Warning($"[RespaldoTrucos] No se pudo crear la copia: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Devuelve a la SD los trucos que falten. Devuelve el número de archivos
        /// restaurados, o -1 si la SD no está disponible o hubo errores.
        /// </summary>
        public static async Task<int> RestaurarAsync(string carpeta, string letraSD, int timeoutMs = 20_000)
        {
            for (int t = 0; t < timeoutMs && !Directory.Exists(letraSD); t += 500)
                await Task.Delay(500);
            if (!Directory.Exists(letraSD) || !Directory.Exists(carpeta))
            {
                Logger.Warning($"[RespaldoTrucos] La SD {letraSD} no está disponible; copia conservada en {carpeta}");
                return -1;
            }

            return await Task.Run(() =>
            {
                try
                {
                    int n = CopiarCarpeta(carpeta, letraSD, sobrescribir: false);
                    Logger.Info($"[RespaldoTrucos] {n} archivo(s) restaurados en {letraSD}");
                    return n;
                }
                catch (Exception ex)
                {
                    Logger.Warning($"[RespaldoTrucos] Restauración incompleta: {ex.Message}. Copia en {carpeta}");
                    return -1;
                }
            });
        }

        private static int CopiarCarpeta(string origen, string destino, bool sobrescribir)
        {
            int n = 0;
            foreach (var f in Directory.EnumerateFiles(origen, "*", SearchOption.AllDirectories))
            {
                string dst = Path.Combine(destino, Path.GetRelativePath(origen, f));
                if (!sobrescribir && File.Exists(dst)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                File.Copy(f, dst, sobrescribir);
                n++;
            }
            return n;
        }
    }
}
