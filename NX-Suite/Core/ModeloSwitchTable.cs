using System;

namespace NX_Swite.Core
{
    /// <summary>
    /// Tabla estática de modelos de Nintendo Switch por prefijo de serial
    /// (3 primeros caracteres). No viene del catálogo: los modelos no cambian.
    /// </summary>
    public static class ModeloSwitchTable
    {
        public record Entry(string Prefijo, string Modelo, string Region);

        private static readonly (string Letra, string Modelo)[] _modelos =
        [
            ("A", "Nintendo Switch V1"),   // Erista (2017)
            ("K", "Nintendo Switch V2"),   // Mariko (2019)
            ("J", "Nintendo Switch Lite"), // Hoag (2019)
            ("T", "Nintendo Switch OLED"), // Aula (2021)
        ];

        private static readonly (string Letra, string Region)[] _regiones =
        [
            ("W", "América"),
            ("E", "Europa"),
            ("J", "Japón"),
            ("K", "Corea"),
            ("C", "China"),
            ("M", "Malasia"),
        ];

        /// <summary>
        /// Resuelve el modelo y región a partir del serial completo.
        /// Devuelve <c>null</c> si el prefijo no corresponde a ningún modelo conocido.
        /// </summary>
        public static Entry? Resolver(string? serial)
        {
            if (string.IsNullOrWhiteSpace(serial)) return null;
            string s = serial.Trim().ToUpperInvariant();
            if (s.Length < 3 || s[0] != 'X') return null;

            foreach (var (letraModelo, modelo) in _modelos)
            {
                if (s[1] != letraModelo[0]) continue;
                foreach (var (letraRegion, region) in _regiones)
                {
                    if (s[2] == letraRegion[0])
                        return new Entry(s[..3], modelo, region);
                }
            }
            return null;
        }
    }
}
