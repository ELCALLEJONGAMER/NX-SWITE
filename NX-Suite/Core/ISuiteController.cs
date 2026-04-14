using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NX_Suite.Hardware;
using NX_Suite.Models;

namespace NX_Suite.Core
{
    public interface ISuiteController
    {
        Task<GistData> SincronizarTodoAsync(string urlGist, string letraSD);
        Task<GistData> SincronizarTodoAsync(string urlGist, string letraSD, CancellationToken cancellationToken);
        Task<List<SDInfo>> ObtenerUnidadesRemoviblesAsync();
        InfoPanelDerecho ObtenerInfoPanel(SDInfo unidad, List<ModuloConfig> modulos);
        Task<(bool Exito, string MensajeError)> InstalarModuloAsync(ModuloConfig modulo, string letraSD, IProgress<EstadoProgreso> progreso);
        Task<bool> DesinstalarModuloAsync(ModuloConfig modulo, string letraSD);
    }
}