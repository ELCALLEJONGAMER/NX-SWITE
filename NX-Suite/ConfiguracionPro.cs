namespace NX_Suite
{
    /// <summary>
    /// Interruptores de sonido. Cada bool puede apagarse individualmente
    /// sin afectar al resto. SonidosActivos es el master switch.
    /// </summary>
    public static class ConfiguracionSonidos
    {
        // ── Master switch ────────────────────────────────────────────────
        public static bool SonidosActivos { get; set; } = true;

        // ── Por evento (se ignoran si SonidosActivos = false) ────────────
        public static bool Intro      { get; set; } = true;
        public static bool Cerrar     { get; set; } = true;
        public static bool Click      { get; set; } = true;
        public static bool Hover      { get; set; } = true;
        public static bool Instalar   { get; set; } = true;
        public static bool Exito      { get; set; } = true;
        public static bool Error      { get; set; } = true;
        public static bool Navegacion { get; set; } = true;

        // ── Parámetros de reproducción ───────────────────────────────────
        /// <summary>Volumen global 0.0 – 1.0</summary>
        public static double Volumen        { get; set; } = 0.8;
        /// <summary>Milisegundos mínimos entre dos hovers consecutivos (anti-spam)</summary>
        public static int    RetardoHoverMs { get; set; } = 120;
    }

    public static class ConfiguracionPro
    {
        public const string UrlGistPrincipal = "https://gist.githubusercontent.com/ELCALLEJONGAMER/57949a130fdc307033492e365780a4bd/raw/NX-SWITE.json";
        public const string UrlGistBeta = "https://gist.githubusercontent.com/usuario/id_beta/raw/test.json";
        public const string NombreManifiesto = ".nx-metadata.json";
        public const string CarpetaTemporal = "NX_Temp";

        // ── Caché del Gist ───────────────────────────────────────────────
        /// <summary>
        /// Tiempo de vida del caché del JSON (en horas).
        /// Si el caché tiene menos de este tiempo, se usa directamente sin descargar.
        /// Ponlo a 0 para forzar siempre la descarga.
        /// </summary>
        public const double TtlCacheGistHoras = 0;
    }
}