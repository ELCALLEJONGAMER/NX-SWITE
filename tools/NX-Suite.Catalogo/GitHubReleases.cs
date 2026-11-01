using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace NX_Suite.Catalogo
{
    internal sealed record AssetGitHub(string Nombre, string Url);

    internal sealed record ReleaseGitHub(
        string Tag, bool Prerelease, DateTimeOffset Publicada, IReadOnlyList<AssetGitHub> Assets,
        string? Url = null, string? Notas = null);

    /// <summary>
    /// Cliente mínimo de GitHub Releases. Usa GITHUB_TOKEN si existe (5000 req/h)
    /// y cachea por repositorio para no repetir llamadas en la misma ejecución.
    /// </summary>
    internal sealed class GitHubReleases : IDisposable
    {
        private readonly HttpClient _http = new();
        private readonly Dictionary<string, IReadOnlyList<ReleaseGitHub>?> _cache =
            new(StringComparer.OrdinalIgnoreCase);

        public GitHubReleases(string? token = null)
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("NX-Suite.Catalogo");
            _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            token ??= Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            if (!string.IsNullOrWhiteSpace(token))
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        public HttpClient Http => _http;

        /// <summary>Releases publicadas (sin borradores), de más reciente a más antigua. Null si el repo no existe.</summary>
        public async Task<IReadOnlyList<ReleaseGitHub>?> ObtenerAsync(string repo)
        {
            if (_cache.TryGetValue(repo, out var enCache)) return enCache;

            string url = $"https://api.github.com/repos/{repo}/releases?per_page=50";
            var resp = await _http.GetAsync(url);
            // Límite secundario de GitHub (ráfagas): esperar y reintentar antes de rendirse.
            for (int intento = 1; intento <= 3 && resp.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests; intento++)
            {
                var espera = resp.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(20 * intento);
                resp.Dispose();
                await Task.Delay(espera);
                resp = await _http.GetAsync(url);
            }
            using var _ = resp;
            if (resp.StatusCode == HttpStatusCode.NotFound)
                return _cache[repo] = null;
            if (resp.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
                throw new InvalidOperationException(
                    $"Límite de la API de GitHub alcanzado ({repo}: {await resp.Content.ReadAsStringAsync()}). " +
                    "Configura un token en NX-STUDIO (botón Token) o la variable GITHUB_TOKEN.");
            resp.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var lista = new List<ReleaseGitHub>();
            foreach (var r in doc.RootElement.EnumerateArray())
            {
                if (r.GetProperty("draft").GetBoolean()) continue;
                var assets = r.GetProperty("assets").EnumerateArray()
                    .Select(a => new AssetGitHub(
                        a.GetProperty("name").GetString()!,
                        a.GetProperty("browser_download_url").GetString()!))
                    .ToList();
                var fecha = r.TryGetProperty("published_at", out var p) && p.ValueKind == JsonValueKind.String
                    ? p.GetDateTimeOffset()
                    : r.GetProperty("created_at").GetDateTimeOffset();
                lista.Add(new ReleaseGitHub(
                    r.GetProperty("tag_name").GetString()!,
                    r.GetProperty("prerelease").GetBoolean(),
                    fecha, assets,
                    r.TryGetProperty("html_url", out var u) ? u.GetString() : null,
                    r.TryGetProperty("body", out var b) && b.ValueKind == JsonValueKind.String ? b.GetString() : null));
            }

            lista.Sort((a, b) => b.Publicada.CompareTo(a.Publicada));
            return _cache[repo] = lista;
        }

        public void Dispose() => _http.Dispose();
    }
}
