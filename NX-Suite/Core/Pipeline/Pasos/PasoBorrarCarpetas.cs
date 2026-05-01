using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NX_Suite.Core.Pipeline.Pasos
{
    /// <summary>
    /// Borra una lista de carpetas de la SD recursivamente. Las rutas que no
    /// existen se ignoran.
    ///
    /// Parámetros JSON:
    ///   CarpetasSD : ["/atmosphere/contents", "/switch/algo"]
    /// </summary>
    public class PasoBorrarCarpetas : IPasoPipeline
    {
        public string TipoAccion => "BORRARCARPETAS";

        public Task EjecutarAsync(ContextoPipeline ctx, JsonElement parametros, CancellationToken ct)
        {
            foreach (var carpeta in parametros.GetProperty("CarpetasSD").EnumerateArray())
            {
                string ruta = PipelineFsHelpers.RutaSDAbsoluta(ctx.LetraSD, carpeta.GetString()!);
                if (Directory.Exists(ruta)) Directory.Delete(ruta, true);
            }
            return Task.CompletedTask;
        }
    }
}
