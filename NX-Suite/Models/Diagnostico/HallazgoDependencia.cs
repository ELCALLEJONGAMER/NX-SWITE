using System.Collections.Generic;

namespace NX_Suite.Models
{
    /// <summary>
    /// Agrupa un módulo instalado con la lista de sus dependencias no satisfechas.
    /// Generado por el panel de Diagnóstico Rápido SD al cruzar módulos instalados
    /// con el resultado de AnalizadorDependencias.
    /// </summary>
    public class HallazgoDependencia
    {
        /// <summary>Módulo instalado cuyas dependencias no se cumplen.</summary>
        public ModuloConfig Modulo { get; init; } = null!;

        /// <summary>Dependencias problemáticas (NoInstalada | Parcial | Desactualizada).</summary>
        public List<ResultadoDependencia> DependenciasPendientes { get; init; } = new();
    }
}
