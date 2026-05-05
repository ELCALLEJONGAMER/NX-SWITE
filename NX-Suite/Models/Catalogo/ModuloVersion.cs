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

        /// <summary>
        /// Versión mínima de cada dependencia requerida para que esta versión del módulo
        /// sea la recomendada. Clave = Id del módulo dependencia, Valor = versión mínima.
        /// Ejemplo: { "hekate": "2.0.0" } ? esta config solo aplica si hekate >= 2.0.0.
        /// Si está vacío, la versión es compatible con cualquier entorno.
        /// </summary>
        public Dictionary<string, string> VersionDependencia { get; set; } = new();

        /// <summary>
        /// Restriccion de version de Atmosphere para esta version del modulo.
        /// Soporta operadores: &lt;=, &gt;=, &lt;, &gt;. Sin operador se trata como &gt;=.
        /// Ejemplo: "&lt;=1.10.0" significa que esta version solo funciona con Atmosphere &lt;= 1.10.0.
        /// Se comprueba contra los IDs "atmosphere" y "atmosphere_mod".
        /// </summary>
        public string Atmos { get; set; } = string.Empty;

        /// <summary>
        /// Reglas de validación de contenido para esta versión del módulo.
        /// Solo aplica a módulos con etiqueta "configuracion".
        /// Al actualizar el pipeline de instalación, actualizar también estas reglas.
        /// </summary>
        public ReglasConfig? ReglasConfig { get; set; }

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
