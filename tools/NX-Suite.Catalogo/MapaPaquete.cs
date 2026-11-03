using System.Text.Json.Nodes;

namespace NX_Suite.Catalogo
{
    /// <summary>Un archivo del paquete descargado y dónde caerá en la microSD.</summary>
    internal sealed class ArchivoMapa
    {
        /// <summary>Ruta dentro del paquete (sin "/" inicial). Para archivos sueltos, su nombre.</summary>
        public required string Origen { get; init; }
        public string? Sha { get; init; }
        /// <summary>Ruta final en la SD (con "/" inicial).</summary>
        public string Destino { get; set; } = "";
        public bool Incluir { get; set; } = true;
        public bool UsarSha { get; set; }
    }

    /// <summary>
    /// Convierte el mapa visual de archivos (origen → destino, incluir, SHA) en
    /// PipelineInstalacion, PipelineDesinstalacion y firma de detección
    /// compatibles con los pasos que ejecuta NX-SWITE.
    /// </summary>
    internal static class MapaPaquete
    {
        public static bool EsComprimido(string asset) => ActualizadorCatalogo.EsComprimido(asset);

        /// <summary>Destino propuesto según la extensión (los paquetes comprimidos se extraen en la raíz).</summary>
        public static string DestinoPorDefecto(string asset, string origen, string idModulo, string carpetaPadre = "", bool quitarRaiz = false)
        {
            string rel = origen.Replace('\\', '/').Trim('/');
            if (EsComprimido(asset))
            {
                if (quitarRaiz && rel.Contains('/')) rel = rel[(rel.IndexOf('/') + 1)..];
                return Unir(carpetaPadre.Length > 0 ? carpetaPadre : "/", rel);
            }
            if (carpetaPadre.Length > 0) return Unir(carpetaPadre, rel);
            string nombre = Path.GetFileName(rel);
            string carpeta = Path.GetExtension(rel).ToLowerInvariant() switch
            {
                ".nro" => "/switch/" + (idModulo.Length > 0 ? idModulo : Path.GetFileNameWithoutExtension(rel)),
                ".ovl" => "/switch/.overlays",
                ".bin" => "/bootloader/payloads",
                ".nsp" => "/nsp",
                _      => "/",
            };
            return Unir(carpeta, nombre);
        }

        public static (JsonArray Instalacion, JsonArray Desinstalacion) GenerarPipelines(
            string url, string asset, string carpetaTemp, IReadOnlyList<ArchivoMapa> archivos, bool limpiarCache)
        {
            var inst = new JsonArray { Paso("DESCARGAR", $"Descargando {asset}…", new() { ["Url"] = url, ["ArchivoDestino"] = asset }) };
            var incluidos = archivos.Where(a => a.Incluir && a.Destino.Length > 0).ToList();
            bool comprimido = EsComprimido(asset);

            if (comprimido)
            {
                inst.Add(Paso("EXTRAER", $"Extrayendo {asset}…", new() { ["ArchivoZip"] = asset, ["CarpetaDestinoTemp"] = carpetaTemp }));
                var pendientes = new List<ArchivoMapa>(incluidos);

                // 1. Todo el paquete con la misma estructura bajo una carpeta → una sola copia.
                string? comun = PrefijoComun(archivos);
                if (comun != null && archivos.All(a => a.Incluir))
                {
                    inst.Add(Paso("COPIARSD", "Copiando a la microSD…", new() { ["OrigenTemp"] = carpetaTemp, ["DestinoSD"] = comun }));
                    pendientes.Clear();
                }
                else
                {
                    // 2. Carpetas de primer nivel que se copian enteras sin cambios.
                    foreach (var grupo in archivos.Where(a => a.Origen.Contains('/')).GroupBy(a => a.Origen[..a.Origen.IndexOf('/')], StringComparer.OrdinalIgnoreCase))
                    {
                        if (!grupo.All(a => a.Incluir)) continue;
                        string? p = PrefijoComun(grupo.ToList());
                        if (p == null) continue;
                        inst.Add(Paso("COPIARSD", $"Copiando {grupo.Key}…", new()
                        {
                            ["OrigenTemp"] = carpetaTemp + "/" + grupo.Key,
                            ["DestinoSD"] = Unir(p, grupo.Key),
                        }));
                        pendientes.RemoveAll(a => grupo.Contains(a));
                    }
                }
                // 3. El resto, archivo a archivo (con renombrado si cambia el nombre).
                foreach (var a in pendientes) CopiarSuelto(inst, carpetaTemp + "/" + a.Origen, a);
            }
            else
            {
                foreach (var a in incluidos) CopiarSuelto(inst, asset, a);
            }

            if (limpiarCache)
            {
                var par = new JsonObject { ["ArchivoZip"] = asset };
                if (comprimido) par["CarpetaTemp"] = carpetaTemp;
                inst.Add(Paso("LIMPIAR_CACHE", "Limpiando temporales…", par));
            }

            var desins = new JsonArray();
            if (incluidos.Count > 0)
            {
                desins.Add(Paso("BORRARARCHIVOS", "Eliminando archivos…", new()
                {
                    ["RutasSD"] = new JsonArray(incluidos.Select(a => (JsonNode?)JsonValue.Create(Normalizar(a.Destino))).ToArray()),
                }));
                var carpetas = incluidos.SelectMany(a => Ancestros(Normalizar(a.Destino)))
                                        .Distinct(StringComparer.OrdinalIgnoreCase)
                                        .OrderByDescending(c => c.Count(ch => ch == '/')).ThenBy(c => c, StringComparer.OrdinalIgnoreCase)
                                        .ToList();
                if (carpetas.Count > 0)
                    desins.Add(Paso("BORRARCARPETASVACIAS", "Limpiando carpetas vacías…", new()
                    {
                        ["CarpetasSD"] = new JsonArray(carpetas.Select(c => (JsonNode?)JsonValue.Create(c)).ToArray()),
                    }));
            }
            Renumerar(inst);
            Renumerar(desins);
            return (inst, desins);
        }

        /// <summary>Firma de detección: todos los incluidos por presencia; los marcados, además con SHA256.</summary>
        public static JsonObject GenerarFirma(string version, IEnumerable<ArchivoMapa> archivos)
        {
            var lista = new JsonArray();
            foreach (var a in archivos.Where(a => a.Incluir && a.Destino.Length > 0))
            {
                var o = new JsonObject { ["Ruta"] = Normalizar(a.Destino) };
                if (a.UsarSha && a.Sha != null) o["SHA256"] = a.Sha;
                lista.Add(o);
            }
            return new JsonObject { ["Version"] = version, ["Archivos"] = lista };
        }

        public static string Normalizar(string ruta) => "/" + ruta.Replace('\\', '/').Trim('/');

        public static string Unir(string carpeta, string relativo) =>
            Normalizar(carpeta.Replace('\\', '/').TrimEnd('/') + "/" + relativo.Replace('\\', '/').TrimStart('/'));

        private static void CopiarSuelto(JsonArray inst, string origenTemp, ArchivoMapa a)
        {
            string destino = Normalizar(a.Destino);
            string carpeta = Carpeta(destino);
            string nombreOrigen = Path.GetFileName(a.Origen);
            inst.Add(Paso("COPIARSD", $"Copiando {nombreOrigen}…", new() { ["OrigenTemp"] = origenTemp, ["DestinoSD"] = carpeta }));
            string copiado = Unir(carpeta, nombreOrigen);
            if (!copiado.Equals(destino, StringComparison.Ordinal))
                inst.Add(Paso("MOVERARCHIVO", $"Renombrando {nombreOrigen}…", new() { ["Origen"] = copiado, ["Destino"] = destino, ["IgnorarSiNoExiste"] = false }));
        }

        /// <summary>Carpeta P tal que Destino = P + Origen en todos los archivos, o null.</summary>
        private static string? PrefijoComun(IReadOnlyList<ArchivoMapa> archivos)
        {
            string? comun = null;
            foreach (var a in archivos)
            {
                string d = Normalizar(a.Destino), o = "/" + a.Origen.Replace('\\', '/').Trim('/');
                if (!d.EndsWith(o, StringComparison.Ordinal)) return null;
                string p = d[..^o.Length];
                p = p.Length == 0 ? "/" : p;
                if (comun == null) comun = p;
                else if (!comun.Equals(p, StringComparison.Ordinal)) return null;
            }
            return comun;
        }

        private static string Carpeta(string ruta)
        {
            int i = ruta.LastIndexOf('/');
            return i <= 0 ? "/" : ruta[..i];
        }

        private static IEnumerable<string> Ancestros(string ruta)
        {
            for (string c = Carpeta(ruta); c != "/"; c = Carpeta(c)) yield return c;
        }

        private static JsonObject Paso(string tipo, string mensaje, JsonObject parametros) =>
            new() { ["Paso"] = 0, ["TipoAccion"] = tipo, ["MensajeUI"] = mensaje, ["Parametros"] = parametros };

        private static void Renumerar(JsonArray pasos)
        {
            int n = 1;
            foreach (var p in pasos.OfType<JsonObject>()) p["Paso"] = n++;
        }
    }
}
