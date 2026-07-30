namespace NX_Swite.Models
{
    public class MundoMenuConfig
    {
        public string Id { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Subtitulo { get; set; } = string.Empty;
        public string IconoUrl { get; set; } = string.Empty;
        public string ColorNeon { get; set; } = "#00D2FF";

        /// <summary>
        /// Tipo de mundo. Valores: "catalogo" | "diagrama" | "asistido" | "personalizacion" | "cfw_hub"
        /// </summary>
        public string Tipo { get; set; } = "catalogo";

        /// <summary>
        /// Solo aplica cuando Tipo == "asistido".
        /// Valores: "libre" | "forzado"
        /// </summary>
        public string ModoAsistente { get; set; } = "libre";

        /// <summary>
        /// Etiquetas base que definen qu� m�dulos muestra este mundo.
        /// Si est� vac�o se muestran todos los m�dulos.
        /// </summary>
        public List<string> EtiquetasFiltro { get; set; } = new();

        /// <summary>
        /// Solo aplica cuando Tipo == "cfw_hub". IDs de los mundos "hijos" a los
        /// que las tarjetas del hub pueden navegar (ej. ["asistido", "catalogo",
        /// "personalizacion"]). No se usa para renderizar directamente — las
        /// tarjetas del hub son fijas por ahora (ver VistaHubCFW); esta lista
        /// permite validar/documentar la relacion hub-hijos desde el Gist.
        /// </summary>
        public List<string> SubMundosIds { get; set; } = new();
    }
}