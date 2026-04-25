using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NX_Suite.Core.Pipeline.Pasos
{
    /// <summary>
    /// Restaura un archivo o carpeta desde la carpeta local de respaldos a la SD.
    ///
    /// Parámetros JSON:
    ///   NombreRespaldo : nombre del archivo/carpeta dentro de Backups
    ///   DestinoSD      : ruta destino dentro de la SD
    /// </summary>
    public class PasoRestaurarDePc : IPasoPipeline
    {
        public string TipoAccion => "RESTAURARDEPC";

        public Task EjecutarAsync(ContextoPipeline ctx, JsonElement parametros, CancellationToken ct)
        {
            string nombreRes = parametros.GetProperty("NombreRespaldo").GetString()!;
            string destinoSD = parametros.GetProperty("DestinoSD").GetString()!;

            string resOrigen  = Path.Combine(ctx.RutaBackups, nombreRes);
            string resDestino = PipelineFsHelpers.RutaSDAbsoluta(ctx.LetraSD, destinoSD);

            if (File.Exists(resOrigen))           File.Copy(resOrigen, resDestino, true);
            else if (Directory.Exists(resOrigen)) PipelineFsHelpers.CopiarDirectorio(resOrigen, resDestino);

            return Task.CompletedTask;
        }
    }
}
