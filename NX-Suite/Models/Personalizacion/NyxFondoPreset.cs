namespace NX_Suite.Models
{
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
