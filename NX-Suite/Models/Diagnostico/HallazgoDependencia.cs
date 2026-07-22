using System.Collections.Generic;

namespace NX_Swite.Models
{
    /// <summary>
    /// Agrupa un m�dulo instalado con la lista de sus dependencias no satisfechas.
    /// Generado por el panel de Diagn�stico R�pido SD al cruzar m�dulos instalados
    /// con el resultado de AnalizadorDependencias.
    /// </summary>
    public class HallazgoDependencia
    {
        /// <summary>M�dulo instalado cuyas dependencias no se cumplen.</summary>
        public ModuloConfig Modulo { get; init; } = null!;

        /// <summary>Dependencias problem�ticas (NoInstalada | Parcial | Desactualizada).</summary>
        public List<ResultadoDependencia> DependenciasPendientes { get; init; } = new();
    }
}
