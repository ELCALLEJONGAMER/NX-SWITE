using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NX_Suite.Core.Pipeline.Pasos
{
    /// <summary>
    /// Copia un archivo o carpeta desde la SD a la carpeta local de respaldos.
    ///
    /// Parámetros JSON:
    ///   OrigenSD       : ruta dentro de la SD
    ///   NombreRespaldo : nombre del archivo/carpeta destino dentro de Backups
    /// </summary>
    public class PasoRespaldarAPc : IPasoPipeline
    {
        public string TipoAccion => "RESPALDARAPC";

        public Task EjecutarAsync(ContextoPipeline ctx, JsonElement parametros, CancellationToken ct)
        {
            string origenSD   = parametros.GetProperty("OrigenSD").GetString()!;
            string nombreBack = parametros.GetProperty("NombreRespaldo").GetString()!;

            string fullOrigen  = PipelineFsHelpers.RutaSDAbsoluta(ctx.LetraSD, origenSD);
            string fullDestino = Path.Combine(ctx.RutaBackups, nombreBack);

            if (File.Exists(fullOrigen))           File.Copy(fullOrigen, fullDestino, true);
            else if (Directory.Exists(fullOrigen)) PipelineFsHelpers.CopiarDirectorio(fullOrigen, fullDestino);

            return Task.CompletedTask;
        }
    }
}
