using System.Collections.Generic;

namespace NX_Suite.Models
{
    /// <summary>
    /// Una versión instalable de un módulo. Contiene los pipelines de instalación
    /// y desinstalación específicos de esa versión.
    /// </summary>
    public class ModuloVersion
    {
        public string Version { get; set; } = string.Empty;

        /// <summary>Firmware mínimo requerido para esta versión. Ej: "22.1.0"</summary>
        public string Firmware { get; set; } = string.Empty;

        /// <summary>
        /// Si es true, esta versión solo se usa para detectar si está instalada
        /// y no está disponible para descargar (ej: versiones antiguas retiradas por seguridad).
        /// </summary>
        public bool SoloDeteccion { get; set; } = false;

        public List<PasoPipeline> PipelineInstalacion { get; set; } = new();
        public List<PasoPipeline> PipelineDesinstalacion { get; set; } = new();
    }
}
