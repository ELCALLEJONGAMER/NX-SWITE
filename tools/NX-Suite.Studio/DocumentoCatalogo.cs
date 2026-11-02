using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NX_Suite.Studio
{
    /// <summary>
    /// catalogo/catalogo.json en memoria. Se edita como JsonNode para conservar
    /// cualquier campo que el editor no conozca. Validación, guardado y
    /// publicación (git commit + push) viven aquí, fuera de la UI.
    /// </summary>
    internal sealed class DocumentoCatalogo
    {
        public static readonly JsonSerializerOptions OpcionesEscritura = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        public DocumentoCatalogo(string raizRepo) => RaizRepo = raizRepo;

        public string RaizRepo { get; }
        public string Ruta => Path.Combine(RaizRepo, "catalogo", "catalogo.json");
        public JsonObject Raiz { get; private set; } = new();
        public bool Modificado { get; set; }

        /// <summary>Sube desde la carpeta del ejecutable hasta encontrar el repositorio.</summary>
        public static string? BuscarRaizRepo()
        {
            for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
                if (File.Exists(Path.Combine(d.FullName, "catalogo", "catalogo.json")))
                    return d.FullName;
            return ModoPortable.RepoJuntoAlExe();
        }

        public void Cargar()
        {
            Raiz = JsonNode.Parse(File.ReadAllText(Ruta, Encoding.UTF8)) as JsonObject
                   ?? throw new InvalidDataException("El catálogo no es un objeto JSON.");
            Modificado = false;
        }

        public void Guardar()
        {
            File.WriteAllText(Ruta, Raiz.ToJsonString(OpcionesEscritura), new UTF8Encoding(false));
            Modificado = false;
        }

        public JsonArray Lista(string clave)
        {
            if (Raiz[clave] is not JsonArray a) Raiz[clave] = a = new JsonArray();
            return a;
        }

        public JsonObject Objeto(string clave)
        {
            if (Raiz[clave] is not JsonObject o) Raiz[clave] = o = new JsonObject();
            return o;
        }

        /// <summary>Errores que impiden publicar. Lista vacía = catálogo publicable.</summary>
        public List<string> Validar()
        {
            var errores = new List<string>();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var m in Lista("Modulos").OfType<JsonObject>())
            {
                string id = Texto(m["Id"]);
                if (string.IsNullOrWhiteSpace(id)) { errores.Add("Hay un módulo sin Id."); continue; }
                if (!ids.Add(id)) errores.Add($"Id repetido: {id}");

                if (m["Versiones"] is not JsonArray versiones || versiones.Count == 0)
                {
                    errores.Add($"{id}: no tiene versiones.");
                    continue;
                }
                foreach (var v in versiones.OfType<JsonObject>())
                {
                    string ver = Texto(v["Version"]);
                    if (string.IsNullOrWhiteSpace(ver)) errores.Add($"{id}: hay una versión sin número.");
                    foreach (var paso in (v["PipelineInstalacion"] as JsonArray)?.OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>())
                    {
                        string tipo = Texto(paso["TipoAccion"]);
                        if (string.IsNullOrWhiteSpace(tipo)) errores.Add($"{id} {ver}: un paso no tiene tipo.");
                        if (tipo == "DESCARGAR")
                        {
                            string url = Texto(paso["Parametros"]?["Url"]);
                            if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || (u.Scheme != "https" && u.Scheme != "http"))
                                errores.Add($"{id} {ver}: URL de descarga no válida ({url}).");
                        }
                        string destino = Texto(paso["Parametros"]?["DestinoSD"]).Replace('\\', '/');
                        if (destino.StartsWith("/switch/", StringComparison.OrdinalIgnoreCase) && destino.EndsWith(".nro", StringComparison.OrdinalIgnoreCase)
                            && destino.Count(c => c == '/') == 2 && !destino.Equals("/switch/hbmenu.nro", StringComparison.OrdinalIgnoreCase))
                            errores.Add($"{id} {ver}: el .nro debe ir en su carpeta (/switch/<nombre>/<nombre>.nro), no en {destino}.");
                    }
                }
            }

            foreach (var n in Lista("news").OfType<JsonObject>())
                if (string.IsNullOrWhiteSpace(Texto(n["title"]))) errores.Add("Hay una noticia sin título.");

            return errores;
        }

        /// <summary>
        /// True si hay algo que publicar: cambios en memoria o catalogo.json guardado
        /// en disco pero distinto del último commit.
        /// </summary>
        public async Task<bool> HayCambiosSinPublicarAsync()
        {
            if (Modificado) return true;
            var (codigo, salida) = await GitAsync("status", "--porcelain", "--", "catalogo/catalogo.json");
            return codigo != 0 || !string.IsNullOrWhiteSpace(salida);
        }

        private const string RamaPublicacion = "main";

        /// <summary>
        /// Guarda, hace commit de catalogo.json en la rama actual y lo publica en
        /// origin/main (la rama que lee la app), sin cambiar de rama ni tocar el
        /// resto de archivos de main.
        /// </summary>
        public async Task<(bool Ok, string Salida)> PublicarAsync(string mensaje)
        {
            Guardar();
            var log = new StringBuilder();
            const string archivo = "catalogo/catalogo.json";

            var (c1, o1) = await GitAsync("add", archivo);
            log.Append(o1);
            if (c1 != 0) return (false, log.ToString());

            var (c2, o2) = await GitAsync("commit", "-m", mensaje, "--", archivo);
            log.Append(o2);
            if (c2 != 0 && !o2.Contains("nothing to commit") && !o2.Contains("nada para hacer commit"))
                return (false, log.ToString());

            var (c3, o3) = await GitAsync("fetch", "origin", RamaPublicacion);
            log.Append(o3);
            if (c3 != 0) return (false, log.ToString());

            // Construye un commit encima de origin/main que solo sustituye catalogo.json,
            // usando un índice temporal para no alterar el índice ni la rama de trabajo.
            string indiceTemporal = Path.Combine(Path.GetTempPath(), $"nx-studio-index-{Guid.NewGuid():N}");
            var entorno = new Dictionary<string, string> { ["GIT_INDEX_FILE"] = indiceTemporal };
            try
            {
                var (c4, blob) = await GitAsync("hash-object", "-w", archivo);
                if (c4 != 0) return (false, log.Append(blob).ToString());

                var (c5, o5) = await GitAsync(entorno, "read-tree", $"origin/{RamaPublicacion}");
                if (c5 != 0) return (false, log.Append(o5).ToString());

                var (c6, o6) = await GitAsync(entorno, "update-index", "--add", "--cacheinfo", $"100644,{blob.Trim()},{archivo}");
                if (c6 != 0) return (false, log.Append(o6).ToString());

                var (c7, arbol) = await GitAsync(entorno, "write-tree");
                if (c7 != 0) return (false, log.Append(arbol).ToString());

                var (_, arbolMain) = await GitAsync("rev-parse", $"origin/{RamaPublicacion}^{{tree}}");
                if (arbol.Trim() == arbolMain.Trim())
                    return (true, log.Append($"El catálogo de {RamaPublicacion} ya estaba al día.").ToString());

                var (c8, commit) = await GitAsync("commit-tree", arbol.Trim(), "-p", $"origin/{RamaPublicacion}", "-m", mensaje);
                if (c8 != 0) return (false, log.Append(commit).ToString());

                var (c9, o9) = await GitAsync("push", "origin", $"{commit.Trim()}:refs/heads/{RamaPublicacion}");
                log.Append(o9);
                return (c9 == 0, log.ToString());
            }
            finally
            {
                try { File.Delete(indiceTemporal); } catch (IOException) { /* temporal: se ignora */ }
            }
        }

        private Task<(int Codigo, string Salida)> GitAsync(params string[] argumentos) =>
            GitAsync(null, argumentos);

        private async Task<(int Codigo, string Salida)> GitAsync(Dictionary<string, string>? entorno, params string[] argumentos)
        {
            var psi = new ProcessStartInfo(ModoPortable.RutaGit())
            {
                WorkingDirectory = RaizRepo,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            foreach (var a in argumentos) psi.ArgumentList.Add(a);
            if (entorno != null)
                foreach (var (clave, valor) in entorno) psi.Environment[clave] = valor;

            using var p = Process.Start(psi)!;
            var salida = p.StandardOutput.ReadToEndAsync();
            var error = p.StandardError.ReadToEndAsync();
            await p.WaitForExitAsync();
            return (p.ExitCode, await salida + await error);
        }

        public static string Texto(JsonNode? nodo) =>
            nodo is JsonValue v && v.TryGetValue<string>(out var s) ? s : nodo?.ToJsonString() ?? "";
    }
}
