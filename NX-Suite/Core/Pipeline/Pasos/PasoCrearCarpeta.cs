using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NX_Suite.Core.Pipeline.Pasos
{
    /// <summary>
    /// Crea una carpeta en la SD (idempotente: si ya existe no hace nada).
    ///
    /// Parámetros JSON:
    ///   CarpetaSD : "/switch/mi_app"
    /// </summary>
    public class PasoCrearCarpeta : IPasoPipeline
    {
        public string TipoAccion => "CREARCARPETA";

        public Task EjecutarAsync(ContextoPipeline ctx, JsonElement parametros, CancellationToken ct)
        {
            string carpeta = parametros.GetProperty("CarpetaSD").GetString()!;
            string ruta    = PipelineFsHelpers.RutaSDAbsoluta(ctx.LetraSD, carpeta);
            if (!Directory.Exists(ruta)) Directory.CreateDirectory(ruta);
            return Task.CompletedTask;
        }
    }
}
