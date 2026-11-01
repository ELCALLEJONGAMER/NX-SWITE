using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SharpCompress.Archives;

namespace NX_Suite.Catalogo
{
    internal enum ResultadoModulo { AlDia, Actualizado, Manual, Omitido }

    internal sealed record InformeModulo(string Id, ResultadoModulo Resultado, string Detalle, string? Vista = null, PropuestaActualizacion? Propuesta = null);

    /// <summary>Archivo que quedará en la SD tras instalar una versión.</summary>
    internal sealed record ArchivoAnalizado(string Ruta, string? Sha256, bool Generado);

    /// <summary>
    /// Datos de una actualización detectada para revisarla visualmente (NX-STUDIO).
    /// <see cref="ModuloPropuesto"/> es el módulo completo con la versión nueva
    /// ya insertada como principal; aprobar consiste en sustituir el módulo por él.
    /// </summary>
    internal sealed record PropuestaActualizacion(
        string VersionActual, string VersionNueva, string? UrlRelease, string? Notas,
        JsonObject ModuloPropuesto, SortedSet<string> SdVieja, SortedSet<string> SdNueva,
        IReadOnlyList<string> Avisos, string? ErrorEstructura, bool EnlaceRoto = false);

    /// <summary>
    /// Actualiza módulos del catálogo usando la versión más reciente de cada
    /// módulo como plantilla de sí misma: se clona su pipeline, se sustituyen
    /// URLs/nombres de assets por los de la nueva release, se valida que la
    /// estructura del paquete no ha cambiado y se generan las firmas SHA256
    /// a partir de los archivos reales. Cualquier duda deja el módulo como
    /// "Manual" en lugar de adivinar.
    /// </summary>
    internal sealed partial class ActualizadorCatalogo
    {
        private const string MarcaVersion = "@@VER@@";
        private const string MarcaSecundaria = "@@SEC@@";
        private const string MarcaHash    = "@@HASH@@";

        private static readonly Regex UrlRelease = new(
            @"^https://github\.com/(?<repo>[^/]+/[^/]+)/releases/download/(?<tag>[^/]+)/(?<asset>[^/?#]+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly GitHubReleases _github;
        private readonly bool _validarPaquetes;

        public ActualizadorCatalogo(GitHubReleases github, bool validarPaquetes)
        {
            _github = github;
            _validarPaquetes = validarPaquetes;
        }

        public async Task<InformeModulo> ProcesarAsync(JsonObject modulo, bool aplicar)
        {
            string id = modulo["Id"]?.GetValue<string>() ?? "?";
            string? repo = modulo["GitHubRepo"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(repo))
                return new(id, ResultadoModulo.Omitido, "Sin GitHubRepo");

            if (modulo["Versiones"] is not JsonArray versiones || versiones.Count == 0 ||
                versiones[0] is not JsonObject actual)
                return new(id, ResultadoModulo.Omitido, "Sin versiones");

            // 1. Descargas actuales: todas deben ser assets de release del mismo tag.
            var partes = new List<(string Url, string Asset)>();
            string? tagActual = null, repoUrl = null;
            foreach (var p in Descargas(actual))
            {
                string url = p["Url"]?.GetValue<string>() ?? "";
                var m = UrlRelease.Match(url);
                if (!m.Success)
                    return new(id, ResultadoModulo.Omitido, $"Descarga directa fuera de releases; se mantiene la versión actual ({url})");
                string tag = Uri.UnescapeDataString(m.Groups["tag"].Value);
                if (tagActual != null && tag != tagActual)
                    return new(id, ResultadoModulo.Manual, "Las descargas mezclan varios tags");
                tagActual = tag;
                repoUrl = m.Groups["repo"].Value;
                partes.Add((url, Uri.UnescapeDataString(m.Groups["asset"].Value)));
            }
            if (partes.Count == 0 || tagActual == null)
                return new(id, ResultadoModulo.Manual, "La versión actual no tiene pasos DESCARGAR");

            if (!string.Equals(repoUrl, repo, StringComparison.OrdinalIgnoreCase))
                return new(id, ResultadoModulo.Manual, $"GitHubRepo ({repo}) no coincide con las URLs ({repoUrl})");

            // 2. Localizar release actual y candidata.
            var releases = await _github.ObtenerAsync(repo);
            if (releases == null)
                return new(id, ResultadoModulo.Manual, "Repositorio no encontrado (404)");

            var releaseActual = releases.FirstOrDefault(r => r.Tag == tagActual);
            // Si el autor borró la release publicada (caso habitual de Atmosphère), las
            // instalaciones están rotas: se propone la release estable más reciente.
            bool releaseBorrada = releaseActual == null;
            var candidata = releaseBorrada
                ? releases.FirstOrDefault(r => !r.Prerelease)
                : releases
                    .Where(r => r.Publicada > releaseActual!.Publicada)
                    .FirstOrDefault(r => releaseActual!.Prerelease || !r.Prerelease);
            if (candidata == null)
                return releaseBorrada
                    ? new(id, ResultadoModulo.Manual, $"El tag actual '{tagActual}' ya no existe y no hay otra release estable")
                    : new(id, ResultadoModulo.AlDia, tagActual);

            string versionActual = actual["Version"]?.GetValue<string>() ?? tagActual;
            string versionNueva  = DerivarVersion(versionActual, tagActual, candidata.Tag);

            if (versiones.OfType<JsonObject>().Any(v => v["Version"]?.GetValue<string>() == versionNueva))
                return new(id, ResultadoModulo.Manual, $"La versión {versionNueva} ya existe pero no es la primera");

            // 3. Emparejar cada asset antiguo con exactamente uno nuevo.
            var reemplazos = new List<Reemplazo>();
            foreach (var (url, asset) in partes)
            {
                var patron = PatronAsset(asset, versionActual, tagActual);
                var coincidencias = candidata.Assets.Where(a => patron.IsMatch(a.Nombre)).ToList();
                if (coincidencias.Count != 1)
                    return new(id, ResultadoModulo.Manual,
                        $"{versionActual} -> {candidata.Tag}: el asset '{asset}' tiene {coincidencias.Count} equivalentes en la nueva release");
                reemplazos.Add(new Reemplazo(asset, url, coincidencias[0]));
            }

            string resumen = $"{versionActual} -> {versionNueva}";
            if (!aplicar)
                return new(id, ResultadoModulo.Actualizado, resumen + " (simulación)");

            // 4. Clonar la versión y sustituir nombres/URLs.
            var nueva = (JsonObject)actual.DeepClone();
            nueva["Version"] = versionNueva;
            string versionActualLimpia = versionActual.TrimStart('v', 'V').TrimEnd('+');
            string versionNuevaLimpia  = versionNueva.TrimStart('v', 'V').TrimEnd('+');
            foreach (var paso in Pasos(nueva))
            {
                if (paso["Parametros"] is not JsonObject param) continue;
                foreach (var clave in param.Select(kv => kv.Key).ToList())
                {
                    if (param[clave] is not JsonValue valor || !valor.TryGetValue<string>(out var texto)) continue;
                    string nuevoTexto = texto;
                    foreach (var r in reemplazos)
                        nuevoTexto = nuevoTexto == r.UrlVieja ? r.Nuevo.Url : nuevoTexto.Replace(r.AssetViejo, r.Nuevo.Nombre);
                    // Carpetas temporales y archivos a renombrar suelen llevar la versión
                    // (ej. hekate_ctcaer_6.5.4.bin). Se valida después contra el paquete real.
                    if (clave is "CarpetaDestinoTemp" or "OrigenTemp" or "Origen" && nuevoTexto == texto)
                        nuevoTexto = nuevoTexto.Replace(versionActualLimpia, versionNuevaLimpia);
                    if (nuevoTexto != texto) param[clave] = nuevoTexto;
                }
            }

            // 5. Descargar paquetes viejo y nuevo, validar estructura y generar firma.
            string nota = "";
            if (_validarPaquetes)
            {
                var contenidoNuevo = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
                var contenidoViejo = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
                var paqueteNuevo = await LeerPaqueteAsync(reemplazos.Select(r => (r.Nuevo.Nombre, r.Nuevo.Url)), contenidoNuevo);
                Dictionary<string, string> paqueteViejo;
                try { paqueteViejo = await LeerPaqueteAsync(reemplazos.Select(r => (r.AssetViejo, r.UrlVieja)), contenidoViejo); }
                catch (HttpRequestException) { paqueteViejo = new(); }

                var avisos = new List<string>();
                bool enlaceRoto = releaseBorrada || paqueteViejo.Count == 0;
                var sdVieja = SimuladorSD.Simular(Pasos(actual), contenidoViejo, new List<string>());
                var sdNueva = SimuladorSD.Simular(Pasos(nueva), contenidoNuevo, avisos);
                string vista = ConstruirVista(sdNueva, SimuladorSD.Diferencias(sdVieja, sdNueva), avisos);

                string? errorEstructura = paqueteViejo.Count == 0 ? null : CompararEstructura(paqueteViejo, paqueteNuevo, versionActualLimpia, versionNuevaLimpia);
                string? error = errorEstructura
                                ?? ValidarPipeline(nueva, paqueteNuevo)
                                ?? (avisos.Count > 0 ? avisos[0] : null);

                PropuestaActualizacion Propuesta(JsonObject propuesto) => new(
                    versionActual, versionNueva, candidata.Url, candidata.Notas,
                    propuesto, sdVieja, sdNueva, avisos, errorEstructura, enlaceRoto);
                JsonObject ConVersionNueva()
                {
                    var copia = (JsonObject)modulo.DeepClone();
                    ((JsonArray)copia["Versiones"]!).Insert(0, nueva.DeepClone());
                    return copia;
                }

                if (error != null)
                    return new(id, ResultadoModulo.Manual, $"{resumen}: {error}", vista, Propuesta(ConVersionNueva()));

                var (firma, errorFirma) = GenerarFirma(modulo, versionActual, versionNueva, reemplazos, paqueteNuevo, Renombrados(nueva));
                if (firma == null)
                    return new(id, ResultadoModulo.Manual, $"{resumen}: {errorFirma}", vista, Propuesta(ConVersionNueva()));
                if (modulo["FirmasDeteccion"] is not JsonArray firmas)
                    modulo["FirmasDeteccion"] = firmas = new JsonArray();
                firmas.Insert(0, firma);
                nota = " (estructura validada, firma SHA256 generada)";
                versiones.Insert(0, nueva);
                return new(id, ResultadoModulo.Actualizado, resumen + nota, vista, Propuesta((JsonObject)modulo.DeepClone()));
            }

            versiones.Insert(0, nueva);
            return new(id, ResultadoModulo.Actualizado, resumen + nota);
        }

        /// <summary>Simula la versión publicada (primera) de un módulo sin modificar nada.</summary>
        public async Task<InformeModulo> PrevisualizarAsync(JsonObject modulo)
        {
            string id = modulo["Id"]?.GetValue<string>() ?? "?";
            if (modulo["Versiones"] is not JsonArray { Count: > 0 } versiones || versiones[0] is not JsonObject actual)
                return new(id, ResultadoModulo.Omitido, "Sin versiones");

            var assets = Descargas(actual)
                .Select(p => p["Url"]?.GetValue<string>() ?? "")
                .Where(u => u.Length > 0)
                .Select(u => (Path.GetFileName(Uri.UnescapeDataString(new Uri(u).AbsolutePath)), u));
            var contenido = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            await LeerPaqueteAsync(assets, contenido);

            var avisos = new List<string>();
            var sd = SimuladorSD.Simular(Pasos(actual), contenido, avisos);
            string version = actual["Version"]?.GetValue<string>() ?? "?";
            return new(id, avisos.Count == 0 ? ResultadoModulo.AlDia : ResultadoModulo.Manual,
                $"{version}: {sd.Count} archivo(s) en la SD, {avisos.Count} aviso(s)",
                ConstruirVista(sd, null, avisos));
        }

        /// <summary>
        /// Valida la versión publicada (primera) aunque no haya release nueva:
        /// descarga su paquete, simula la SD y comprueba que cada archivo de su
        /// firma existe en la SD resultante y que los SHA256 coinciden.
        /// </summary>
        public async Task<InformeModulo> ValidarEstructuraAsync(JsonObject modulo)
        {
            string id = modulo["Id"]?.GetValue<string>() ?? "?";
            if (modulo["Versiones"] is not JsonArray { Count: > 0 } versiones || versiones[0] is not JsonObject actual)
                return new(id, ResultadoModulo.Omitido, "Sin versiones");
            string version = actual["Version"]?.GetValue<string>() ?? "?";

            var assets = Descargas(actual)
                .Select(p => p["Url"]?.GetValue<string>() ?? "")
                .Where(u => Uri.IsWellFormedUriString(u, UriKind.Absolute))
                .Select(u => (Path.GetFileName(Uri.UnescapeDataString(new Uri(u).AbsolutePath)), u))
                .ToList();
            if (assets.Count == 0)
                return new(id, ResultadoModulo.Omitido, "Sin descargas");

            var contenido = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> paquete;
            bool enlaceRoto = false;
            try { paquete = await LeerPaqueteAsync(assets, contenido); }
            catch (HttpRequestException) { paquete = new(); enlaceRoto = true; contenido.Clear(); }

            var avisos = new List<string>();
            var sd = enlaceRoto ? new SortedSet<string>(StringComparer.OrdinalIgnoreCase) : SimuladorSD.Simular(Pasos(actual), contenido, avisos);
            if (enlaceRoto) avisos.Add("El enlace de descarga publicado no responde.");
            else avisos.AddRange(ComprobarFirma(modulo, version, sd, paquete, Renombrados(actual)));

            var propuesta = new PropuestaActualizacion(version, version, null, null, modulo, sd, sd, avisos, null, enlaceRoto);
            int graves = avisos.Count(a => !a.StartsWith("ℹ"));
            return new(id, graves == 0 ? ResultadoModulo.AlDia : ResultadoModulo.Manual,
                graves == 0 ? $"{version}: estructura y firma correctas ({sd.Count} archivos)" : $"{version}: {graves} problema(s)",
                ConstruirVista(sd, null, avisos), propuesta);
        }

        /// <summary>Problemas de la firma de <paramref name="version"/> frente a la SD simulada.</summary>
        internal static List<string> ComprobarFirma(
            JsonObject modulo, string version, SortedSet<string> sd,
            IReadOnlyDictionary<string, string> paquete, IReadOnlyDictionary<string, string> renombrados)
        {
            var problemas = new List<string>();
            var firma = (modulo["FirmasDeteccion"] as JsonArray)?.OfType<JsonObject>()
                .FirstOrDefault(f => f["Version"]?.GetValue<string>() == version);
            if (firma?["Archivos"] is not JsonArray archivos || archivos.Count == 0)
            {
                problemas.Add($"No hay firma de detección para la versión {version}.");
                return problemas;
            }

            var sdLimpia = new HashSet<string>(sd.Select(r => r.Replace("  (generado)", "")), StringComparer.OrdinalIgnoreCase);
            bool algunSha = false;
            foreach (var a in archivos.OfType<JsonObject>())
            {
                string ruta = "/" + (a["Ruta"]?.GetValue<string>() ?? "").Replace('\\', '/').Trim('/');
                bool existe = sdLimpia.Contains(ruta) || sdLimpia.Any(r => r.StartsWith(ruta + "/", StringComparison.OrdinalIgnoreCase));
                if (!existe) { problemas.Add($"La firma espera '{ruta}' pero no queda en la SD."); continue; }

                string? sha = a["SHA256"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(sha)) continue;
                algunSha = true;
                string rutaPaquete = renombrados.TryGetValue(ruta.TrimStart('/'), out var origen) ? "/" + origen : ruta;
                var candidatos = BuscarEnPaquete(rutaPaquete, paquete, new List<Reemplazo>());
                if (candidatos.Count == 0)
                    candidatos = paquete.Where(h => Path.GetFileName(h.Key).Equals(Path.GetFileName(rutaPaquete), StringComparison.OrdinalIgnoreCase))
                                        .Select(h => h.Value).ToList();
                if (candidatos.Count > 0 && !candidatos.Contains(sha, StringComparer.OrdinalIgnoreCase))
                    problemas.Add($"SHA256 de '{ruta}' no coincide con el paquete publicado.");
            }
            if (!algunSha) problemas.Add("ℹ Ningún archivo de la firma tiene SHA256: no se puede identificar la versión exacta.");
            return problemas;
        }

        private static string ConstruirVista(SortedSet<string> sd, string? diff, List<string> avisos)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var a in avisos) sb.AppendLine($"> ⚠️ {a}");
            if (diff != null)
            {
                sb.AppendLine("Cambios respecto a la versión anterior:");
                sb.AppendLine("```diff").Append(diff).AppendLine("```");
            }
            sb.AppendLine("Contenido final en la microSD:");
            sb.AppendLine("```");
            foreach (var r in sd) sb.AppendLine(r);
            sb.AppendLine("```");
            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────────────────

        private sealed record Reemplazo(string AssetViejo, string UrlVieja, AssetGitHub Nuevo);

        /// <summary>Contenido de los assets: ruta "/relativa" → SHA256. Los no comprimidos se indexan por su nombre.</summary>
        private async Task<Dictionary<string, string>> LeerPaqueteAsync(
            IEnumerable<(string Nombre, string Url)> assets,
            Dictionary<string, IReadOnlyList<string>>? contenido = null)
        {
            var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (nombre, url) in assets)
            {
                string? huella = await CachePaquetes.HuellaAsync(_github.Http, url);
                if (huella != null && CachePaquetes.Leer(url, huella) is { } cache)
                {
                    foreach (var h in cache.Hashes) hashes[h.Key] = h.Value;
                    if (contenido != null) contenido[nombre] = cache.Rutas;
                    continue;
                }
                var propios = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                List<string> rutas;
                byte[] datos = await _github.Http.GetByteArrayAsync(url);
                if (EsComprimido(nombre))
                {
                    rutas = new List<string>();
                    using var ms = new MemoryStream(datos);
                    using var archivo = ArchiveFactory.OpenArchive(ms);
                    foreach (var e in archivo.Entries.Where(e => !e.IsDirectory && e.Key != null))
                    {
                        using var s = e.OpenEntryStream();
                        string ruta = e.Key!.Replace('\\', '/').TrimStart('/');
                        propios["/" + ruta] = Sha256(s);
                        rutas.Add(ruta);
                    }
                }
                else
                {
                    propios["/" + nombre] = Convert.ToHexString(SHA256.HashData(datos)).ToLowerInvariant();
                    rutas = new List<string> { nombre };
                }
                foreach (var h in propios) hashes[h.Key] = h.Value;
                if (contenido != null) contenido[nombre] = rutas;
                if (huella != null) CachePaquetes.Guardar(new(url, huella, rutas, propios));
            }
            return hashes;
        }

        /// <summary>
        /// Compara las carpetas (hasta 2 niveles) del paquete viejo y el nuevo. Si
        /// desaparece alguna, el pipeline clonado podría copiar a un sitio incorrecto.
        /// Las carpetas nuevas se permiten (suele ser contenido adicional).
        /// </summary>
        internal static string? CompararEstructura(
            IReadOnlyDictionary<string, string> viejo, IReadOnlyDictionary<string, string> nuevo,
            string versionVieja, string versionNueva)
        {
            var carpetasNuevas = Carpetas(nuevo.Keys, versionNueva);
            var perdidas = Carpetas(viejo.Keys, versionVieja).Where(c => !carpetasNuevas.Contains(c)).ToList();
            return perdidas.Count == 0
                ? null
                : $"la estructura del paquete cambió; faltan carpetas: {string.Join(", ", perdidas.Take(5))}";
        }

        private static HashSet<string> Carpetas(IEnumerable<string> rutas, string version)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var ruta in rutas)
            {
                var segmentos = ruta.Trim('/').Split('/');
                for (int i = 1; i < segmentos.Length && i <= 2; i++)
                {
                    string carpeta = string.Join('/', segmentos.Take(i));
                    set.Add(version.Length > 0 ? carpeta.Replace(version, "*") : carpeta);
                }
            }
            return set;
        }

        /// <summary>Comprueba que los archivos que el pipeline renombra existen en el paquete nuevo.</summary>
        private static string? ValidarPipeline(JsonObject version, IReadOnlyDictionary<string, string> paquete)
        {
            foreach (var paso in Pasos(version))
            {
                if (!string.Equals(paso["TipoAccion"]?.GetValue<string>(), "MOVERARCHIVO", StringComparison.OrdinalIgnoreCase))
                    continue;
                string? origen = paso["Parametros"]?["Origen"]?.GetValue<string>();
                if (string.IsNullOrEmpty(origen)) continue;
                string sufijo = "/" + origen.Replace('\\', '/').TrimStart('/');
                // Los assets directos (.nro/.bin) se indexan por nombre: tras COPIARSD acaban en cualquier carpeta de la SD.
                string nombre = "/" + Path.GetFileName(sufijo);
                if (!paquete.Keys.Any(k => k.EndsWith(sufijo, StringComparison.OrdinalIgnoreCase) ||
                                           k.Equals(nombre, StringComparison.OrdinalIgnoreCase)))
                    return $"MOVERARCHIVO espera '{origen}' pero no está en el paquete nuevo";
            }
            return null;
        }

        private static (JsonObject? Firma, string Error) GenerarFirma(
            JsonObject modulo, string versionActual, string versionNueva,
            List<Reemplazo> reemplazos, IReadOnlyDictionary<string, string> paquete,
            IReadOnlyDictionary<string, string> renombrados)
        {
            var firmaVieja = (modulo["FirmasDeteccion"] as JsonArray)?.OfType<JsonObject>()
                .FirstOrDefault(f => f["Version"]?.GetValue<string>() == versionActual);
            if (firmaVieja?["Archivos"] is not JsonArray archivosViejos || archivosViejos.Count == 0)
                return (null, "no hay firma de la versión actual que tomar como modelo");

            var archivos = new JsonArray();
            foreach (var a in archivosViejos.OfType<JsonObject>())
            {
                string ruta = a["Ruta"]?.GetValue<string>() ?? "";
                var copia = new JsonObject { ["Ruta"] = ruta };
                if (a["SHA256"] != null)
                {
                    // Si la instalación renombra el archivo (MOVERARCHIVO), se busca por su nombre original.
                    string rutaPaquete = renombrados.TryGetValue(ruta.TrimStart('/'), out var origen) ? "/" + origen : ruta;
                    var candidatos = BuscarEnPaquete(rutaPaquete, paquete, reemplazos);
                    if (candidatos.Count != 1)
                        return (null, candidatos.Count == 0
                            ? $"'{ruta}' ya no existe en el paquete nuevo"
                            : $"'{ruta}' es ambiguo en el paquete nuevo ({candidatos.Count} coincidencias)");
                    copia["SHA256"] = candidatos[0];
                }
                archivos.Add(copia);
            }

            return (new JsonObject { ["Version"] = versionNueva, ["Archivos"] = archivos }, "");
        }

        private static List<string> BuscarEnPaquete(
            string ruta, IReadOnlyDictionary<string, string> paquete, List<Reemplazo> reemplazos)
        {
            // Coincidencia exacta o por sufijo (tolera carpetas envoltorio del ZIP).
            var exacta = paquete.Where(h => h.Key.Equals(ruta, StringComparison.OrdinalIgnoreCase)).Select(h => h.Value).ToList();
            if (exacta.Count > 0) return exacta;
            var sufijo = paquete.Where(h => h.Key.EndsWith(ruta, StringComparison.OrdinalIgnoreCase)).Select(h => h.Value).ToList();
            if (sufijo.Count > 0) return sufijo;

            // Asset directo (.nro, .bin) copiado a la SD, posiblemente con otro nombre.
            string nombre = Path.GetFileName(ruta);
            var directos = reemplazos.Where(r => !EsComprimido(r.Nuevo.Nombre)).ToList();
            var elegido = directos.FirstOrDefault(x => string.Equals(nombre, x.AssetViejo, StringComparison.OrdinalIgnoreCase))
                          ?? (directos.Count == 1 ? directos[0] : null);
            return elegido != null && paquete.TryGetValue("/" + elegido.Nuevo.Nombre, out var h)
                ? new List<string> { h }
                : new List<string>();
        }

        /// <summary>Destino → origen de los pasos MOVERARCHIVO (rutas de SD sin "/" inicial).</summary>
        private static Dictionary<string, string> Renombrados(JsonObject version)
        {
            var mapa = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var paso in Pasos(version))
            {
                if (!string.Equals(paso["TipoAccion"]?.GetValue<string>(), "MOVERARCHIVO", StringComparison.OrdinalIgnoreCase)) continue;
                string? o = paso["Parametros"]?["Origen"]?.GetValue<string>();
                string? d = paso["Parametros"]?["Destino"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(o) && !string.IsNullOrEmpty(d))
                    mapa[d.Replace('\\', '/').TrimStart('/')] = o.Replace('\\', '/').TrimStart('/');
            }
            return mapa;
        }

        private static IEnumerable<JsonObject> Pasos(JsonObject version) =>
            (version["PipelineInstalacion"] as JsonArray)?.OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>();

        private static IEnumerable<JsonObject> Descargas(JsonObject version) =>
            Pasos(version)
                .Where(p => string.Equals(p["TipoAccion"]?.GetValue<string>(), "DESCARGAR", StringComparison.OrdinalIgnoreCase))
                .Select(p => p["Parametros"] as JsonObject)
                .OfType<JsonObject>();

        internal static bool EsComprimido(string nombre) =>
            nombre.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
            nombre.EndsWith(".7z",  StringComparison.OrdinalIgnoreCase) ||
            nombre.EndsWith(".rar", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Convierte el nombre de un asset en un patrón donde la versión (con y sin "v")
        /// y los hashes de commit son comodines.
        /// </summary>
        internal static Regex PatronAsset(string asset, string version, string tag)
        {
            var tokens = new[] { tag, tag.TrimStart('v', 'V'), version, version.TrimEnd('+') }
                .Where(t => t.Length > 0)
                .Distinct()
                .OrderByDescending(t => t.Length);

            // El comodín solo admite separadores que ya tenía la versión, para no
            // confundir "tico-0.8.0.zip" con variantes como "tico-0.8.0-debug.zip".
            string patron = asset;
            string extra = "";
            foreach (var t in tokens)
            {
                if (!patron.Contains(t)) continue;
                patron = patron.Replace(t, MarcaVersion);
                if (t.Contains('-')) extra += @"\-";
                if (t.Contains('_')) extra += "_";
            }
            patron = Regex.Replace(patron, @"(?<![0-9a-zA-Z])[0-9a-f]{7,40}(?![0-9a-zA-Z])", MarcaHash);
            // Versiones secundarias (ej. «Nyx_1.9.4» en hekate, «hbl-2.4.5» en Atmosphère): también cambian entre releases.
            patron = Regex.Replace(patron, @"(?<![0-9A-Za-z.])\d+(?:\.\d+)+(?![0-9A-Za-z])", MarcaSecundaria);
            patron = Regex.Escape(patron)
                .Replace(MarcaVersion, $"[0-9A-Za-z.+{extra}]+")
                .Replace(MarcaHash, "[0-9a-f]{7,40}")
                .Replace(MarcaSecundaria, @"\d+(?:\.\d+)+");
            return new Regex("^" + patron + "$", RegexOptions.IgnoreCase);
        }

        /// <summary>Aplica al tag nuevo la misma relación que había entre versión y tag actuales.</summary>
        internal static string DerivarVersion(string versionActual, string tagActual, string tagNuevo) =>
            versionActual == tagActual ? tagNuevo : tagNuevo.TrimStart('v', 'V');

        private static string Sha256(Stream s) =>
            Convert.ToHexString(SHA256.HashData(s)).ToLowerInvariant();
    }
}
