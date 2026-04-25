using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NX_Suite.Core.Pipeline.Pasos
{
    /// <summary>
    /// Extrae un archivo comprimido de la caché de ZIPs a la carpeta de
    /// extracción. Si la carpeta destino ya tiene archivos, asume que ya se
    /// extrajo y omite la operación.
    ///
    /// Parámetros JSON:
    ///   ArchivoZip         : nombre del .zip dentro de RutaCacheZips
    ///   CarpetaDestinoTemp : subcarpeta destino dentro de RutaCacheExtraccion
    /// </summary>
    public class PasoExtraer : IPasoPipeline
    {
        public string TipoAccion => "EXTRAER";

        public async Task EjecutarAsync(ContextoPipeline ctx, JsonElement parametros, CancellationToken ct)
        {
            string archivoZip  = parametros.GetProperty("ArchivoZip").GetString()!;
            string carpetaTemp = parametros.GetProperty("CarpetaDestinoTemp").GetString()!;

            string rutaZip     = Path.Combine(ctx.RutaCacheZips, archivoZip);
            string rutaDestino = Path.Combine(ctx.RutaCacheExtraccion, carpetaTemp);

            if (!Directory.Exists(rutaDestino) ||
                Directory.GetFiles(rutaDestino, "*.*", SearchOption.AllDirectories).Length == 0)
            {
                await ctx.MotorZip.ExtraerTodoAsync(rutaZip, rutaDestino, ctx.Progreso);
            }
        }
    }
}
