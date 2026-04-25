using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NX_Suite.Core.Pipeline.Pasos
{
    /// <summary>
    /// Mueve un archivo dentro de la SD. Crea el directorio destino si no existe
    /// y sobrescribe el destino si ya existía.
    ///
    /// Parámetros JSON:
    ///   Origen             : ruta de origen en la SD
    ///   Destino            : ruta de destino en la SD
    ///   IgnorarSiNoExiste  : true | false (opcional, por defecto false ? lanza excepción)
    /// </summary>
    public class PasoMoverArchivo : IPasoPipeline
    {
        public string TipoAccion => "MOVERARCHIVO";

        public Task EjecutarAsync(ContextoPipeline ctx, JsonElement parametros, CancellationToken ct)
        {
            string origen          = parametros.GetProperty("Origen").GetString()!;
            string destino         = parametros.GetProperty("Destino").GetString()!;
            bool   ignorarSiFalta  = parametros.TryGetProperty("IgnorarSiNoExiste", out var ignProp) && ignProp.GetBoolean();

            string rutaOrigen  = PipelineFsHelpers.RutaSDAbsoluta(ctx.LetraSD, origen);
            string rutaDestino = PipelineFsHelpers.RutaSDAbsoluta(ctx.LetraSD, destino);

            if (File.Exists(rutaOrigen))
            {
                string? dirDestino = Path.GetDirectoryName(rutaDestino);
                if (!string.IsNullOrEmpty(dirDestino) && !Directory.Exists(dirDestino))
                    Directory.CreateDirectory(dirDestino);

                if (File.Exists(rutaDestino)) File.Delete(rutaDestino);
                File.Move(rutaOrigen, rutaDestino);
            }
            else if (!ignorarSiFalta)
            {
                throw new InvalidOperationException($"No existe el archivo a mover: {rutaOrigen}");
            }

            return Task.CompletedTask;
        }
    }
}
