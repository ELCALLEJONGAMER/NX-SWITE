using NX_Swite.Core;
using NX_Swite.Core.Configuracion;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NX_Swite.Services
{
    /// <summary>
    /// Servicio reutilizable de resolución de nombres de juegos a partir de un
    /// TitleId, respaldado por un TitleDB remoto cacheado localmente.
    ///
    /// Diseño:
    ///  - NO conoce ningún proveedor concreto (blawar u otro): solo depende de
    ///    <see cref="ConfiguracionRemota.TitleDB"/> (URL declarada en el Gist).
    ///  - Si no hay URL configurada, si falla la red, o si el TitleId no existe
    ///    en el índice, <see cref="ResolverNombre"/> siempre devuelve un
    ///    fallback seguro ("Título &lt;TITLE_ID&gt;") — nunca lanza ni bloquea.
    ///  - El índice TitleId ? Nombre se construye UNA sola vez en memoria por
    ///    sesión (o hasta que cambie el remoto) y se reutiliza para todas las
    ///    resoluciones sin volver a parsear el JSON completo.
    ///  - Sigue la misma estrategia stale-while-revalidate con ETag que
    ///    <see cref="NX_Swite.Network.GistParser"/>: sirve desde caché local
    ///    de inmediato y revalida en background sin bloquear al llamador.
    /// </summary>
    public class TitleDbService
    {
        private static readonly HttpClient _client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(8)
        };

        private readonly GestorCacheTitleDB _cache;

        private readonly object _lock = new();
        private Dictionary<string, string>? _indice;
        private bool _intentado;
        private Task? _preparacionEnCurso;

        public TitleDbService(GestorCacheTitleDB? cache = null)
        {
            _cache = cache ?? new GestorCacheTitleDB();
        }

        /// <summary>
        /// Garantiza que el índice en memoria esté listo (desde caché local si
        /// existe, o descargando por primera vez). Idempotente y single-flight:
        /// llamadas concurrentes reutilizan la misma preparación en curso.
        /// No lanza excepciones: cualquier fallo deja el índice vacío y
        /// <see cref="ResolverNombre"/> cae al fallback.
        /// </summary>
        public Task PrepararAsync()
        {
            lock (_lock)
            {
                if (_intentado && _indice != null) return Task.CompletedTask;
                if (_preparacionEnCurso != null && !_preparacionEnCurso.IsCompleted)
                    return _preparacionEnCurso;

                _preparacionEnCurso = EjecutarPreparacionAsync();
                return _preparacionEnCurso;
            }
        }

        /// <summary>
        /// Resuelve el nombre de un juego a partir de su TitleId usando
        /// únicamente el índice ya cargado en memoria (no hace I/O ni red).
        /// Llamar a <see cref="PrepararAsync"/> antes de usar este método.
        /// </summary>
        public string ResolverNombre(string titleId)
        {
            if (string.IsNullOrWhiteSpace(titleId))
                return "Título desconocido";

            string clave = titleId.Trim().ToUpperInvariant();

            if (_indice != null && _indice.TryGetValue(clave, out var nombre) &&
                !string.IsNullOrWhiteSpace(nombre))
            {
                return nombre;
            }

            return $"Título {clave}";
        }

        // ?? Preparación del índice ???????????????????????????????????????

        private async Task EjecutarPreparacionAsync()
        {
            try
            {
                string urlRemota = ConfiguracionRemota.TitleDB?.UrlTitleDB ?? string.Empty;

                // 1) Caché local disponible ? construir índice de inmediato.
                if (_cache.TieneCache)
                {
                    string? json = await _cache.CargarJsonAsync();
                    var indiceCache = json != null ? ParsearIndice(json) : null;

                    if (indiceCache != null)
                    {
                        lock (_lock) { _indice = indiceCache; _intentado = true; }

                        // Revalidar en background si hay URL configurada. No bloquea
                        // al llamador ni afecta la respuesta ya servida desde caché.
                        if (!string.IsNullOrWhiteSpace(urlRemota))
                            _ = RevalidarEnBackgroundAsync(urlRemota);

                        return;
                    }
                }

                // 2) Sin caché válido: si hay URL, intentar descarga completa.
                if (!string.IsNullOrWhiteSpace(urlRemota))
                {
                    var indiceDescargado = await DescargarYConstruirIndiceAsync(urlRemota);
                    if (indiceDescargado != null)
                    {
                        lock (_lock) { _indice = indiceDescargado; _intentado = true; }
                        return;
                    }
                }

                // 3) Sin caché y sin red/URL: índice vacío, fallback siempre disponible.
                lock (_lock) { _indice ??= new Dictionary<string, string>(); _intentado = true; }
            }
            catch
            {
                // Nunca debe bloquear ni fallar "Preparar actualización" por esto.
                lock (_lock) { _indice ??= new Dictionary<string, string>(); _intentado = true; }
            }
            finally
            {
                lock (_lock) _preparacionEnCurso = null;
            }
        }

        private async Task<Dictionary<string, string>?> DescargarYConstruirIndiceAsync(string urlRemota)
        {
            try
            {
                using var response = await _client.GetAsync(urlRemota);
                if (!response.IsSuccessStatusCode) return null;

                string jsonContent = await response.Content.ReadAsStringAsync();
                var indice = ParsearIndice(jsonContent);
                if (indice == null) return null; // JSON corrupto/no parseable: no tocar caché previo.

                // Guardado atómico: solo reemplaza el caché anterior tras validar el parseo.
                await _cache.GuardarJsonAtomicoAsync(jsonContent);

                string? etag = response.Headers.ETag?.Tag;
                if (!string.IsNullOrWhiteSpace(etag))
                    await _cache.GuardarETagAsync(etag);

                return indice;
            }
            catch
            {
                return null;
            }
        }

        private async Task RevalidarEnBackgroundAsync(string urlRemota)
        {
            try
            {
                string? etag = await _cache.CargarETagAsync();

                var request = new HttpRequestMessage(HttpMethod.Get, urlRemota);
                if (!string.IsNullOrWhiteSpace(etag))
                    request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(etag, isWeak: true));

                using var response = await _client.SendAsync(request);

                if (response.StatusCode == HttpStatusCode.NotModified)
                    return; // Sin cambios: el caché e índice actuales siguen siendo válidos.

                if (!response.IsSuccessStatusCode)
                    return; // Error no crítico: conservar índice/caché actuales.

                string jsonContent = await response.Content.ReadAsStringAsync();
                var nuevoIndice = ParsearIndice(jsonContent);
                if (nuevoIndice == null) return; // Corrupto: NO destruir el caché previo.

                await _cache.GuardarJsonAtomicoAsync(jsonContent);

                string? nuevoEtag = response.Headers.ETag?.Tag;
                if (!string.IsNullOrWhiteSpace(nuevoEtag))
                    await _cache.GuardarETagAsync(nuevoEtag);

                lock (_lock) _indice = nuevoIndice;
            }
            catch
            {
                // No crítico: el índice/caché actuales siguen siendo válidos.
            }
        }

        // ?? Parseo flexible del formato TitleDB ??????????????????????????

        /// <summary>
        /// Construye el índice TitleId ? Nombre a partir del JSON del TitleDB.
        /// Soporta dos formas habituales:
        ///  1. Diccionario {"0100...": {"name": "...", ...}, ...} (formato blawar por región).
        ///  2. Array [{"id"/"titleId": "0100...", "name": "..."}, ...].
        /// Devuelve null si el contenido no puede interpretarse como ninguna
        /// de las dos formas (JSON corrupto o formato desconocido).
        /// </summary>
        private static readonly object _iconosLock = new();
        private static Dictionary<string, string> _iconos = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>URL del icono del juego según el TitleDB, o null.</summary>
        public string? ResolverIconoUrl(string titleId)
        {
            if (string.IsNullOrWhiteSpace(titleId)) return null;
            lock (_iconosLock)
                return _iconos.TryGetValue(titleId.Trim(), out var u) ? u : null;
        }

        private static Dictionary<string, string>? ParsearIndice(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var indice = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var iconos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                if (root.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in root.EnumerateObject())
                    {
                        // blawar US.en.json usa el nsuId como clave; el TitleId real va en "id".
                        string titleId = (prop.Value.ValueKind == JsonValueKind.Object
                                ? ObtenerPropiedadTexto(prop.Value, "id")
                                : null)?.Trim().ToUpperInvariant()
                            ?? prop.Name.Trim().ToUpperInvariant();
                        if (!EsTitleIdValido(titleId)) titleId = prop.Name.Trim().ToUpperInvariant();
                        string? nombre = ExtraerNombre(prop.Value);
                        if (!string.IsNullOrWhiteSpace(nombre) && EsTitleIdValido(titleId))
                        {
                            indice[titleId] = nombre!;
                            string? icono = ObtenerPropiedadTexto(prop.Value, "iconUrl");
                            if (!string.IsNullOrWhiteSpace(icono)) iconos[titleId] = icono!;
                        }
                    }
                    lock (_iconosLock) _iconos = iconos;
                    return indice;
                }

                if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in root.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.Object) continue;

                        string? titleId = ObtenerPropiedadTexto(item, "id") ??
                                          ObtenerPropiedadTexto(item, "titleId") ??
                                          ObtenerPropiedadTexto(item, "titleid");
                        string? nombre = ExtraerNombre(item);

                        if (titleId != null && !string.IsNullOrWhiteSpace(nombre))
                        {
                            titleId = titleId.Trim().ToUpperInvariant();
                            if (EsTitleIdValido(titleId))
                                indice[titleId] = nombre!;
                        }
                    }
                    return indice;
                }

                return null; // Formato irreconocible.
            }
            catch
            {
                return null; // JSON corrupto.
            }
        }

        private static string? ExtraerNombre(JsonElement elemento)
        {
            if (elemento.ValueKind != JsonValueKind.Object) return null;
            return ObtenerPropiedadTexto(elemento, "name") ??
                   ObtenerPropiedadTexto(elemento, "Name") ??
                   ObtenerPropiedadTexto(elemento, "nombre");
        }

        private static string? ObtenerPropiedadTexto(JsonElement obj, string propiedad)
        {
            if (obj.TryGetProperty(propiedad, out var valor) && valor.ValueKind == JsonValueKind.String)
                return valor.GetString();
            return null;
        }

        private static bool EsTitleIdValido(string titleId)
        {
            if (titleId.Length != 16) return false;
            foreach (char c in titleId)
            {
                bool esHex = (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F');
                if (!esHex) return false;
            }
            return true;
        }
    }
}
