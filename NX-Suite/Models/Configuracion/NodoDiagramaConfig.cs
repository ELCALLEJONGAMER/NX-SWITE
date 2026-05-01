using System.Collections.Generic;

namespace NX_Suite.Models
{
    /// <summary>
    /// Define un slot visual en el modo asistido.
    /// Solo describe estructura de pantalla; la lógica de módulos vive en ModuloConfig.
    /// </summary>
    public class NodoDiagramaConfig
    {
        public string Id { get; set; } = string.Empty;

        /// <summary>Tipo de slot: "nucleo" | "complemento".</summary>
        public string Tipo { get; set; } = "nucleo";

        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string IconoUrl { get; set; } = string.Empty;
        public string ColorNeon { get; set; } = "#00D2FF";

        /// <summary>Si true, el usuario debe seleccionar un módulo en este slot.</summary>
        public bool EsObligatorio { get; set; }

        /// <summary>
        /// Etiquetas que filtran qué módulos aparecen al pulsar "+" en este slot.
        /// </summary>
        public List<string> EtiquetasFiltro { get; set; } = new();
    }
}
