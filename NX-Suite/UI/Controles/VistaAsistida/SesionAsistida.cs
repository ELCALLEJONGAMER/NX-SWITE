using NX_Swite.Models;
using NX_Swite.Models;
using System;
using System.Collections.Generic;

namespace NX_Swite.UI.Controles
{
    /// <summary>
    /// Sesi�n de instalaci�n asistida (modo libre): conjunto de m�dulos que el
    /// usuario ha seleccionado para instalar uno tras otro.
    /// <para>
    /// Los m�dulos cuyos IDs est�n en <see cref="IdsDependencias"/> son
    /// dependencias resueltas autom�ticamente; se instalan antes que el m�dulo
    /// que las necesita y se muestran con etiqueta diferenciada en la pantalla
    /// de carga para que el usuario sepa qu� est� pasando.
    /// </para>
    /// </summary>
    public class SesionAsistida
    {
        public List<ModuloConfig> Modulos { get; init; } = new();

        /// <summary>IDs de m�dulos que son dependencias autom�ticas (no elegidos por el usuario).</summary>
        public HashSet<string> IdsDependencias { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }
}

