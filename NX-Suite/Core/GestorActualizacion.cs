using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace NX_Suite.Core
{
    /// <summary>
    /// Lógica estática de auto-actualización: comparación de versiones,
    /// descarga del ZIP de actualización y lanzamiento del updater externo.
    /// </summary>
    public static class GestorActualizacion
    {
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(10) };

        // ?????????????????????????????????????????????????????????????????
        //  Comparación de versiones
        // ?????????????????????????????????????????????????????????????????

        /// <summary>
        /// Devuelve <c>true</c> si <paramref name="remota"/> es estrictamente
        /// mayor que <paramref name="actual"/> usando comparación semántica.
        /// </summary>
        public static bool EsVersionNueva(string actual, string remota)
        {
            if (string.IsNullOrWhiteSpace(remota)) return false;

            if (Version.TryParse(Normalizar(actual), out var vActual) &&
                Version.TryParse(Normalizar(remota),  out var vRemota))
                return vRemota > vActual;

            return false;
        }

        private static string Normalizar(string v)
        {
            v = v.TrimStart('v', 'V').Trim();
            // Garantizar al menos 3 componentes para Version.TryParse
            var partes = v.Split('.');
            while (partes.Length < 3) { v += ".0"; partes = v.Split('.'); }
            return v;
        }

        // ?????????????????????????????????????????????????????????????????
        //  Descarga
        // ?????????????????????????????????????????????????????????????????

        /// <summary>
        /// Descarga el ZIP de actualización a una ruta temporal y reporta progreso.
        /// Devuelve la ruta local del ZIP descargado.
        /// </summary>
        public static async Task<string> DescargarActualizacionAsync(
            string url,
            IProgress<(double pct, string msg)>? progreso = null)
        {
            string zipPath = Path.Combine(Path.GetTempPath(), "NX-Suite-Update.zip");

            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            long total = response.Content.Headers.ContentLength ?? -1;
            long read  = 0;

            using var input  = await response.Content.ReadAsStreamAsync();
            using var output = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            int bytes;

            while ((bytes = await input.ReadAsync(buffer)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, bytes));
                read += bytes;

                if (total > 0 && progreso != null)
                {
                    double pct = (double)read / total * 100.0;
                    double mbR = read  / 1_048_576.0;
                    double mbT = total / 1_048_576.0;
                    progreso.Report((pct, $"Descargando {mbR:F1} MB / {mbT:F1} MB  ({pct:F0}%)"));
                }
            }

            return zipPath;
        }

        // ?????????????????????????????????????????????????????????????????
        //  Lanzar updater externo
        // ?????????????????????????????????????????????????????????????????

        /// <summary>
        /// Lanza <c>NX-Suite.Updater.exe</c> pasándole los argumentos necesarios
        /// para que espere a que cierre la app, extraiga el ZIP y la relance.
        /// </summary>
        public static void LanzarActualizador(
            string updaterPath,
            string zipPath,
            string appDir,
            string mainExePath)
        {
            if (!File.Exists(updaterPath))
                throw new FileNotFoundException(
                    $"No se encontró el actualizador en:\n{updaterPath}\n\n" +
                    "Asegúrate de que NX-Suite.Updater.exe está junto al ejecutable principal.",
                    updaterPath);

            int pid = System.Diagnostics.Process.GetCurrentProcess().Id;

            // Recortar la barra final de appDir: en Windows "ruta\" dentro de comillas
            // hace que \" se interprete como comilla escapada, fusionando los argumentos.
            string appDirArg = appDir.TrimEnd('\\', '/');

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName        = updaterPath,
                Arguments       = $"\"{zipPath}\" \"{appDirArg}\" \"{mainExePath}\" {pid}",
                UseShellExecute = false,
                CreateNoWindow  = true,
            };

            System.Diagnostics.Process.Start(psi);
        }
    }
}
