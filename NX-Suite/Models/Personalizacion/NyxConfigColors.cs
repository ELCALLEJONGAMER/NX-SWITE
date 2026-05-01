using System.Collections.Generic;

namespace NX_Suite.Models
{
    /// <summary>
    /// Sección independiente del Gist JSON para los colores configurables de NYX.
    /// Separada de ConfiguracionUI para que cada añadido de colores no mezcle
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
}
