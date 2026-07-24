using System;
using System.Collections.Generic;

namespace NX_Swite.Core
{
    /// <summary>
    /// Tabla de modelos de Nintendo Switch mapeados por prefijo de serial.
    ///
    /// <para>Formato del pipe-string (Gist):</para>
    /// <c>XAW, Nintendo Switch V1, América | XAJ, Nintendo Switch V1, Japón | ...</c>
    ///
    /// <para>La tabla base está embebida en el binario. <see cref="AplicarRemota"/> la
    /// extiende (o sobreescribe entradas) con los datos del Gist sin recompilar.</para>
    /// </summary>
    public static class ModeloSwitchTable
    {
        public record Entry(string Prefijo, string Modelo, string Region);

        // ?? Tabla base embebida ???????????????????????????????????????????
        private static readonly Entry[] _base =
        [
            // V1 — Erista (2017)
            new("XAW", "Nintendo Switch V1",   "América"),
            new("XAE", "Nintendo Switch V1",   "Europa"),
            new("XAJ", "Nintendo Switch V1",   "Japón"),
            // V2 — Mariko (2019)
            new("XKW", "Nintendo Switch V2",   "América"),
            new("XKE", "Nintendo Switch V2",   "Europa"),
            new("XKJ", "Nintendo Switch V2",   "Japón"),
            // Lite — Hoag (2019)
            new("XJW", "Nintendo Switch Lite", "América"),
            new("XJE", "Nintendo Switch Lite", "Europa"),
            new("XJJ", "Nintendo Switch Lite", "Japón"),
            // OLED — Aula (2021)
            new("XTW", "Nintendo Switch OLED", "América"),
            new("XTE", "Nintendo Switch OLED", "Europa"),
            new("XTJ", "Nintendo Switch OLED", "Japón"),
        ];

        private static List<Entry> _fusionada = new(_base);

        /// <summary>
        /// Fusiona la tabla remota del Gist sobre la base embebida.
        /// Entradas con el mismo prefijo (ignorando mayúsculas) son reemplazadas;
        /// prefijos nuevos son añadidos.
        /// </summary>
        public static void AplicarRemota(string? pipeString)
        {
            _fusionada = new List<Entry>(_base);
            if (string.IsNullOrWhiteSpace(pipeString)) return;

            foreach (string segmento in pipeString.Split('|',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string[] partes = segmento.Split(',', StringSplitOptions.TrimEntries);
                if (partes.Length < 3) continue;

                string prefijo = partes[0].Trim().ToUpperInvariant();
                string modelo  = partes[1].Trim();
                string region  = partes[2].Trim();

                int idx = _fusionada.FindIndex(e =>
                    string.Equals(e.Prefijo, prefijo, StringComparison.OrdinalIgnoreCase));
                var entry = new Entry(prefijo, modelo, region);
                if (idx >= 0)
                    _fusionada[idx] = entry;
                else
                    _fusionada.Add(entry);
            }
        }

        /// <summary>
        /// Resuelve el modelo y región a partir del serial completo.
        /// Compara los primeros caracteres del serial con cada prefijo conocido.
        /// Devuelve <c>null</c> si no hay coincidencia.
        /// </summary>
        public static Entry? Resolver(string? serial)
        {
            if (string.IsNullOrWhiteSpace(serial)) return null;
            string s = serial.Trim().ToUpperInvariant();
            foreach (var entry in _fusionada)
            {
                if (s.StartsWith(entry.Prefijo, StringComparison.OrdinalIgnoreCase))
                    return entry;
            }
            return null;
        }
    }
}
