using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NX_Suite.Core.Pipeline.Pasos
{
    /// <summary>
    /// Borra carpetas de la SD <b>únicamente si están vacías</b> después de una
    /// desinstalación. Es seguro para carpetas compartidas entre módulos:
    /// si otro módulo dejó archivos dentro, la carpeta no se toca.
    ///
    /// Parámetros JSON:
    ///   CarpetasSD : ["/atmosphere/22.1.0", "/atmosphere/22.0.0"]
    ///
    /// Úsalo al final del PipelineDesinstalacion de cada versión para limpiar
    /// las carpetas específicas de esa versión sin riesgo de borrar datos ajenos.
    /// </summary>
    public class PasoBorrarCarpetasVacias : IPasoPipeline
    {
        public string TipoAccion => "BORRARCARPETASVACIAS";

        public Task EjecutarAsync(ContextoPipeline ctx, JsonElement parametros, CancellationToken ct)
        {
            foreach (var item in parametros.GetProperty("CarpetasSD").EnumerateArray())
            {
                string ruta = PipelineFsHelpers.RutaSDAbsoluta(ctx.LetraSD, item.GetString()!);

                if (!Directory.Exists(ruta)) continue;

                // Solo borra si la carpeta está completamente vacía (sin archivos ni subcarpetas)
                if (Directory.GetFileSystemEntries(ruta).Length == 0)
                    Directory.Delete(ruta, false);
            }
            return Task.CompletedTask;
        }
    }
}
