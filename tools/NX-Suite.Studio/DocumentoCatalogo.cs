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
            return null;
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

        /// <summary>Guarda, hace commit solo de catalogo.json y lo sube a main.</summary>
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

            var (c3, o3) = await GitAsync("pull", "--rebase", "--autostash");
            log.Append(o3);
            if (c3 != 0) return (false, log.ToString());

            var (c4, o4) = await GitAsync("push");
            log.Append(o4);
            return (c4 == 0, log.ToString());
        }

        private async Task<(int Codigo, string Salida)> GitAsync(params string[] argumentos)
        {
            var psi = new ProcessStartInfo("git")
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
