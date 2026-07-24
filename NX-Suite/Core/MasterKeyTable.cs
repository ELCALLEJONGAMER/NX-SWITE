using System;
using System.Collections.Generic;

namespace NX_Swite.Core
{
    /// <summary>
    /// Tabla de relación entre <c>master_key_XX</c>, rango de Horizon OS compatible
    /// y primera versión de Atmosphere que la soportó.
    ///
    /// <para><b>Estrategia híbrida:</b> las entradas embebidas en el binario sirven
    /// de base offline. Al sincronizar el Gist se llama a
    /// <see cref="AplicarRemota(string)"/> con el valor de
    /// <c>ConfiguracionUI.TablaMasterKeys</c>; las entradas remotas se fusionan
    /// sobre las embebidas (remota tiene prioridad). Las claves conocidas siguen
    /// funcionando aunque el Gist no llegue.</para>
    ///
    /// <para><b>Formato del campo en el Gist</b> (<c>tabla_master_keys</c>):</para>
    /// <code>
    /// master_key_15: 22.0.0-22.5.0, 1.11.0 | master_key_16: 23.x, 1.12.0
    /// </code>
    /// Cada entrada: <c>nombre_clave: rango_hos, atmos_desde</c>
    /// separadas por <c>|</c>. Los espacios alrededor son ignorados.
    /// </summary>
    internal static class MasterKeyTable
    {
        /// <summary>Una fila de la tabla de compatibilidad.</summary>
        public sealed record Entry(
            string MasterKey,
            string RangoHosCompatible,
            string AtmosphereDesde);

        // ?? Tabla embebida (base offline) ?????????????????????????????????
        private static readonly Entry[] _base =
        [
            new("master_key_00", "1.0.0 - 2.3.0",   "0.7.0"),
            new("master_key_01", "3.0.0",            "0.7.0"),
            new("master_key_02", "3.0.1 - 3.0.2",   "0.7.0"),
            new("master_key_03", "4.0.0 - 4.1.0",   "0.7.0"),
            new("master_key_04", "5.0.0 - 5.1.0",   "0.7.0"),
            new("master_key_05", "6.0.0 - 6.1.0",   "0.7.0"),
            new("master_key_06", "6.2.0",            "0.8.0"),
            new("master_key_07", "7.0.0 - 8.0.x",   "0.8.4"),
            new("master_key_08", "8.1.0",            "0.9.1"),
            new("master_key_09", "9.0.x",            "0.9.4"),
            new("master_key_0a", "9.1.0 - 12.0.3",  "0.10.0"),
            new("master_key_0b", "12.1.0",           "0.19.5"),
            new("master_key_0c", "13.0.0 - 13.2.1", "1.1.0"),
            new("master_key_0d", "14.x",             "1.3.0"),
            new("master_key_0e", "15.x",             "1.4.0"),
            new("master_key_0f", "16.x",             "1.5.0"),
            new("master_key_10", "17.x",             "1.6.0"),
            new("master_key_11", "18.x",             "1.7.0"),
            new("master_key_12", "19.x",             "1.8.0"),
            new("master_key_13", "20.0.0 - 20.5.0", "1.9.0"),
            new("master_key_14", "21.0.0 - 21.2.0", "1.10.0"),
            new("master_key_15", "22.0.0 - 22.5.0", "1.11.0"),
        ];

        // ?? Tabla fusionada en memoria (base + remota) ????????????????????
        private static Dictionary<string, Entry> _fusionada = Inicializar();

        // ?? Solo entradas remotas (Gist) — nunca contiene la base embebida ???
        private static Dictionary<string, Entry> _soloRemota = new(StringComparer.OrdinalIgnoreCase);

        private static Dictionary<string, Entry> Inicializar()
        {
            var d = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in _base)
                d[e.MasterKey] = e;
            return d;
        }

        // ?? API pública ???????????????????????????????????????????????????

        /// <summary>
        /// Fusiona las entradas remotas venidas del Gist sobre la tabla base.
        /// Las entradas remotas tienen prioridad; las base que no aparezcan en
        /// remota se conservan. Las claves nuevas que no estén en la base se agregan.
        ///
        /// <para>Llamar una vez al sincronizar el Gist, antes de cualquier análisis.</para>
        /// </summary>
        /// <param name="tablaRaw">
        /// Valor de <c>ConfiguracionUI.TablaMasterKeys</c>.
        /// Formato: <c>master_key_15: 22.0.0-22.5.0, 1.11.0 | master_key_16: 23.x, 1.12.0</c>
        /// </param>
        public static void AplicarRemota(string? tablaRaw)
        {
            if (string.IsNullOrWhiteSpace(tablaRaw)) return;

            var nueva      = Inicializar(); // parte siempre de la base
            var soloRemota = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

            foreach (string trozo in tablaRaw.Split('|',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                // Formato esperado: "master_key_15: 22.0.0-22.5.0, 1.11.0"
                int dospuntos = trozo.IndexOf(':');
                if (dospuntos <= 0) continue;

                string nombre = trozo[..dospuntos].Trim().ToLowerInvariant();
                string resto  = trozo[(dospuntos + 1)..].Trim();

                // resto = "rango_hos, atmos_desde"
                int coma  = resto.IndexOf(',');
                string rango = coma > 0 ? resto[..coma].Trim()       : resto.Trim();
                string atmos = coma > 0 ? resto[(coma + 1)..].Trim() : string.Empty;

                if (string.IsNullOrEmpty(nombre)) continue;

                var entry = new Entry(nombre, rango, atmos);
                nueva[nombre]      = entry;  // fusionada (remota sobre base)
                soloRemota[nombre] = entry;  // solo lo que dijo el Gist
            }

            _fusionada  = nueva;
            _soloRemota = soloRemota;
        }

        /// <summary>
        /// Busca la entrada de <paramref name="masterKey"/> en la tabla fusionada.
        /// Devuelve <c>null</c> si la clave no está en ninguna tabla.
        /// </summary>
        public static Entry? Buscar(string masterKey)
            => _fusionada.TryGetValue(masterKey.Trim(), out var e) ? e : null;

        /// <summary>
        /// Busca la entrada de <paramref name="masterKey"/> <b>solo</b> entre las
        /// entradas que vinieron del Gist (nunca usa la base embebida).
        /// Devuelve <c>null</c> si el Gist no ha llegado aún o no define esa clave.
        /// </summary>
        public static Entry? BuscarSoloRemota(string masterKey)
            => _soloRemota.TryGetValue(masterKey.Trim(), out var e) ? e : null;

        /// <summary>Número total de entradas en la tabla fusionada actualmente.</summary>
        public static int Total => _fusionada.Count;
    }
}
