namespace NX_Suite.Models
{
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

    /// <summary>
    /// Sección independiente del Gist JSON para los colores configurables de NYX.
    /// Separada de ConfiguracionUI para que cada añado de colores no mezcle
    /// con el resto de la configuración de UI.
    /// </summary>
    public class NyxConfigColors
    {
        /// <summary>
        /// Presets de color de icono/acento (themecolor 0-359).
        /// Valor = número NYX. HexRgb = color exacto que verá el usuario en Hekate.
        /// </summary>
        public List<NyxColorPreset> Themecolors { get; set; } = new();

        /// <summary>
        /// Presets de color de fondo (themebg).
        /// IniValue = string exacto de 6 chars hex para nyx.ini.
        /// HexRgb = color de preview en UI.
        /// </summary>
        public List<NyxFondoPreset> Themebgs { get; set; } = new();
    }

    public class NyxColorPreset
    {
        public string Nombre { get; set; } = string.Empty; // "Rojo"
        public int    Valor  { get; set; }                  // NYX themecolor 0-359
        public string HexRgb { get; set; } = string.Empty; // "#FF0000" display aprox
    }

    /// <summary>
    /// Preset de color de fondo NYX (themebg).
    /// IniValue es el string exacto que va en nyx.ini (6 hex chars, ej: "0b0b64").
    /// HexRgb es el mismo color para preview en UI.
    /// </summary>
    public class NyxFondoPreset
    {
        public string Nombre   { get; set; } = string.Empty; // "Azul Marino"
        public string IniValue { get; set; } = string.Empty; // "0b0b64"
        public string HexRgb   { get; set; } = string.Empty; // "#0B0B64" display
    }
}
