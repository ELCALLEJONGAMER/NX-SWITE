namespace NX_Suite
{
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
        public const double TtlCacheGistHoras = 1.0;
    }
}