using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NX_Suite.Core.Pipeline.Pasos
{
    /// <summary>
    /// Borra una lista de archivos de la SD. Las rutas que no existen se ignoran.
    ///
    /// Parámetros JSON:
    ///   RutasSD : ["/atmosphere/foo.bin", "/switch/bar.nro"]
    /// </summary>
    public class PasoBorrarArchivos : IPasoPipeline
    {
        public string TipoAccion => "BORRARARCHIVOS";

        public Task EjecutarAsync(ContextoPipeline ctx, JsonElement parametros, CancellationToken ct)
        {
            foreach (var ruta in parametros.GetProperty("RutasSD").EnumerateArray())
            {
                string archivo = PipelineFsHelpers.RutaSDAbsoluta(ctx.LetraSD, ruta.GetString()!);
                if (File.Exists(archivo)) File.Delete(archivo);
            }
            return Task.CompletedTask;
        }
    }
}
