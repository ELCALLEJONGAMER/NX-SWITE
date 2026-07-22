using System.Collections.Generic;

namespace NX_Swite.Models
{
    /// <summary>
    /// Conjunto de archivos cr�ticos cuya presencia (y SHA256 opcional) sirve para
    /// detectar si una versi�n concreta del m�dulo est� instalada en la SD.
    /// </summary>
    public class FirmaDeteccion
    {
        public string Version { get; set; } = string.Empty;
        public List<ArchivoCritico> Archivos { get; set; } = new();
    }

    /// <summary>
    /// Un �nico archivo de la firma de detecci�n. El SHA256 es opcional;
    /// cuando est� vac�o basta con que el archivo exista.
    /// </summary>
    public class ArchivoCritico
    {
        public string Ruta { get; set; } = string.Empty;
        public string SHA256 { get; set; } = string.Empty;
    }
}
