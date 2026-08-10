using NX_Swite.Core;
using NX_Swite.Core.Configuracion;
using NX_Swite.Models;
using NX_Swite.Services;
using NX_Swite.UI;
using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly GestorIconos? _gestorIconos;

        // ── Single-flight de revalidación en background ──────────────────
        // Evita que múltiples consumidores (varias llamadas casi simultáneas a
        // ObtenerTodoElGistAsync) disparen revalidaciones HTTP concurrentes
        // contra el mismo Gist. Mientras una revalidación siga activa, los
        // demás consumidores reutilizan la misma Task en curso.
        private readonly object _revalidacionLock = new();
        private Task? _revalidacionEnCurso;

        /// <summary>
        /// Se dispara cuando la revalidación en background detecta que el Gist
        /// cambió y actualizó el caché en disco. El argumento contiene los nuevos datos.
        /// Los suscriptores deben volver al hilo de UI antes de tocar controles.
        /// </summary>
        public event Action<GistData>? GistActualizadoEnBackground;

        public GistParser(GestorCache gestorCache, GestorIconos? gestorIconos = null)
        {
            _gestorCache  = gestorCache ?? throw new ArgumentNullException(nameof(gestorCache));
            _gestorIconos = gestorIconos;
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

                // Revalidar en background sin bloquear la UI. Coalescida:
                // si ya hay una revalidación activa para este Gist, los
                // consumidores adicionales reutilizan esa misma operación
                // en lugar de disparar otra petición HTTP.
                _ = RevalidarConETagCoalescedAsync(urlGistRaw, opciones);

                return datos;
            }

            // ── 2. Sin caché: descarga completa (primera vez) ────────────
            Logger.GistSincronizacionIniciada();
            return await DescargarCompletoAsync(urlGistRaw, opciones, etagActual: null);
        }

        // ── Single-flight: coalescer revalidaciones concurrentes ─────────

        private Task RevalidarConETagCoalescedAsync(string urlGistRaw, JsonSerializerOptions opciones)
        {
            lock (_revalidacionLock)
            {
                // Ya hay una revalidación activa: reutilizarla, no iniciar otra petición HTTP.
                if (_revalidacionEnCurso != null && !_revalidacionEnCurso.IsCompleted)
                    return _revalidacionEnCurso;

                Logger.GistSincronizacionIniciada();
                _revalidacionEnCurso = EjecutarRevalidacionAsync(urlGistRaw, opciones);
                return _revalidacionEnCurso;
            }
        }

        private async Task EjecutarRevalidacionAsync(string urlGistRaw, JsonSerializerOptions opciones)
        {
            try
            {
                await RevalidarConETagAsync(urlGistRaw, opciones);
            }
            finally
            {
                // Liberar el estado siempre: éxito, 304, fallo o excepción.
                lock (_revalidacionLock)
                    _revalidacionEnCurso = null;
            }
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
                {
                    Logger.GistSinCambios();
                    return; // El Gist no cambió, caché sigue siendo válido
                }

                if (!response.IsSuccessStatusCode)
                    return; // Error de red no crítico: conservar caché actual

                string jsonContent = await response.Content.ReadAsStringAsync();
                var nuevaData = JsonSerializer.Deserialize<GistData>(jsonContent, opciones);
                if (nuevaData == null) return;

                // Invalidar iconos en caché: el Gist cambió, cualquier imagen
                // apuntada por una URL raw de GitHub puede tener contenido nuevo.
                if (_gestorIconos != null)
                    _gestorIconos.InvalidarIconos(ExtraerUrlsIconos(nuevaData));

                // Guardar nuevo JSON y el nuevo ETag
                await _gestorCache.GuardarJsonGistAsync(jsonContent);

                string? nuevoEtag = response.Headers.ETag?.Tag;
                if (!string.IsNullOrWhiteSpace(nuevoEtag))
                    await _gestorCache.GuardarETagGistAsync(nuevoEtag);

                // Notificar a los suscriptores (p.ej. MainWindow) para que
                // re-apliquen las tablas remotas y refresquen el panel derecho.
                Logger.GistConfiguracionActualizada();
                GistActualizadoEnBackground?.Invoke(nuevaData);
            }
            catch (Exception ex)
            {
                // No crítico: la caché local sigue siendo válida y la app continúa funcionando.
                Logger.GistRevalidacionFallida(ex.Message);
            }
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
                Logger.GistSincronizacionFallida($"JSON remoto con formato inválido (línea {jsonEx.LineNumber})");
                Dialogos.Error(
                    $"Error de sintaxis en el JSON remoto:\nLínea {jsonEx.LineNumber}, Posición {jsonEx.BytePositionInLine}\nDetalle: {jsonEx.Message}",
                    "Error de Gist");
                return null;
            }
            catch (Exception ex)
            {
                Logger.GistUsoCacheOffline($"error de red en la descarga inicial ({ex.Message}), se intentará usar caché local");
                return await IntentarCargarDesdeCacheAsync(opciones);
            }
        }

        // ── Fallback offline ─────────────────────────────────────────────

        // ── Extractor de URLs de iconos del Gist ─────────────────────────

        private static IEnumerable<string> ExtraerUrlsIconos(GistData data)
        {
            var urls = new List<string>();

            // Iconos de módulos y banners
            if (data.Modulos != null)
            {
                foreach (var m in data.Modulos)
                {
                    if (!string.IsNullOrWhiteSpace(m.IconoUrl))  urls.Add(m.IconoUrl);
                    if (!string.IsNullOrWhiteSpace(m.BannerUrl)) urls.Add(m.BannerUrl);
                    if (m.ScreenshotsUrl != null) urls.AddRange(m.ScreenshotsUrl.Where(u => !string.IsNullOrWhiteSpace(u)));
                }
            }

            // Iconos de UI (ConfiguracionUI)
            var ui = data.ConfiguracionUI;
            if (ui != null)
            {
                var campos = new[]
                {
                    ui.IconoCacheUrl, ui.IconoEliminarUrl, ui.IconoAgregarUrl, ui.IconoVolverUrl,
                    ui.IconoSiguienteUrl, ui.IconoPaginaAnteriorUrl, ui.IconoPaginaSiguienteUrl,
                    ui.IconoZipUrl, ui.IconoQueueUrl, ui.IconoBellUrl, ui.IconoMailUrl,
                    ui.IconoUpdateUrl, ui.IconoMicroSDUrl, ui.IconoPaintUrl, ui.IconoInfoUrl,
                    ui.IconoEjectUrl, ui.IconoConfigUrl, ui.IconoCarpetaUrl, ui.IconoArchivoUrl,
                    ui.IconoShieldUrl, ui.IconoLogUrl, ui.IconoRp2040Url,
                };
                urls.AddRange(campos.Where(u => !string.IsNullOrWhiteSpace(u))!);
            }

            // Iconos de mundos del menú
            if (data.MundosMenu != null)
                foreach (var m in data.MundosMenu)
                    if (!string.IsNullOrWhiteSpace(m.IconoUrl)) urls.Add(m.IconoUrl);

            return urls.Distinct(StringComparer.OrdinalIgnoreCase);
        }

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
                Logger.GistSincronizacionFallida("sin conexión a internet y no existe una configuración en caché");
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

                Logger.GistUsoCacheOffline($"sin conexión a internet, usando caché de {fechaTexto}");
                Dialogos.Info(
                    $"Sin conexión a internet.\nCargando catálogo desde caché local ({fechaTexto}).",
                    "Modo offline");

                return resultado ?? new GistData();
            }
            catch (JsonException)
            {
                Logger.GistSincronizacionFallida("la caché local está dañada y no hay conexión a internet");
                Dialogos.Error(
                    "Sin conexión y el caché local está dañado. No se puede cargar el catálogo.",
                    "Error de caché");
                return null;
            }
        }
    }
}
