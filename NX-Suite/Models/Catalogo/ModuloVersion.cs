using System.Collections.Generic;

namespace NX_Swite.Models
{
    /// <summary>
    /// Una versi�n instalable de un m�dulo. Contiene los pipelines de instalaci�n
    /// y desinstalaci�n espec�ficos de esa versi�n.
    /// </summary>
    public class ModuloVersion
    {
        public string Version { get; set; } = string.Empty;

        /// <summary>Firmware m�nimo requerido para esta versi�n. Ej: "22.1.0"</summary>
        public string Firmware { get; set; } = string.Empty;

        /// <summary>
        /// Si es true, esta versi�n solo se usa para detectar si est� instalada
        /// y no est� disponible para descargar (ej: versiones antiguas retiradas por seguridad).
        /// </summary>
        public bool SoloDeteccion { get; set; } = false;

        public List<PasoPipeline> PipelineInstalacion { get; set; } = new();
        public List<PasoPipeline> PipelineDesinstalacion { get; set; } = new();

        /// <summary>
        /// Versi�n m�nima de cada dependencia requerida para que esta versi�n del m�dulo
        /// sea la recomendada. Clave = Id del m�dulo dependencia, Valor = versi�n m�nima.
        /// Ejemplo: { "hekate": "2.0.0" } ? esta config solo aplica si hekate >= 2.0.0.
        /// Si est� vac�o, la versi�n es compatible con cualquier entorno.
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
        /// Reglas de validaci�n para un �nico archivo (formato ini/txt/hosts con reglas individuales).
        /// Compatibilidad con builds anteriores � los builds nuevos prefieren ReglasConfigArchivos.
        /// </summary>
        public ReglasConfig? ReglasConfig { get; set; }

        /// <summary>
        /// Lista de validaciones de contenido, una entrada por archivo a validar.
        /// Soporta formato "exacto" (ContenidoEsperado) para txt/hosts sin reglas individuales.
        /// Si est� presente, tiene prioridad sobre ReglasConfig.
        /// </summary>
        public List<ReglasConfig> ReglasConfigArchivos { get; set; } = new();

        // ?? Estado de cach� por versi�n (calculado en tiempo de ejecuci�n por GestorCache) ??

        /// <summary>Ruta absoluta al ZIP de esta versi�n en la b�veda de cach�.</summary>
        public string RutaCacheZipVer { get; set; } = string.Empty;

        /// <summary>Ruta absoluta a la carpeta extra�da de esta versi�n en la b�veda de cach�.</summary>
        public string RutaCacheCarpetaVer { get; set; } = string.Empty;

        /// <summary>True si existe el ZIP de esta versi�n en la cach� local.</summary>
        public bool TieneZipCache { get; set; }

        /// <summary>True si existe la carpeta extra�da (o archivo directo) de esta versi�n en la cach� local.</summary>
        public bool TieneCarpetaCache { get; set; }
    }
}
