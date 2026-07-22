using NX_Swite.Models;
using System.Collections.Generic;

namespace NX_Swite.Core.Configuracion
{
    /// <summary>
    /// Single source of truth para todo lo que llega del JSON remoto (Gist):
    /// configuraci�n de UI (URLs de iconos, colores), paleta NYX y cat�logo
    /// de m�dulos recomendados. Se rellena una sola vez tras la sincronizaci�n
    /// y cualquier consumidor de la app lee desde aqu�.
    ///
    /// Sustituye al antiguo <c>UIConfigService</c>.
    /// </summary>
    public static class ConfiguracionRemota
    {
        /// <summary>Configuraci�n de UI (iconos, colores, URL de fat32format, etc.).</summary>
        public static ConfiguracionUI Ui { get; set; } = new();

        /// <summary>Paleta NYX completa (colores y fondos) declarada en el JSON.</summary>
        public static NyxConfigColors NyxColors { get; set; } = new();

        /// <summary>
        /// M�dulos recomendados cargados desde el Gist, ya ordenados por
        /// <see cref="ModuloRecomendado.Orden"/>.
        /// </summary>
        public static List<ModuloRecomendado> Recomendados { get; set; } = new();
    }
}
