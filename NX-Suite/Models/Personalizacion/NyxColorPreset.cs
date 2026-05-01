namespace NX_Suite.Models
{
    /// <summary>
    /// Preset de color de icono/acento de NYX (themecolor 0-359).
    /// </summary>
    public class NyxColorPreset
    {
        public string Nombre { get; set; } = string.Empty; // "Rojo"
        public int    Valor  { get; set; }                  // NYX themecolor 0-359
        public string HexRgb { get; set; } = string.Empty; // "#FF0000" display aprox
    }
}
