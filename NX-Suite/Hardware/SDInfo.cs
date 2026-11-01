namespace NX_Swite.Hardware
{
    /// <summary>
    /// Información de una unidad extraíble detectada por <see cref="EscanerDiscos"/>.
    /// Es la estructura única que viaja entre Hardware y la UI (panel derecho,
    /// combo de unidades, vista asistida).
    /// </summary>
    public class SDInfo
    {
        public string Letra          { get; set; } = string.Empty;
        public string Etiqueta       { get; set; } = string.Empty;
        public string CapacidadTotal { get; set; } = string.Empty;
        public string Formato        { get; set; } = string.Empty;
        public string Serial         { get; set; } = string.Empty;

        /// <summary>Índice del disco físico (necesario para diskpart).</summary>
        public int    DiscoFisico    { get; set; }

        public string FullName => $"{Letra} ({Etiqueta}) - {CapacidadTotal}GB";

        public override string ToString() => FullName;
    }
}
