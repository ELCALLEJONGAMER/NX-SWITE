using NX_Suite.Models;

namespace NX_Suite.Models.Cache
{
    /// <summary>
    /// ViewModel de un módulo con caché descargada, usado en la lista del
    /// panel de Caché dentro de los Ajustes de usuario.
    /// </summary>
    public class ItemCacheModuloVM
    {
        /// <summary>Nombre visible del módulo (ej. "SaltyNX").</summary>
        public string Nombre { get; init; } = string.Empty;

        /// <summary>Texto de detalle con versiones y tamaño (ej. "v1.7.5 · ZIP 220 KB").</summary>
        public string Detalle { get; init; } = string.Empty;

        /// <summary>Referencia al ModuloConfig para poder llamar a LimpiarCacheModulo.</summary>
        public ModuloConfig Modulo { get; init; } = null!;
    }
}
