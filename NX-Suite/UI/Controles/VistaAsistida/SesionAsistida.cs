using NX_Suite.Models;
using System.Collections.Generic;

namespace NX_Suite.UI.Controles
{
    /// <summary>
    /// Sesión de instalación asistida (modo libre): conjunto de módulos que el
    /// usuario ha seleccionado para instalar uno tras otro.
    /// </summary>
    public class SesionAsistida
    {
        public List<ModuloConfig> Modulos { get; init; } = new();
    }
}
