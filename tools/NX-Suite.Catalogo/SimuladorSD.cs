using System.Text;
using System.Text.Json.Nodes;

namespace NX_Suite.Catalogo
{
    /// <summary>
    /// Simula en memoria un pipeline de instalación (sin tocar disco) y devuelve
    /// el árbol de archivos que quedaría en la microSD. Reproduce la semántica de
    /// los pasos reales de NX-Swite (PasoDescargar, PasoExtraer, PasoCopiarSD,
    /// PasoMoverArchivo, PasoCrearIni/Txt, BorrarCarpetas) para poder revisar
    /// cómo se instalará y renombrará un paquete antes de aprobarlo.
    /// </summary>
    internal static class SimuladorSD
    {
        /// <param name="pasos">PipelineInstalacion de la versión.</param>
        /// <param name="contenidoAssets">Nombre de asset descargado → rutas relativas que contiene
        /// (para .zip/.7z, sus entradas; para archivos sueltos, solo su nombre).</param>
        /// <param name="avisos">Problemas detectados (origen inexistente, renombrado sin efecto…).</param>
        public static SortedSet<string> Simular(
            IEnumerable<JsonObject> pasos,
            IReadOnlyDictionary<string, IReadOnlyList<string>> contenidoAssets,
            List<string> avisos)
        {
            var sd          = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            var descargados = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);          // ArchivoDestino → asset
            var extraidos   = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase); // CarpetaTemp → rutas

            foreach (var paso in pasos)
            {
                string tipo = paso["TipoAccion"]?.GetValue<string>()?.ToUpperInvariant() ?? "";
                var p = paso["Parametros"] as JsonObject ?? new JsonObject();
                string S(string k) => p[k] is JsonValue v && v.TryGetValue<string>(out var s) ? s : "";

                switch (tipo)
                {
                    case "DESCARGAR":
                    {
                        string asset = Path.GetFileName(Uri.UnescapeDataString(S("Url")));
                        descargados[S("ArchivoDestino")] = asset;
                        break;
                    }
                    case "EXTRAER":
                    {
                        string zip = S("ArchivoZip") is { Length: > 0 } z ? z : S("Archivo");
                        if (descargados.TryGetValue(zip, out var asset) && contenidoAssets.TryGetValue(asset, out var rutas))
                            extraidos[S("CarpetaDestinoTemp")] = rutas;
                        else
                            avisos.Add($"EXTRAER: '{zip}' no se descargó antes");
                        break;
                    }
                    case "COPIARSD":
                        Copiar(S("OrigenTemp"), S("DestinoSD"), descargados, extraidos, contenidoAssets, sd, avisos);
                        break;
                    case "MOVERARCHIVO":
                        Mover(S("Origen"), S("Destino"), p["IgnorarSiNoExiste"]?.GetValue<bool>() == true, sd, avisos);
                        break;
                    case "CREARINI":
                    case "CREARTXT":
                        sd.Add(Normalizar(S("RutaSD")) + "  (generado)");
                        break;
                    case "BORRARCARPETAS":
                        if (p["CarpetasSD"] is JsonArray carpetas)
                            foreach (var c in carpetas.Select(x => Normalizar(x?.GetValue<string>() ?? "")))
                                sd.RemoveWhere(r => r.StartsWith(c + "/", StringComparison.OrdinalIgnoreCase));
                        break;
                }
            }
            return sd;
        }

        private static void Copiar(
            string origen, string destino,
            Dictionary<string, string> descargados,
            Dictionary<string, IReadOnlyList<string>> extraidos,
            IReadOnlyDictionary<string, IReadOnlyList<string>> contenidoAssets,
            SortedSet<string> sd, List<string> avisos)
        {
            string dest = Normalizar(destino);
            string o = origen.Replace('\\', '/').Trim('/');

            // 1. Carpeta (o subcarpeta/archivo) dentro de una extracción.
            foreach (var (carpeta, rutas) in extraidos)
            {
                if (o.Equals(carpeta, StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var r in rutas) sd.Add(Unir(dest, r));
                    return;
                }
                if (o.StartsWith(carpeta + "/", StringComparison.OrdinalIgnoreCase))
                {
                    string sub = o[(carpeta.Length + 1)..];
                    var dentro = rutas.Where(r => r.StartsWith(sub + "/", StringComparison.OrdinalIgnoreCase)).ToList();
                    if (dentro.Count > 0)
                    {
                        foreach (var r in dentro) sd.Add(Unir(dest, r[(sub.Length + 1)..]));
                        return;
                    }
                    if (rutas.Any(r => r.Equals(sub, StringComparison.OrdinalIgnoreCase)))
                    {
                        sd.Add(Unir(dest, Path.GetFileName(sub)));
                        return;
                    }
                }
            }

            // 2. Archivo descargado suelto (.nro, .bin…).
            if (descargados.ContainsKey(o))
            {
                sd.Add(Unir(dest, Path.GetFileName(o)));
                return;
            }

            avisos.Add($"COPIARSD: el origen '{origen}' no existe en el paquete");
        }

        private static void Mover(string origen, string destino, bool ignorar, SortedSet<string> sd, List<string> avisos)
        {
            string o = Normalizar(origen), d = Normalizar(destino);
            if (sd.Remove(o))
            {
                sd.Add(d);
                return;
            }
            var hijos = sd.Where(r => r.StartsWith(o + "/", StringComparison.OrdinalIgnoreCase)).ToList();
            if (hijos.Count > 0)
            {
                foreach (var h in hijos) { sd.Remove(h); sd.Add(d + h[o.Length..]); }
                return;
            }
            avisos.Add($"MOVERARCHIVO: '{origen}' no existe{(ignorar ? " (se ignora, el renombrado no se aplicará)" : "")}");
        }

        private static string Normalizar(string ruta) => "/" + ruta.Replace('\\', '/').Trim('/');

        private static string Unir(string carpeta, string relativo) =>
            (carpeta.TrimEnd('/') + "/" + relativo.Replace('\\', '/').TrimStart('/')).Replace("//", "/");

        /// <summary>Diff legible entre dos árboles: + añadido, - eliminado.</summary>
        public static string Diferencias(SortedSet<string> antes, SortedSet<string> despues)
        {
            var sb = new StringBuilder();
            foreach (var r in despues.Where(r => !antes.Contains(r))) sb.AppendLine("+ " + r);
            foreach (var r in antes.Where(r => !despues.Contains(r))) sb.AppendLine("- " + r);
            return sb.Length == 0 ? "(misma estructura de archivos)\n" : sb.ToString();
        }
    }
}
