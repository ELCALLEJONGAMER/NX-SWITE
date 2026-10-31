namespace NX_Swite.Models
{
    /// <summary>
    /// Resultado mínimo de la detección de un cheat instalado en la SD:
    /// carpeta <c>/atmosphere/contents/&lt;TITLE_ID&gt;/cheats/</c> con al
    /// menos un archivo dentro. No se calcula hash ni se lee el contenido de
    /// los archivos de cheats.
    /// </summary>
    public sealed class CheatDetectado
    {
        public string TitleId { get; init; } = string.Empty;
        public string NombreJuego { get; init; } = string.Empty;
        public int CantidadArchivos { get; init; }

        /// <summary>Ruta relativa dentro de la SD, ej. "atmosphere/contents/0100.../cheats".</summary>
        public string RutaRelativa { get; init; } = string.Empty;
    }
}
