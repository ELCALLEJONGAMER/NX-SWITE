namespace NX_Swite.Hardware
{
    /// <summary>
    /// Informaci�n de una unidad extra�ble detectada por <see cref="EscanerDiscos"/>.
    /// Es la estructura �nica que viaja entre Hardware y la UI (panel derecho,
    /// combo de unidades, vista asistida).
    /// </summary>
    public class SDInfo
    {
        public string Letra          { get; set; }
        public string Etiqueta       { get; set; }
        public string CapacidadTotal { get; set; }
        public string Formato        { get; set; }
        public string Serial         { get; set; }

        /// <summary>�ndice del disco f�sico (necesario para diskpart).</summary>
        public int    DiscoFisico    { get; set; }

        public string FullName => $"{Letra} ({Etiqueta}) - {CapacidadTotal}GB";
    }
}
