using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NX_Suite.Core.Pipeline.Pasos
{
    /// <summary>
    /// Copia el contenido de OrigenTemp (carpeta o archivo) a una ruta de la SD.
    /// Resuelve el origen probando primero como carpeta extraída, luego como
    /// archivo en Extracción y por último como archivo en la caché de ZIPs.
    ///
    /// Parámetros JSON:
    ///   OrigenTemp : nombre de carpeta o archivo (relativo a las cachés)
    ///   DestinoSD  : ruta destino dentro de la SD (ej. "/atmosphere/")
    /// </summary>
    public class PasoCopiarSD : IPasoPipeline
    {
        public string TipoAccion => "COPIARSD";

        public Task EjecutarAsync(ContextoPipeline ctx, JsonElement parametros, CancellationToken ct)
        {
            string origenTemp   = parametros.GetProperty("OrigenTemp").GetString()!;
            string destinoSDJson = parametros.GetProperty("DestinoSD").GetString()!;
            string rutaDestinoSD = PipelineFsHelpers.RutaSDAbsoluta(ctx.LetraSD, destinoSDJson);

            string rutaOrigenExtraccion = Path.Combine(ctx.RutaCacheExtraccion, origenTemp);

            if (Directory.Exists(rutaOrigenExtraccion))
            {
                PipelineFsHelpers.CopiarDirectorio(rutaOrigenExtraccion, rutaDestinoSD);
            }
            else if (File.Exists(rutaOrigenExtraccion))
            {
                if (!Directory.Exists(rutaDestinoSD)) Directory.CreateDirectory(rutaDestinoSD);
                File.Copy(rutaOrigenExtraccion, Path.Combine(rutaDestinoSD, Path.GetFileName(origenTemp)), true);
            }
            else
            {
                string rutaOrigenZips = Path.Combine(ctx.RutaCacheZips, origenTemp);
                if (File.Exists(rutaOrigenZips))
                {
                    if (!Directory.Exists(rutaDestinoSD)) Directory.CreateDirectory(rutaDestinoSD);
                    File.Copy(rutaOrigenZips, Path.Combine(rutaDestinoSD, Path.GetFileName(origenTemp)), true);
                }
            }

            return Task.CompletedTask;
        }
    }
}
