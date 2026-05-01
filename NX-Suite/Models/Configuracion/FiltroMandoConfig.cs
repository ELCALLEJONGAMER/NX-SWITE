using System.Collections.Generic;

namespace NX_Suite.Models
{
    /// <summary>
    /// Filtro del panel lateral (centro de mando) que agrupa módulos por etiqueta
    /// y/o por mundo activo.
    /// </summary>
    public class FiltroMandoConfig
    {
        public string Titulo { get; set; } = "Filtro";
        public string Nombre { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
        public string IconoUrl { get; set; } = string.Empty;
        public List<string> Mundos { get; set; } = new();
    }
}
