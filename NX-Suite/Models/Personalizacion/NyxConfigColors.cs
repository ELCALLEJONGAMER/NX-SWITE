using System.Collections.Generic;

namespace NX_Swite.Models
{
    /// <summary>
    /// Secci�n independiente del Gist JSON para los colores configurables de NYX.
    /// Separada de ConfiguracionUI para que cada a�adido de colores no mezcle
    /// con el resto de la configuraci�n de UI.
    /// </summary>
    public class NyxConfigColors
    {
        /// <summary>
        /// Presets de color de icono/acento (themecolor 0-359).
        /// Valor = n�mero NYX. HexRgb = color exacto que ver� el usuario en Hekate.
        /// </summary>
        public List<NyxColorPreset> Themecolors { get; set; } = new();

        /// <summary>
        /// Presets de color de fondo (themebg).
        /// IniValue = string exacto de 6 chars hex para nyx.ini.
        /// HexRgb = color de preview en UI.
        /// </summary>
        public List<NyxFondoPreset> Themebgs { get; set; } = new();
    }
}
