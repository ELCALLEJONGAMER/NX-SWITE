using NX_Suite.Models;
using System.Collections.Generic;

namespace NX_Suite.Core.Configuracion
{
    /// <summary>
    /// Single source of truth para todo lo que llega del JSON remoto (Gist):
    /// configuración de UI (URLs de iconos, colores), paleta NYX y catálogo
    /// de módulos recomendados. Se rellena una sola vez tras la sincronización
    /// y cualquier consumidor de la app lee desde aquí.
    ///
    /// Sustituye al antiguo <c>UIConfigService</c>.
    /// </summary>
    public static class ConfiguracionRemota
    {
        /// <summary>Configuración de UI (iconos, colores, URL de fat32format, etc.).</summary>
        public static ConfiguracionUI Ui { get; set; } = new();

        /// <summary>Paleta NYX completa (colores y fondos) declarada en el JSON.</summary>
        public static NyxConfigColors NyxColors { get; set; } = new();

        /// <summary>
        /// Módulos recomendados cargados desde el Gist, ya ordenados por
        /// <see cref="ModuloRecomendado.Orden"/>.
        /// </summary>
        public static List<ModuloRecomendado> Recomendados { get; set; } = new();
    }
}
