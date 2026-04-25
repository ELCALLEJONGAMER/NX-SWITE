namespace NX_Suite.Models
{
    /// <summary>
    /// Datos resumidos de la microSD seleccionada que se muestran en el panel
    /// derecho (capacidad, formato, versión de Atmosphere detectada, serial).
    /// </summary>
    public class InfoPanelDerecho
    {
        public string Capacidad { get; set; } = "--";
        public string Formato { get; set; } = "--";
        public string VersionAtmos { get; set; } = "Desconocido";
        public string Serial { get; set; } = "N/A";
    }
}
