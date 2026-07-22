using NX_Swite.Core;
using NX_Swite.Core;
using NX_Swite.Core.Configuracion;
using NX_Swite.Models;
using NX_Swite.UI;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace NX_Swite.Network
{
    public class GistParser
    {
        private static readonly HttpClient _client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(8)
        };

        private readonly GestorCache _gestorCache;

        public GistParser(GestorCache gestorCache)
        {
            _gestorCache = gestorCache ?? throw new ArgumentNullException(nameof(gestorCache));
        }

        /// <summary>
        /// Estrategia Stale-While-Revalidate con ETag:
        ///
        ///  1. Si hay caché local → retorna inmediatamente desde disco.
        ///     En background comprueba con <c>If-None-Match</c> si el Gist cambió:
        ///     · 304 Not Modified → no hace nada (caché sigue válido).
        ///     · 200 con nuevo JSON → actualiza caché + ETag silenciosamente.
        ///
        ///  2. Si no hay caché → descarga completa (primera ejecución).
        ///
        ///  3. Sin red y sin caché → muestra aviso al usuario.
        /// </summary>
        public async Task<GistData?> ObtenerTodoElGistAsync(string urlGistRaw)
        {
            var opciones = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true
            };

            // ── 1. ¿Existe caché local? → servir inmediatamente ──────────
            bool tieneCache = _gestorCache.TieneCacheGist;

            if (tieneCache)
            {
                var datos = await CargarDesdeCacheSilenciosoAsync(opciones);

                // Revalidar en background sin bloquear la UI
                _ = RevalidarConETagAsync(urlGistRaw, opciones);

                return datos;
            }

            // ── 2. Sin caché: descarga completa (primera vez) ────────────
            return await DescargarCompletoAsync(urlGistRaw, opciones, etagActual: null);
        }

        // ── Revalidación condicional (If-None-Match) ─────────────────────

        private async Task RevalidarConETagAsync(string urlGistRaw, JsonSerializerOptions opciones)
        {
            try
            {
                string? etag = await _gestorCache.CargarETagGistAsync();

                var request = new HttpRequestMessage(HttpMethod.Get, urlGistRaw);
                if (!string.IsNullOrWhiteSpace(etag))
                    request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(etag, isWeak: true));

                using var response = await _client.SendAsync(request);

                if (response.StatusCode == HttpStatusCode.NotModified)
                    return; // El Gist no cambió, caché sigue siendo válido

                if (!response.IsSuccessStatusCode)
                    return; // Error de red no crítico: conservar caché actual

                string jsonContent = await response.Content.ReadAsStringAsync();
                var nuevaData = JsonSerializer.Deserialize<GistData>(jsonContent, opciones);
                if (nuevaData == null) return;

                // Guardar nuevo JSON y el nuevo ETag
                await _gestorCache.GuardarJsonGistAsync(jsonContent);

                string? nuevoEtag = response.Headers.ETag?.Tag;
                if (!string.IsNullOrWhiteSpace(nuevoEtag))
                    await _gestorCache.GuardarETagGistAsync(nuevoEtag);
            }
            catch { /* Silencioso: fallo de red en background no es crítico */ }
        }

        // ── Descarga completa (primera ejecución, sin caché) ─────────────

        private async Task<GistData?> DescargarCompletoAsync(
            string urlGistRaw,
            JsonSerializerOptions opciones,
            string? etagActual)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, urlGistRaw);
                if (!string.IsNullOrWhiteSpace(etagActual))
                    request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(etagActual, isWeak: true));

                using var response = await _client.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                    return await IntentarCargarDesdeCacheAsync(opciones);

                string jsonContent = await response.Content.ReadAsStringAsync();

                var resultado = JsonSerializer.Deserialize<GistData>(jsonContent, opciones);
                if (resultado == null) return new GistData();

                await _gestorCache.GuardarJsonGistAsync(jsonContent);

                string? nuevoEtag = response.Headers.ETag?.Tag;
                if (!string.IsNullOrWhiteSpace(nuevoEtag))
                    await _gestorCache.GuardarETagGistAsync(nuevoEtag);

                return resultado;
            }
            catch (JsonException jsonEx)
            {
                Dialogos.Error(
                    $"Error de sintaxis en el JSON remoto:\nLínea {jsonEx.LineNumber}, Posición {jsonEx.BytePositionInLine}\nDetalle: {jsonEx.Message}",
                    "Error de Gist");
                return null;
            }
            catch (Exception)
            {
                return await IntentarCargarDesdeCacheAsync(opciones);
            }
        }

        // ── Fallback offline ─────────────────────────────────────────────

        private async Task<GistData?> CargarDesdeCacheSilenciosoAsync(JsonSerializerOptions opciones)
        {
            string? json = await _gestorCache.CargarJsonGistAsync();
            if (string.IsNullOrWhiteSpace(json)) return null;

            try { return JsonSerializer.Deserialize<GistData>(json, opciones) ?? new GistData(); }
            catch (JsonException) { return null; }
        }

        private async Task<GistData?> IntentarCargarDesdeCacheAsync(JsonSerializerOptions opciones)
        {
            string? jsonCacheado = await _gestorCache.CargarJsonGistAsync();

            if (string.IsNullOrWhiteSpace(jsonCacheado))
            {
                Dialogos.Advertencia(
                    "Sin conexión a internet y no hay datos en caché.\nConéctate a internet para cargar el catálogo por primera vez.",
                    "Sin conexión");
                return null;
            }

            try
            {
                var resultado = JsonSerializer.Deserialize<GistData>(jsonCacheado, opciones);

                DateTime? fecha = _gestorCache.FechaUltimaCacheGist;
                string fechaTexto = fecha.HasValue
                    ? fecha.Value.ToString("dd/MM/yyyy HH:mm")
                    : "fecha desconocida";

                Dialogos.Info(
                    $"Sin conexión a internet.\nCargando catálogo desde caché local ({fechaTexto}).",
                    "Modo offline");

                return resultado ?? new GistData();
            }
            catch (JsonException)
            {
                Dialogos.Error(
                    "Sin conexión y el caché local está dañado. No se puede cargar el catálogo.",
                    "Error de caché");
                return null;
            }
        }
    }
}
