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
        /// Tipo de mundo. Valores: "catalogo" | "diagrama" | "asistido" | "personalizacion"
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
    }
}