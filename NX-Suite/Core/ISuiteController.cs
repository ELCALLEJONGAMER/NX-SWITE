using NX_Suite.Hardware;
using NX_Suite.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NX_Suite.Core
{
    public interface ISuiteController
    {
        Task<GistData?> SincronizarTodoAsync(string urlGist, string letraSD);
        Task<GistData?> SincronizarTodoAsync(string urlGist, string letraSD, CancellationToken cancellationToken);
        Task<List<SDInfo>> ObtenerUnidadesRemoviblesAsync();
        InfoPanelDerecho ObtenerInfoPanel(SDInfo unidad, List<ModuloConfig> modulos);
        Task<(bool Exito, string MensajeError)> InstalarModuloAsync(ModuloConfig modulo, string letraSD, IProgress<EstadoProgreso> progreso);
        Task<(bool Exito, string MensajeError)> InstalarModuloAsync(ModuloConfig modulo, string letraSD, IProgress<EstadoProgreso> progreso, CancellationToken ct);
        Task<bool> DesinstalarModuloAsync(ModuloConfig modulo, string letraSD);
        void LimpiarCacheModulo(ModuloConfig modulo);
        void ActualizarEstadoCacheCatalogo(IEnumerable<ModuloConfig> catalogo);

        /// <summary>
        /// Filtra módulos por una única etiqueta (usado por el panel de categorías).
        /// </summary>
        IEnumerable<ModuloConfig> FiltrarPorEtiqueta(IEnumerable<ModuloConfig> modulos, string etiqueta);
    }
}