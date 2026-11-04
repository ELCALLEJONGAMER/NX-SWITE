using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace NX_Swite.Core
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
            string zipPath = Path.Combine(Path.GetTempPath(), "NX-Swite-Update.zip");

            // Simulación local (actualizacion-prueba.json): la url es un ZIP en disco
            if (File.Exists(url))
            {
                File.Copy(url, zipPath, overwrite: true);
                progreso?.Report((100, "ZIP local copiado (simulación)"));
                return zipPath;
            }

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
        /// Extrae a una carpeta temporal el actualizador que viene dentro del ZIP
        /// nuevo, para que la actualización la haga siempre el updater más reciente
        /// y no el instalado. Devuelve su ruta o null si el ZIP no lo trae.
        /// </summary>
        public static string? ExtraerUpdaterDelZip(string zipPath)
        {
            try
            {
                string destino = Path.Combine(Path.GetTempPath(), "NX-Swite-Updater");
                if (Directory.Exists(destino)) Directory.Delete(destino, true);
                Directory.CreateDirectory(destino);

                using var zip = System.IO.Compression.ZipFile.OpenRead(zipPath);
                string prefijo = Path.GetFileNameWithoutExtension(NX_Swite.Core.Configuracion.ConfiguracionLocal.NombreUpdater) + ".";
                foreach (var e in zip.Entries.Where(e => e.Name.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase)
                                                        && e.FullName.IndexOfAny(new[] { '/', '\\' }) < 0))
                    e.ExtractToFile(Path.Combine(destino, e.Name), overwrite: true);

                string exe = Path.Combine(destino, NX_Swite.Core.Configuracion.ConfiguracionLocal.NombreUpdater);
                return File.Exists(exe) ? exe : null;
            }
            catch (Exception ex)
            {
                NX_Swite.Services.Logger.Warning($"No se pudo usar el actualizador del ZIP; se usa el instalado. {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Lanza <c>NX-Swite.Updater.exe</c> pasándole los argumentos necesarios
        /// para que espere a que cierre la app, extraiga el ZIP y la relance.
        /// </summary>
        public static void LanzarActualizador(
            string updaterPath,
            string zipPath,
            string appDir,
            string mainExePath,
            string? logoPath = null)
        {
            if (!File.Exists(updaterPath))
                throw new FileNotFoundException(
                    $"No se encontró el actualizador en:\n{updaterPath}\n\n" +
                    "Asegúrate de que NX-Swite.Updater.exe está junto al ejecutable principal.",
                    updaterPath);

            int pid = System.Diagnostics.Process.GetCurrentProcess().Id;

            // Recortar la barra final de appDir: en Windows "ruta\" dentro de comillas
            // hace que \" se interprete como comilla escapada, fusionando los argumentos.
            string appDirArg = appDir.TrimEnd('\\', '/');

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName        = updaterPath,
                Arguments       = $"\"{zipPath}\" \"{appDirArg}\" \"{mainExePath}\" {pid} \"{logoPath ?? string.Empty}\"",
                UseShellExecute = false,
                CreateNoWindow  = true,
            };

            System.Diagnostics.Process.Start(psi);
        }
    }
}
