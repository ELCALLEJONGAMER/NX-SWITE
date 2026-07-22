using NX_Swite.Models;

namespace NX_Swite.Models.Cache
{
    /// <summary>
    /// ViewModel de un m�dulo con cach� descargada, usado en la lista del
    /// panel de Cach� dentro de los Ajustes de usuario.
    /// </summary>
    public class ItemCacheModuloVM
    {
        /// <summary>Nombre visible del m�dulo (ej. "SaltyNX").</summary>
        public string Nombre { get; init; } = string.Empty;

        /// <summary>Texto de detalle con versiones y tama�o (ej. "v1.7.5 � ZIP 220 KB").</summary>
        public string Detalle { get; init; } = string.Empty;

        /// <summary>Referencia al ModuloConfig para poder llamar a LimpiarCacheModulo.</summary>
        public ModuloConfig Modulo { get; init; } = null!;
    }
}
