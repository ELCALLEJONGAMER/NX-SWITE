using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NX_Swite.Core
{
    public enum TipoEmuMmc { NoConfigurada, Particion, Archivos }

    public sealed class InfoEmuMmc
    {
        public bool       ExisteIni        { get; init; }
        public bool       Habilitada       { get; init; }
        public TipoEmuMmc Tipo             { get; init; }
        public long?      Sector           { get; init; }
        public string?    RutaArchivos     { get; init; }
        public string?    RutaNintendo     { get; init; }
        public bool       ExisteNintendo   { get; init; }
        public int        ContenidosNintendo { get; init; }
    }

    /// <summary>
    /// Lectura (solo lectura) de la configuración emuMMC de una microSD:
    /// <c>emuMMC/emummc.ini</c> y la carpeta Nintendo asociada.
    /// </summary>
    public static class EmuMmcInfoLogic
    {
        public static InfoEmuMmc Leer(string raizSd)
        {
            string rutaIni = Path.Combine(raizSd, "emuMMC", "emummc.ini");
            if (!File.Exists(rutaIni))
                return new InfoEmuMmc();

            var valores = ParsearIni(File.ReadLines(rutaIni));
            var info = Interpretar(valores);

            string? nintendo = string.IsNullOrWhiteSpace(info.RutaNintendo)
                ? null
                : Path.Combine(raizSd, info.RutaNintendo.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar));
            bool existe = nintendo != null && Directory.Exists(nintendo);
            int contenidos = 0;
            if (existe)
            {
                string contents = Path.Combine(nintendo!, "Contents", "registered");
                try { if (Directory.Exists(contents)) contenidos = Directory.EnumerateFileSystemEntries(contents).Count(); }
                catch { }
            }

            return new InfoEmuMmc
            {
                ExisteIni          = true,
                Habilitada         = info.Habilitada,
                Tipo               = info.Tipo,
                Sector             = info.Sector,
                RutaArchivos       = info.RutaArchivos,
                RutaNintendo       = info.RutaNintendo,
                ExisteNintendo     = existe,
                ContenidosNintendo = contenidos,
            };
        }

        public static Dictionary<string, string> ParsearIni(IEnumerable<string> lineas)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var bruta in lineas)
            {
                string l = bruta.Trim();
                if (l.Length == 0 || l.StartsWith(';') || l.StartsWith('#') || l.StartsWith('[')) continue;
                int i = l.IndexOf('=');
                if (i <= 0) continue;
                d[l[..i].Trim()] = l[(i + 1)..].Trim();
            }
            return d;
        }

        public static InfoEmuMmc Interpretar(IReadOnlyDictionary<string, string> v)
        {
            bool habilitada = v.TryGetValue("enabled", out var en) && en.Trim() == "1";

            long? sector = null;
            if (v.TryGetValue("sector", out var s))
            {
                string hex = s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? s[2..] : s;
                if (long.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out long n) && n > 0)
                    sector = n;
            }

            v.TryGetValue("path", out var path);
            v.TryGetValue("nintendo_path", out var nin);

            var tipo = sector != null ? TipoEmuMmc.Particion
                     : !string.IsNullOrWhiteSpace(path) ? TipoEmuMmc.Archivos
                     : TipoEmuMmc.NoConfigurada;

            return new InfoEmuMmc
            {
                ExisteIni    = true,
                Habilitada   = habilitada,
                Tipo         = tipo,
                Sector       = sector,
                RutaArchivos = string.IsNullOrWhiteSpace(path) ? null : path,
                RutaNintendo = string.IsNullOrWhiteSpace(nin) ? null : nin,
            };
        }
    }
}
