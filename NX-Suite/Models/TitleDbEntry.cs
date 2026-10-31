namespace NX_Swite.Models
{
    /// <summary>
    /// Entrada mínima de un TitleDB genérico: TitleId (16 hex) y nombre del
    /// juego. Se ignoran el resto de campos que pueda traer el JSON remoto
    /// (icon, region, etc.) — solo interesa el nombre para mostrarlo en la UI.
    /// </summary>
    public sealed class TitleDbEntry
    {
        public string TitleId { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
    }
}
