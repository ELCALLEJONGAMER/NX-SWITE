namespace NX_Swite.Models
{
    /// <summary>
    /// Datos resumidos de la microSD seleccionada que se muestran en el panel
    /// derecho (capacidad, formato, versi�n de Atmosphere detectada, serial).
    /// </summary>
    public class InfoPanelDerecho
    {
        public string Capacidad { get; set; } = "--";
        public string Formato { get; set; } = "--";
        public string VersionAtmos { get; set; } = "Desconocido";
        public string Serial { get; set; } = "N/A";

        // ── Compatibilidad de llaves (prod.keys) ────────────────────────
        /// <summary><c>true</c> si se encontró prod.keys en la SD.</summary>
        public bool   HayProdkeys        { get; set; }
        /// <summary>Clave máxima detectada (p.ej. "master_key_15"). Vacío si no hay prod.keys.</summary>
        public string MasterKeyMaxima    { get; set; } = string.Empty;
        /// <summary>Rango de firmware compatible (p.ej. "22.0.0 – 22.5.0").</summary>
        public string FirmwareCompatible { get; set; } = "--";
        /// <summary>Primera versión de Atmosphere que soporta estas llaves (p.ej. "1.11.0").</summary>
        public string AtmosphereDesde    { get; set; } = "--";
    }
}
