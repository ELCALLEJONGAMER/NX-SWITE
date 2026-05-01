using System.Collections.Generic;

namespace NX_Suite.Models
{
    /// <summary>
    /// Raíz del JSON remoto (Gist). Contiene toda la configuración descargable:
    /// branding, sonidos, mundos, módulos, recomendados, temas, etc.
    /// </summary>
    public class GistData
    {
        public ConfiguracionUI          ConfiguracionUI      { get; set; } = new();
        public NyxConfigColors          NyxConfigColors      { get; set; } = new();
        public BrandingConfig           GlobalBranding       { get; set; } = new();
        public SonidosConfig            Sonidos              { get; set; } = new();
        public List<ModuloRecomendado>  Recomendados         { get; set; } = new();
        public List<MundoMenuConfig>    MundosMenu           { get; set; } = new();
        public List<FiltroMandoConfig>  FiltrosCentroMando   { get; set; } = new();
        public List<NodoDiagramaConfig> DiagramaNodos        { get; set; } = new();
        public List<ModuloConfig>       Modulos              { get; set; } = new();
        public List<TemaConfig>         Temas                { get; set; } = new();
        public List<NewsItem>           News                 { get; set; } = new();

        /// <summary>Versión más reciente disponible de la app (ej. "1.2.0").</summary>
        public string AppVersion     { get; set; } = string.Empty;

        /// <summary>URL del ZIP con la nueva versión de la app.</summary>
        public string AppUpdateUrl   { get; set; } = string.Empty;

        /// <summary>Notas/changelog de la nueva versión.</summary>
        public string AppUpdateNotes { get; set; } = string.Empty;
    }
}
