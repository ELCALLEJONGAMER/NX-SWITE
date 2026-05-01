namespace NX_Suite.Models
{
    /// <summary>
    /// Tema visual descargable que aplica simultáneamente a Hekate (imagen +
    /// hekate_ipl.ini) y a NYX (color de acento + fondo).
    /// </summary>
    public class TemaConfig
    {
        public string Id             { get; set; } = string.Empty;
        public string Nombre         { get; set; } = string.Empty;
        public string Autor          { get; set; } = string.Empty;
        public string Descripcion    { get; set; } = string.Empty;
        public string PreviewUrl     { get; set; } = string.Empty;
        public string Version        { get; set; } = string.Empty;
        public bool   EsOficial      { get; set; } = false;
        public bool   Aplicado       { get; set; } = false;

        // Hekate: imagen PNG 720x1280 para /bootloader/
        public string HekateImagenUrl { get; set; } = string.Empty;
        public string HekateIniUrl    { get; set; } = string.Empty;

        // NYX: color de acento e imagen de fondo
        public string NyxColorAcento  { get; set; } = string.Empty;
        public string NyxFondoUrl     { get; set; } = string.Empty;
    }
}
