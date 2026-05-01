using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NX_Suite.Core.Pipeline
{
    /// <summary>
    /// Contrato de un paso del pipeline declarativo del JSON.
    /// Cada implementación corresponde 1:1 con un valor de <c>TipoAccion</c>
    /// del JSON (DESCARGAR, EXTRAER, CREARINI, FORMATEARSD, etc.).
    ///
    /// Para añadir un paso nuevo:
    ///   1. Crear una clase en <c>Core/Pipeline/Pasos/</c> que implemente esta interfaz.
    ///   2. Registrarla en <see cref="RegistroPasos"/>.
    /// El orquestador (<c>ReglasLogic.EjecutarPipelineAsync</c>) NO necesita cambios.
    /// </summary>
    public interface IPasoPipeline
    {
        /// <summary>Identificador del tipo de acción tal como aparece en el JSON. Case-insensitive.</summary>
        string TipoAccion { get; }

        /// <summary>
        /// Ejecuta la acción usando el estado compartido (<paramref name="ctx"/>) y los
        /// parámetros JSON específicos del paso (<paramref name="parametros"/>).
        /// </summary>
        Task EjecutarAsync(ContextoPipeline ctx, JsonElement parametros, CancellationToken ct);
    }
}
