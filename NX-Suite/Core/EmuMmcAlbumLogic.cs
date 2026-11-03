using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace NX_Swite.Core
{
    public enum OrigenAlbum { MicroSd, Consola }

    public sealed class ArchivoAlbum
    {
        public string      Ruta    { get; init; } = string.Empty;
        public DateTime    Fecha   { get; init; }
        public bool        EsVideo { get; init; }
        public OrigenAlbum Origen  { get; init; }
        public long        Tamano  { get; init; }
    }

    /// <summary>
    /// Álbum de la consola: capturas (.jpg) y vídeos (.mp4) guardados en
    /// <c>{nintendo_path}/Album</c> de la microSD o en <c>USER:/Album</c> de la emuMMC.
    /// </summary>
    public static class EmuMmcAlbumLogic
    {
        private static readonly string[] Extensiones = { ".jpg", ".jpeg", ".mp4" };

        public static bool EsArchivoAlbum(string ruta)
            => Extensiones.Contains(Path.GetExtension(ruta), StringComparer.OrdinalIgnoreCase);

        public static bool EsVideo(string ruta)
            => Path.GetExtension(ruta).Equals(".mp4", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// La consola nombra los archivos <c>YYYYMMDDHHMMSSnn-&lt;id&gt;.jpg</c>.
        /// Devuelve null si el nombre no sigue ese formato.
        /// </summary>
        public static DateTime? FechaDesdeNombre(string nombreArchivo)
        {
            string n = Path.GetFileNameWithoutExtension(nombreArchivo);
            return n.Length >= 14 && DateTime.TryParseExact(n[..14], "yyyyMMddHHmmss",
                       CultureInfo.InvariantCulture, DateTimeStyles.None, out var f)
                ? f : null;
        }

        /// <summary>Nombre legible para exportar al PC (p.ej. "2024-05-31 18.42.07.jpg").</summary>
        public static string NombreExportacion(ArchivoAlbum a, int indice = 0)
            => $"{a.Fecha:yyyy-MM-dd HH.mm.ss}{(indice > 0 ? $" ({indice})" : string.Empty)}{Path.GetExtension(a.Ruta).ToLowerInvariant()}";

        public static List<ArchivoAlbum> ListarSd(string raizSd, string? rutaNintendo)
        {
            var r = new List<ArchivoAlbum>();
            if (string.IsNullOrWhiteSpace(rutaNintendo)) return r;
            string carpeta = Path.Combine(raizSd, rutaNintendo.Replace('/', Path.DirectorySeparatorChar)
                                                              .Trim(Path.DirectorySeparatorChar), "Album");
            if (!Directory.Exists(carpeta)) return r;

            foreach (var f in new DirectoryInfo(carpeta).EnumerateFiles("*", SearchOption.AllDirectories))
            {
                if (!EsArchivoAlbum(f.Name)) continue;
                r.Add(new ArchivoAlbum
                {
                    Ruta    = f.FullName,
                    Fecha   = FechaDesdeNombre(f.Name) ?? f.LastWriteTime,
                    EsVideo = EsVideo(f.Name),
                    Origen  = OrigenAlbum.MicroSd,
                    Tamano  = f.Length,
                });
            }
            return r;
        }

        /// <summary>Carpeta local donde se cachea el álbum interno de la emuMMC.</summary>
        public static string CarpetaCache(string? serial)
        {
            string id = string.Concat((string.IsNullOrWhiteSpace(serial) ? "sd" : serial).Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                "NX-Suite", "AlbumEmuMMC", id);
        }
    }
}
