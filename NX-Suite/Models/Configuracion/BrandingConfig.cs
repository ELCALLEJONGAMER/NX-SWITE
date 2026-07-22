namespace NX_Swite.Models
{
    /// <summary>
    /// Identidad visual de la aplicaci�n servida desde el Gist
    /// (nombre, logo, color de acento global, banner por defecto).
    /// </summary>
    public class BrandingConfig
    {
        public string NombrePrograma { get; set; } = string.Empty;
        public string LogoUrl { get; set; } = string.Empty;
        public string ColorAcentoGlobal { get; set; } = "#00D2FF";
        public string BannerPorDefectoUrl { get; set; } = string.Empty;
    }
}
