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

        // ?? Estado de caché por versión (calculado en tiempo de ejecución por GestorCache) ??

        /// <summary>Ruta absoluta al ZIP de esta versión en la bóveda de caché.</summary>
        public string RutaCacheZipVer { get; set; } = string.Empty;

        /// <summary>Ruta absoluta a la carpeta extraída de esta versión en la bóveda de caché.</summary>
        public string RutaCacheCarpetaVer { get; set; } = string.Empty;

        /// <summary>True si existe el ZIP de esta versión en la caché local.</summary>
        public bool TieneZipCache { get; set; }

        /// <summary>True si existe la carpeta extraída (o archivo directo) de esta versión en la caché local.</summary>
        public bool TieneCarpetaCache { get; set; }
    }
}
