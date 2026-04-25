namespace NX_Suite.Core.Configuracion
{
    /// <summary>
    /// Interruptores de sonido. Cada bool puede apagarse individualmente sin
    /// afectar al resto. <see cref="SonidosActivos"/> es el master switch que
    /// silencia todo de golpe.
    /// </summary>
    public static class ConfiguracionSonidos
    {
        // ?? Master switch ????????????????????????????????????????????????
        public static bool SonidosActivos { get; set; } = true;

        // ?? Por evento (se ignoran si SonidosActivos = false) ????????????
        public static bool Intro      { get; set; } = true;
        public static bool Cerrar     { get; set; } = true;
        public static bool Click      { get; set; } = true;
        public static bool Hover      { get; set; } = true;
        public static bool Instalar   { get; set; } = true;
        public static bool Exito      { get; set; } = true;
        public static bool Error      { get; set; } = true;
        public static bool Navegacion { get; set; } = true;

        // ?? Parámetros de reproducción ???????????????????????????????????
        /// <summary>Volumen global 0.0 – 1.0</summary>
        public static double Volumen        { get; set; } = 0.8;

        /// <summary>Milisegundos mínimos entre dos hovers consecutivos (anti-spam).</summary>
        public static int    RetardoHoverMs { get; set; } = 60;
    }
}
