using NX_Suite.Core;
using NX_Suite.Core.Configuracion;
using NX_Suite.Models;
using NX_Suite.UI;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace NX_Suite.Network
{
    public class GistParser
    {
        private static readonly HttpClient _client = new HttpClient();
        private readonly GestorCache _gestorCache;

        public GistParser(GestorCache gestorCache)
        {
            _gestorCache = gestorCache ?? throw new ArgumentNullException(nameof(gestorCache));
        }

        public async Task<GistData?> ObtenerTodoElGistAsync(string urlGistRaw)
        {
            var opciones = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true
            };

            // ── 1. ¿El caché local sigue siendo válido según el TTL? ──────
            if (_gestorCache.CacheGistEsValido(ConfiguracionLocal.TtlCacheGistHoras))
                return await CargarDesdeCacheSilenciosoAsync(opciones);

            // ── 2. Intentamos descargar desde la red ─────────────────────
            try
            {
                string urlAntiCache = $"{urlGistRaw}?t={DateTime.Now.Ticks}";
                string jsonContent  = await _client.GetStringAsync(urlAntiCache);

                var resultado = JsonSerializer.Deserialize<GistData>(jsonContent, opciones);

                if (resultado != null)
                {
                    await _gestorCache.GuardarJsonGistAsync(jsonContent);
                    return resultado;
                }

                return new GistData();
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
                // ── 3. Sin red: intentamos cargar desde el caché local ────
                return await IntentarCargarDesdeCacheAsync(opciones);
            }
        }

        /// <summary>
        /// Carga el caché sin avisar al usuario (el caché está fresco, es transparente).
        /// </summary>
        private async Task<GistData?> CargarDesdeCacheSilenciosoAsync(JsonSerializerOptions opciones)
        {
            string? json = await _gestorCache.CargarJsonGistAsync();
            if (string.IsNullOrWhiteSpace(json)) return null;

            try
            {
                return JsonSerializer.Deserialize<GistData>(json, opciones) ?? new GistData();
            }
            catch (JsonException)
            {
                return null;
            }
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