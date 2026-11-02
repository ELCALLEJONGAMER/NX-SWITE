using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NX_Swite.Services;

namespace NX_Swite.Core
{
    /// <summary>Release alternativa encontrada cuando el enlace del catálogo está caído.</summary>
    public sealed record AlternativaDescarga(
        string Repo,
        string Tag,
        string NombreAsset,
        string Url,
        DateTime? Fecha,
        bool EsPrerelease,
        bool MismaVersion);

    /// <summary>
    /// Busca una alternativa cuando una URL de GitHub Releases del catálogo devuelve
    /// 404/410 (el autor borró o renombró la release). Solo busca en el MISMO repo
    /// (respeta el origen) y solo releases con versión igual o superior a la pedida.
    /// El asset se elige por "esqueleto" del nombre (mismo patrón con otros números),
    /// de modo que el renombrado del catálogo (<c>ArchivoDestino</c>) no se altera.
    /// </summary>
    public static class ResolutorEnlaceCaido
    {
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

        private static readonly List<AlternativaDescarga> _sustituciones = new();

        /// <summary>
        /// Solo Atmosphère: su autor da de baja enlaces de releases y sin Atmosphère
        /// el paquete no arranca. El resto de módulos falla como siempre.
        /// </summary>
        public static bool AplicaA(string url)
            => ParsearUrlRelease(url, out _, out string repo, out _, out _)
               && repo.Contains("atmosphere", StringComparison.OrdinalIgnoreCase);

        /// <summary>Anota una sustitución automática para avisar al usuario al terminar.</summary>
        public static void RegistrarSustitucion(AlternativaDescarga alternativa)
        {
            lock (_sustituciones) _sustituciones.Add(alternativa);
        }

        /// <summary>Devuelve y vacía las sustituciones pendientes de avisar.</summary>
        public static List<AlternativaDescarga> TomarSustituciones()
        {
            lock (_sustituciones)
            {
                var copia = _sustituciones.ToList();
                _sustituciones.Clear();
                return copia;
            }
        }

        /// <summary>True si la excepción indica que el recurso ya no existe.</summary>
        public static bool EsEnlaceCaido(Exception ex)
        {
            for (var e = ex; e != null; e = e.InnerException)
                if (e is HttpRequestException h &&
                    (h.StatusCode == System.Net.HttpStatusCode.NotFound || h.StatusCode == System.Net.HttpStatusCode.Gone))
                    return true;
            return false;
        }

        /// <summary>
        /// Consulta las releases del repo de <paramref name="urlCaida"/> y devuelve
        /// las alternativas compatibles, ordenadas de la más cercana a la más nueva.
        /// </summary>
        public static async Task<List<AlternativaDescarga>> BuscarAlternativasAsync(
            string urlCaida, string versionPedida, string? token, CancellationToken ct)
        {
            var resultado = new List<AlternativaDescarga>();
            if (!ParsearUrlRelease(urlCaida, out string owner, out string repo, out string tagCaido, out string archivo))
                return resultado;

            Version? minima = ExtraerVersion(versionPedida) ?? ExtraerVersion(tagCaido);
            string esqueleto = Esqueleto(archivo);
            string extension = System.IO.Path.GetExtension(archivo);

            using var req = new HttpRequestMessage(HttpMethod.Get,
                $"https://api.github.com/repos/{owner}/{repo}/releases?per_page=30");
            req.Headers.UserAgent.ParseAdd("NX-Swite/1.0");
            req.Headers.Accept.ParseAdd("application/vnd.github+json");
            if (!string.IsNullOrWhiteSpace(token))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());

            using var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                Logger.Warning($"[EnlaceCaido] API de releases devolvió {(int)resp.StatusCode} para {owner}/{repo}.");
                return resultado;
            }

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            foreach (var rel in doc.RootElement.EnumerateArray())
            {
                if (rel.TryGetProperty("draft", out var d) && d.GetBoolean()) continue;

                string tag = rel.GetProperty("tag_name").GetString() ?? string.Empty;
                Version? vRel = ExtraerVersion(tag);
                if (minima != null && (vRel == null || vRel < minima)) continue;

                bool pre = rel.TryGetProperty("prerelease", out var p) && p.GetBoolean();
                DateTime? fecha = rel.TryGetProperty("published_at", out var f) && f.ValueKind == JsonValueKind.String
                    ? f.GetDateTime() : null;

                var assets = rel.GetProperty("assets").EnumerateArray()
                    .Select(a => (Nombre: a.GetProperty("name").GetString() ?? "",
                                  Url:    a.GetProperty("browser_download_url").GetString() ?? ""))
                    .Where(a => a.Nombre.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var elegido = assets.FirstOrDefault(a => Esqueleto(a.Nombre) == esqueleto);
                if (elegido.Nombre.Length == 0) continue;
                if (string.Equals(elegido.Url, urlCaida, StringComparison.OrdinalIgnoreCase)) continue;

                resultado.Add(new AlternativaDescarga($"{owner}/{repo}", tag, elegido.Nombre, elegido.Url,
                    fecha, pre, minima != null && vRel == minima));
            }

            return resultado
                .OrderBy(a => a.EsPrerelease)
                .ThenBy(a => ExtraerVersion(a.Tag))
                .ThenByDescending(a => a.Fecha)
                .ToList();
        }

        // ── Helpers puros (testeables) ─────────────────────────────────────

        /// <summary>
        /// Patrón del nombre sin números ni hashes: "hekate_ctcaer_6.5.4_Nyx_1.9.4.zip"
        /// → "hekate_ctcaer_#_nyx_#.zip". Dos assets del mismo "tipo" en versiones
        /// distintas comparten esqueleto.
        /// </summary>
        public static string Esqueleto(string nombre)
        {
            string s = Uri.UnescapeDataString(nombre).ToLowerInvariant();
            s = Regex.Replace(s, @"(?<![a-z])[0-9a-f]{7,40}(?![a-z])", m => m.Value.Any(char.IsDigit) ? "#" : m.Value);
            s = Regex.Replace(s, @"\d+", "#");
            s = Regex.Replace(s, @"#(?:[.\-]?#)+", "#");
            return s;
        }

        /// <summary>Primera versión numérica de un texto ("v1.12.1+" → 1.12.1).</summary>
        public static Version? ExtraerVersion(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return null;
            var m = Regex.Match(Uri.UnescapeDataString(texto), @"\d+(?:\.\d+){0,3}");
            if (!m.Success) return null;
            string v = m.Value.Contains('.') ? m.Value : m.Value + ".0";
            return Version.TryParse(v, out var ver) ? ver : null;
        }

        /// <summary>https://github.com/{owner}/{repo}/releases/download/{tag}/{archivo}</summary>
        public static bool ParsearUrlRelease(string url, out string owner, out string repo, out string tag, out string archivo)
        {
            owner = repo = tag = archivo = string.Empty;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
            if (!uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)) return false;

            string[] seg = uri.AbsolutePath.Trim('/').Split('/');
            if (seg.Length < 6 || !seg[2].Equals("releases", StringComparison.OrdinalIgnoreCase)
                               || !seg[3].Equals("download", StringComparison.OrdinalIgnoreCase))
                return false;

            owner   = seg[0];
            repo    = seg[1];
            tag     = Uri.UnescapeDataString(seg[4]);
            archivo = Uri.UnescapeDataString(seg[5]);
            return true;
        }
    }
}
