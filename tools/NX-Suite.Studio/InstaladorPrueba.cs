using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace NX_Suite.Studio
{
    /// <summary>
    /// Ejecuta de verdad el pipeline de instalación de una versión sobre una
    /// carpeta o microSD elegida, para probarla en la consola antes de aprobarla.
    /// Replica de forma simplificada los pasos de NX-SWITE.
    /// </summary>
    internal static class InstaladorPrueba
    {
        public static async Task<string> InstalarAsync(JsonObject version, string raizSd, IProgress<string> progreso)
        {
            var log = new StringBuilder();
            string temp = Path.Combine(Path.GetTempPath(), "NX-Studio", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("NX-Suite.Studio");

            try
            {
                foreach (var paso in (version["PipelineInstalacion"] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
                {
                    string tipo = DocumentoCatalogo.Texto(paso["TipoAccion"]).ToUpperInvariant();
                    var p = paso["Parametros"] as JsonObject ?? new JsonObject();
                    string S(string k) => DocumentoCatalogo.Texto(p[k]);
                    bool B(string k) => p[k] is JsonValue v && v.TryGetValue<bool>(out var b) && b;
                    string EnTemp(string r) => Path.Combine(temp, r.Replace('/', '\\').TrimStart('\\'));
                    string EnSd(string r) => Path.Combine(raizSd, r.Replace('/', '\\').TrimStart('\\'));

                    progreso.Report($"{tipo}: {DocumentoCatalogo.Texto(paso["MensajeUI"])}");
                    switch (tipo)
                    {
                        case "DESCARGAR":
                            await File.WriteAllBytesAsync(EnTemp(S("ArchivoDestino")), await http.GetByteArrayAsync(S("Url")));
                            break;
                        case "EXTRAER":
                        {
                            string zip = S("ArchivoZip") is { Length: > 0 } z ? z : S("Archivo");
                            string destino = EnTemp(S("CarpetaDestinoTemp"));
                            Directory.CreateDirectory(destino);
                            using var archivo = ArchiveFactory.OpenArchive(EnTemp(zip));
                            foreach (var e in archivo.Entries.Where(e => !e.IsDirectory))
                                e.WriteToDirectory(destino, new ExtractionOptions { ExtractFullPath = true, Overwrite = true });
                            break;
                        }
                        case "COPIARSD":
                        {
                            string origen = EnTemp(S("OrigenTemp"));
                            string destino = EnSd(S("DestinoSD"));
                            Directory.CreateDirectory(destino);
                            if (Directory.Exists(origen)) CopiarCarpeta(origen, destino);
                            else if (File.Exists(origen)) File.Copy(origen, Path.Combine(destino, Path.GetFileName(origen)), true);
                            else log.AppendLine($"⚠ COPIARSD: no existe '{S("OrigenTemp")}'");
                            break;
                        }
                        case "MOVERARCHIVO":
                        {
                            string o = EnSd(S("Origen")), d = EnSd(S("Destino"));
                            Directory.CreateDirectory(Path.GetDirectoryName(d)!);
                            if (File.Exists(o)) File.Move(o, d, true);
                            else if (Directory.Exists(o)) { if (Directory.Exists(d)) Directory.Delete(d, true); Directory.Move(o, d); }
                            else if (!B("IgnorarSiNoExiste")) log.AppendLine($"⚠ MOVERARCHIVO: no existe '{S("Origen")}'");
                            break;
                        }
                        case "CREARTXT":
                        {
                            string ruta = EnSd(S("RutaSD"));
                            if (File.Exists(ruta) && !B("SobreescribirSiExiste")) break;
                            Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
                            await File.WriteAllTextAsync(ruta, S("Contenido"), new UTF8Encoding(false));
                            break;
                        }
                        case "CREARINI":
                        {
                            string ruta = EnSd(S("RutaSD"));
                            if (File.Exists(ruta) && !B("SobreescribirSiExiste")) break;
                            Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
                            await File.WriteAllTextAsync(ruta, GenerarIni(p["Secciones"] as JsonArray), new UTF8Encoding(false));
                            break;
                        }
                        case "BORRARCARPETAS":
                            foreach (var c in (p["CarpetasSD"] as JsonArray ?? new JsonArray()).Select(DocumentoCatalogo.Texto))
                                if (Directory.Exists(EnSd(c))) Directory.Delete(EnSd(c), true);
                            break;
                        case "LIMPIAR_CACHE":
                            break;
                        default:
                            log.AppendLine($"⚠ Paso '{tipo}' no soportado en la instalación de prueba");
                            break;
                    }
                }
                log.Insert(0, $"✓ Instalado en {raizSd}\n");
            }
            finally
            {
                try { Directory.Delete(temp, true); } catch { /* temporal */ }
            }
            return log.ToString();
        }

        private static string GenerarIni(JsonArray? secciones)
        {
            var sb = new StringBuilder();
            foreach (var s in (secciones ?? new JsonArray()).OfType<JsonObject>())
            {
                if (DocumentoCatalogo.Texto(s["Comentario"]) is { Length: > 0 } com) sb.AppendLine(com);
                sb.AppendLine($"[{DocumentoCatalogo.Texto(s["Nombre"])}]");
                foreach (var c in (s["Claves"] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
                    sb.AppendLine($"{DocumentoCatalogo.Texto(c["Clave"])}={DocumentoCatalogo.Texto(c["Valor"])}");
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private static void CopiarCarpeta(string origen, string destino)
        {
            foreach (var archivo in Directory.EnumerateFiles(origen, "*", SearchOption.AllDirectories))
            {
                string d = Path.Combine(destino, Path.GetRelativePath(origen, archivo));
                Directory.CreateDirectory(Path.GetDirectoryName(d)!);
                File.Copy(archivo, d, true);
            }
        }
    }
}
