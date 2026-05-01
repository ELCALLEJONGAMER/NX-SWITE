using System.Collections.Generic;

namespace NX_Suite.Models
{
    /// <summary>
    /// Conjunto de archivos críticos cuya presencia (y SHA256 opcional) sirve para
    /// detectar si una versión concreta del módulo está instalada en la SD.
    /// </summary>
    public class FirmaDeteccion
    {
        public string Version { get; set; } = string.Empty;
        public List<ArchivoCritico> Archivos { get; set; } = new();
    }

    /// <summary>
    /// Un único archivo de la firma de detección. El SHA256 es opcional;
    /// cuando está vacío basta con que el archivo exista.
    /// </summary>
    public class ArchivoCritico
    {
        public string Ruta { get; set; } = string.Empty;
        public string SHA256 { get; set; } = string.Empty;
    }
}
