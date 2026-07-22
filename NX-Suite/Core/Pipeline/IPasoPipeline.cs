using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NX_Swite.Core.Pipeline
{
    /// <summary>
    /// Contrato de un paso del pipeline declarativo del JSON.
    /// Cada implementaci�n corresponde 1:1 con un valor de <c>TipoAccion</c>
    /// del JSON (DESCARGAR, EXTRAER, CREARINI, FORMATEARSD, etc.).
    ///
    /// Para a�adir un paso nuevo:
    ///   1. Crear una clase en <c>Core/Pipeline/Pasos/</c> que implemente esta interfaz.
    ///   2. Registrarla en <see cref="RegistroPasos"/>.
    /// El orquestador (<c>ReglasLogic.EjecutarPipelineAsync</c>) NO necesita cambios.
    /// </summary>
    public interface IPasoPipeline
    {
        /// <summary>Identificador del tipo de acci�n tal como aparece en el JSON. Case-insensitive.</summary>
        string TipoAccion { get; }

        /// <summary>
        /// Ejecuta la acci�n usando el estado compartido (<paramref name="ctx"/>) y los
        /// par�metros JSON espec�ficos del paso (<paramref name="parametros"/>).
        /// </summary>
        Task EjecutarAsync(ContextoPipeline ctx, JsonElement parametros, CancellationToken ct);
    }
}
