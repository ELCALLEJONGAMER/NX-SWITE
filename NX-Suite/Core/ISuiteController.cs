using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NX_Suite.Hardware;
using NX_Suite.Models;

namespace NX_Suite.Core
{
    /// <summary>
    /// Interfaz que expone las operaciones que la UI necesita del "Cerebro".
    /// Mantén aquí solo métodos sin lógica UI.
    /// </summary>
    public interface ISuiteController
    {
        Task<GistData> SincronizarTodoAsync(string urlGist, string letraSD);
        // Sobre carga para soportar cancelación desde la UI (preparación, no obliga a cambiar implementación existente)
        Task<GistData> SincronizarTodoAsync(string urlGist, string letraSD, CancellationToken cancellationToken);
        InfoPanelDerecho ObtenerInfoPanel(SDInfo unidad, List<ModuloConfig> modulos);
        Task<(bool Exito, string MensajeError)> InstalarModuloAsync(ModuloConfig modulo, string letraSD, IProgress<EstadoProgreso> progreso);
        Task<bool> DesinstalarModuloAsync(ModuloConfig modulo, string letraSD);
    }
}