using System.Text.Json;

namespace NX_Suite.Models
{
    /// <summary>
    /// Representa un paso individual del pipeline declarativo definido en el JSON
    /// (descarga, extracción, copia, formato, etc.).
    /// El handler concreto se resuelve por <see cref="TipoAccion"/> en ReglasLogic.
    /// </summary>
    public class PasoPipeline
    {
        public int Paso { get; set; }
        public string TipoAccion { get; set; } = string.Empty;
        public string MensajeUI { get; set; } = string.Empty;
        public JsonElement Parametros { get; set; }
    }
}
