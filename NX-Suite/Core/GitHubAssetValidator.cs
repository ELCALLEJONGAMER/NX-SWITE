using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NX_Suite.Services;

namespace NX_Suite.Core
{
    /// <summary>
    /// Consulta la API de GitHub Releases para obtener el digest SHA256 de un
    /// asset descargable y lo compara contra el archivo en caché local.
    ///
    /// Reglas de bypass — el validador NO consulta la API cuando:
    ///   • La URL no pertenece a GitHub (<c>github.com</c> o <c>objects.githubusercontent.com</c>).
    ///   • El pipeline del módulo no contiene ningún paso <c>DESCARGAR</c> (módulo solo de configuración).
    ///
    /// Comportamiento no forzoso:
    ///   Si no hay internet, el token no es válido, o la API devuelve error, el
    ///   resultado es <see cref="ResultadoValidacion.NoDisponible"/> y el llamador
    ///   debe continuar usando la caché existente sin interrumpir la instalación.
    /// </summary>
    public class GitHubAssetValidator
    {
        // Un solo HttpClient compartido para toda la vida del validador.
        private static readonly HttpClient _http = new()
        {
            Timeout = TimeSpan.FromSeconds(10),
        };

        private readonly string? _token;

        /// <param name="token">
        /// Token de GitHub (PAT) opcional. Aumenta el límite de rate de 60 a 5 000 req/h.
        /// Puede ser <c>null</c> o vacío — en ese caso se usa acceso anónimo.
        /// </param>
        public GitHubAssetValidator(string? token = null)
        {
            _token = string.IsNullOrWhiteSpace(token) ? null : token.Trim();
        }

        // ???????????????????????????????????????????????????????????????????
        //  API pública
        // ???????????????????????????????????????????????????????????????????

        /// <summary>
        /// Verifica si el archivo en <paramref name="rutaArchivoLocal"/> coincide
        /// con el asset publicado en <paramref name="urlDescarga"/>.
        ///
        /// Retorna <see cref="ResultadoValidacion.NoDisponible"/> en cualquier
        /// situación donde no se pueda determinar el hash remoto (sin red, sin
        /// token suficiente para el repo, URL no GitHub, etc.), permitiendo al
        /// llamador continuar desde caché sin interrumpir la instalación.
        /// </summary>
        public async Task<ResultadoValidacion> ValidarAsync(
            string urlDescarga,
            string rutaArchivoLocal,
            CancellationToken ct = default)
        {
            // ?? 1. Solo procesar URLs de GitHub ??????????????????????????
            if (!EsUrlGitHub(urlDescarga))
                return ResultadoValidacion.NoDisponible;

            // ?? 2. El archivo local debe existir para poder comparar ??????
            if (!File.Exists(rutaArchivoLocal))
                return ResultadoValidacion.NoDisponible;

            // ?? 3. Resolver info del asset desde la API de Releases ???????
            string? sha256Remoto = null;
            try
            {
                sha256Remoto = await ObtenerSha256RemotoAsync(urlDescarga, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Warning($"GitHubAssetValidator: no se pudo consultar la API para '{urlDescarga}': {ex.Message}");
                return ResultadoValidacion.NoDisponible;
            }

            if (string.IsNullOrWhiteSpace(sha256Remoto))
                return ResultadoValidacion.NoDisponible;

            // ?? 4. Calcular SHA256 del archivo local ??????????????????????
            string sha256Local;
            try
            {
                sha256Local = new SHA256Logic().ObtenerHashArchivo(rutaArchivoLocal);
            }
            catch (Exception ex)
            {
                Logger.Warning($"GitHubAssetValidator: no se pudo calcular el hash local: {ex.Message}");
                return ResultadoValidacion.NoDisponible;
            }

            bool coincide = string.Equals(sha256Local, sha256Remoto, StringComparison.OrdinalIgnoreCase);
            return coincide ? ResultadoValidacion.Valido : ResultadoValidacion.Desactualizado;
        }

        // ???????????????????????????????????????????????????????????????????
        //  Helpers privados
        // ???????????????????????????????????????????????????????????????????

        /// <summary>Devuelve true si la URL pertenece a un servidor de GitHub.</summary>
        public static bool EsUrlGitHub(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            return url.Contains("github.com", StringComparison.OrdinalIgnoreCase)
                || url.Contains("objects.githubusercontent.com", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Intenta obtener el SHA256 del asset desde la API de GitHub Releases.
        /// Soporta dos formatos de URL de descarga:
        ///   • https://github.com/{owner}/{repo}/releases/download/{tag}/{filename}
        ///   • https://objects.githubusercontent.com/... (CDN de GitHub)
        ///
        /// La API de GitHub expone <c>digest</c> (SHA256) en la lista de assets
        /// del release desde 2024: <c>GET /repos/{owner}/{repo}/releases/tags/{tag}</c>.
        /// Si el campo <c>digest</c> no está disponible (releases antiguos) devuelve <c>null</c>.
        /// </summary>
        private async Task<string?> ObtenerSha256RemotoAsync(string urlDescarga, CancellationToken ct)
        {
            // Parsear URL para extraer owner/repo/tag/filename
            if (!TryParseGitHubReleaseUrl(urlDescarga, out string owner, out string repo,
                                           out string tag, out string filename))
                return null;

            string apiUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/tags/{tag}";

            using var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
            request.Headers.UserAgent.ParseAdd("NX-Suite/1.0");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

            if (_token != null)
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

            if (!response.IsSuccessStatusCode)
            {
                Logger.Warning($"GitHubAssetValidator: API devolvió {(int)response.StatusCode} para '{apiUrl}'.");
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            // Buscar el asset cuyo nombre coincida con filename
            if (!doc.RootElement.TryGetProperty("assets", out var assets))
                return null;

            foreach (var asset in assets.EnumerateArray())
            {
                if (!asset.TryGetProperty("name", out var nameProp)) continue;
                if (!string.Equals(nameProp.GetString(), filename, StringComparison.OrdinalIgnoreCase)) continue;

                // Campo "digest" añadido por GitHub en 2024 — formato "sha256:<hex>"
                if (asset.TryGetProperty("digest", out var digestProp))
                {
                    string? digest = digestProp.GetString();
                    if (!string.IsNullOrWhiteSpace(digest))
                    {
                        const string prefix = "sha256:";
                        return digest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                            ? digest[prefix.Length..]
                            : digest;
                    }
                }

                // Fallback: campo "sha256" si algún release lo incluye directamente
                if (asset.TryGetProperty("sha256", out var sha256Prop))
                    return sha256Prop.GetString();

                // Encontramos el asset pero sin hash — no podemos validar
                return null;
            }

            return null;
        }

        /// <summary>
        /// Parsea una URL de GitHub Releases y extrae owner, repo, tag y filename.
        /// Formato esperado: https://github.com/{owner}/{repo}/releases/download/{tag}/{filename}
        /// </summary>
        private static bool TryParseGitHubReleaseUrl(
            string url,
            out string owner, out string repo,
            out string tag,   out string filename)
        {
            owner = repo = tag = filename = string.Empty;
            try
            {
                var uri = new Uri(url);
                // Solo procesar github.com directo (no CDN)
                if (!uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
                    return false;

                // Segmentos: ["", owner, repo, "releases", "download", tag, filename]
                string[] seg = uri.AbsolutePath.Trim('/').Split('/');
                if (seg.Length < 6) return false;
                if (!string.Equals(seg[2], "releases", StringComparison.OrdinalIgnoreCase)) return false;
                if (!string.Equals(seg[3], "download",  StringComparison.OrdinalIgnoreCase)) return false;

                owner    = seg[0];
                repo     = seg[1];
                tag      = seg[4];
                filename = seg[5];
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>Resultado de la validación de hash remoto de un asset de GitHub.</summary>
    public enum ResultadoValidacion
    {
        /// <summary>El hash local coincide con el remoto — la caché es válida.</summary>
        Valido,

        /// <summary>El hash local no coincide — hay una versión más nueva del asset.</summary>
        Desactualizado,

        /// <summary>
        /// No se pudo obtener el hash remoto (sin red, sin token, URL no GitHub,
        /// API no disponible, release sin digest). La caché debe usarse tal cual.
        /// </summary>
        NoDisponible,
    }
}
