using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NX_Swite.Core.Pipeline.Pasos
{
    /// <summary>
    /// Borra carpetas de la SD <b>�nicamente si est�n vac�as</b> despu�s de una
    /// desinstalaci�n. Es seguro para carpetas compartidas entre m�dulos:
    /// si otro m�dulo dej� archivos dentro, la carpeta no se toca.
    ///
    /// Par�metros JSON:
    ///   CarpetasSD : ["/atmosphere/22.1.0", "/atmosphere/22.0.0"]
    ///
    /// �salo al final del PipelineDesinstalacion de cada versi�n para limpiar
    /// las carpetas espec�ficas de esa versi�n sin riesgo de borrar datos ajenos.
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

                // Solo borra si la carpeta est� completamente vac�a (sin archivos ni subcarpetas)
                if (Directory.GetFileSystemEntries(ruta).Length == 0)
                    Directory.Delete(ruta, false);
            }
            return Task.CompletedTask;
        }
    }
}
