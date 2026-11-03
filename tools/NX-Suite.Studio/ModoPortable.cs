using System.IO;

namespace NX_Suite.Studio
{
    /// <summary>
    /// Modo USB: si junto al ejecutable existe el archivo <c>portable.txt</c>
    /// (o el ejecutable está en una unidad extraíble), los datos de NX-STUDIO
    /// (token, última revisión) se guardan en <c>datos\</c> junto al .exe en lugar
    /// de %AppData%. También localiza el repositorio y un Git portátil en la USB.
    /// </summary>
    internal static class ModoPortable
    {
        private static readonly string CarpetaExe = AppContext.BaseDirectory;

        public static bool Activo { get; } =
            File.Exists(Path.Combine(CarpetaExe, "portable.txt")) || EsUnidadExtraible(CarpetaExe);

        /// <summary>Carpeta donde guardar config y estado (USB en modo portable, %AppData% si no).</summary>
        public static string CarpetaDatos(Environment.SpecialFolder carpetaNormal) =>
            Activo
                ? Path.Combine(CarpetaExe, "datos")
                : Path.Combine(Environment.GetFolderPath(carpetaNormal), "NX-Studio");

        /// <summary>Repositorio clonado junto al ejecutable (USB:\NX-SWITE), si existe.</summary>
        public static string? RepoJuntoAlExe()
        {
            string ruta = Path.Combine(CarpetaExe, "NX-SWITE");
            if (EsRepo(ruta)) return ruta;
            try
            {
                string recordado = File.ReadAllText(ArchivoRepo).Trim();
                return EsRepo(recordado) ? recordado : null;
            }
            catch { return null; }
        }

        public static bool EsRepo(string ruta) => File.Exists(Path.Combine(ruta, "catalogo", "catalogo.json"));

        private static string ArchivoRepo => Path.Combine(CarpetaDatos(Environment.SpecialFolder.ApplicationData), "repo.txt");

        /// <summary>Guarda la ruta del repositorio elegida para los siguientes arranques.</summary>
        public static void RecordarRepo(string ruta)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ArchivoRepo)!);
                File.WriteAllText(ArchivoRepo, ruta);
            }
            catch { /* Solo es una comodidad */ }
        }

        public const string UrlRepo = "https://github.com/ELCALLEJONGAMER/NX-SWITE.git";

        /// <summary>Clona el repositorio en NX-SWITE junto al ejecutable. Devuelve la ruta o null con el error.</summary>
        public static string? ClonarRepo(out string error)
        {
            string destino = Path.Combine(CarpetaExe, "NX-SWITE");
            error = "";
            try
            {
                if (Directory.Exists(destino) && Directory.EnumerateFileSystemEntries(destino).Any())
                {
                    error = $"La carpeta {destino} ya existe pero no contiene el catálogo. Bórrala o elige otra carpeta.";
                    return null;
                }
                var psi = new System.Diagnostics.ProcessStartInfo(RutaGit(), $"clone --depth 50 {UrlRepo} \"{destino}\"")
                {
                    UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true,
                };
                using var p = System.Diagnostics.Process.Start(psi)!;
                string salida = p.StandardError.ReadToEnd();
                p.WaitForExit();
                if (p.ExitCode == 0 && EsRepo(destino)) return destino;
                error = "No se pudo clonar el repositorio:\n" + salida.Trim();
            }
            catch (Exception ex)
            {
                error = "No se pudo ejecutar Git (" + ex.Message + ").\nInstala Git o copia PortableGit en la carpeta 'PortableGit' junto al ejecutable.";
            }
            return null;
        }

        /// <summary>git.exe de PortableGit junto al ejecutable; si no existe, "git" del PATH.</summary>
        public static string RutaGit()
        {
            string portable = Path.Combine(CarpetaExe, "PortableGit", "cmd", "git.exe");
            return File.Exists(portable) ? portable : "git";
        }

        private static bool EsUnidadExtraible(string ruta)
        {
            try
            {
                string? raiz = Path.GetPathRoot(ruta);
                return raiz != null && new DriveInfo(raiz).DriveType == DriveType.Removable;
            }
            catch { return false; }
        }
    }
}
