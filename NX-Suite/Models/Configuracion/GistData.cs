using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NX_Swite.Models
{
    /// <summary>
    /// Raíz del JSON remoto (Gist). Contiene toda la configuración descargable:
    /// branding, sonidos, mundos, módulos, recomendados, temas, etc.
    /// </summary>
    public class GistData : IJsonOnDeserialized
    {
        /// <summary>
        /// FiltrosCentroMando, DiagramaNodos y NyxConfigColors se
        /// toman siempre de <see cref="ConfiguracionFija"/> (no del catálogo).
        /// </summary>
        protected virtual bool AplicaConfiguracionFija => true;

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (AplicaConfiguracionFija) ConfiguracionFija.Aplicar(this);
        }

        public ConfiguracionUI          ConfiguracionUI      { get; set; } = new();
        public NyxConfigColors          NyxConfigColors      { get; set; } = new();
        public BrandingConfig           GlobalBranding       { get; set; } = new();
        public SonidosConfig            Sonidos              { get; set; } = new();
        public List<ModuloRecomendado>  Recomendados         { get; set; } = new();
        public List<FiltroMandoConfig>  FiltrosCentroMando   { get; set; } = new();
        public List<NodoDiagramaConfig> DiagramaNodos        { get; set; } = new();
        public List<ModuloConfig>       Modulos              { get; set; } = new();
        public List<TemaConfig>         Temas                { get; set; } = new();
        public List<NewsItem>           News                 { get; set; } = new();

        /// <summary>
        /// Sección raíz "tools" del Gist: configuración de herramientas
        /// externas administradas (descarga, caché y verificación por hash),
        /// como el CLI de NxNandManager.
        /// </summary>
        public ToolsConfig Tools { get; set; } = new();

        /// <summary>
        /// Sección raíz "titleDB" del Gist: URL del TitleDB remoto usado para
        /// resolver nombres de juegos a partir de un TitleId. Opcional: si no
        /// existe en el Gist, la resolución de nombres cae a un fallback
        /// ("Título &lt;TITLE_ID&gt;") sin afectar el resto de la app.
        /// </summary>
        public TitleDbConfig TitleDB { get; set; } = new();

        /// <summary>
        /// Sección raíz "tarjetasHubCfw" del Gist: imágenes de fondo por
        /// tarjeta fija del hub CFW. Declarado en el Gist como un objeto
        /// diccionario id -> url (ej. { "instalacion": "https://..." }),
        /// con IDs: "instalacion", "actualizacion", "catalogo",
        /// "personalizacion", "herramientas".
        /// </summary>
        public Dictionary<string, string> TarjetasHubCfw { get; set; } = new();

        /// <summary>Versión más reciente disponible de la app (ej. "1.2.0").</summary>
        public string AppVersion     { get; set; } = string.Empty;

        /// <summary>URL del ZIP con la nueva versión de la app.</summary>
        public string AppUpdateUrl   { get; set; } = string.Empty;

        /// <summary>Notas/changelog de la nueva versión.</summary>
        public string AppUpdateNotes { get; set; } = string.Empty;
    }
}
