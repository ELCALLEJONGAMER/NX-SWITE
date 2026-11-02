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
            return File.Exists(Path.Combine(ruta, "catalogo", "catalogo.json")) ? ruta : null;
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
