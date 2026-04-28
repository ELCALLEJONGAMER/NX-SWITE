using NX_Suite.Models;
using NX_Suite.Models;
using System;
using System.Collections.Generic;

namespace NX_Suite.UI.Controles
{
    /// <summary>
    /// Sesión de instalación asistida (modo libre): conjunto de módulos que el
    /// usuario ha seleccionado para instalar uno tras otro.
    /// <para>
    /// Los módulos cuyos IDs están en <see cref="IdsDependencias"/> son
    /// dependencias resueltas automáticamente; se instalan antes que el módulo
    /// que las necesita y se muestran con etiqueta diferenciada en la pantalla
    /// de carga para que el usuario sepa qué está pasando.
    /// </para>
    /// </summary>
    public class SesionAsistida
    {
        public List<ModuloConfig> Modulos { get; init; } = new();

        /// <summary>IDs de módulos que son dependencias automáticas (no elegidos por el usuario).</summary>
        public HashSet<string> IdsDependencias { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }
}

