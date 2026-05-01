using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NX_Suite.Core.Pipeline.Pasos
{
    /// <summary>
    /// Crea un archivo de texto plano en la SD. Útil para .txt, .json, .cfg.
    ///
    /// Parámetros JSON:
    ///   RutaSD                 : ruta del archivo en la SD
    ///   Contenido              : texto a escribir (los "\n" del JSON se convierten en saltos de línea reales)
    ///   SobreescribirSiExiste  : true | false (opcional, por defecto true)
    /// </summary>
    public class PasoCrearTxt : IPasoPipeline
    {
        public string TipoAccion => "CREARTXT";

        public async Task EjecutarAsync(ContextoPipeline ctx, JsonElement parametros, CancellationToken ct)
        {
            string ruta      = parametros.GetProperty("RutaSD").GetString()!;
            string contenido = parametros.GetProperty("Contenido").GetString()!;
            bool sobreescribir = !parametros.TryGetProperty("SobreescribirSiExiste", out var sobProp)
                                 || sobProp.GetBoolean();
            string path = PipelineFsHelpers.RutaSDAbsoluta(ctx.LetraSD, ruta);

            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (sobreescribir || !File.Exists(path))
                await File.WriteAllTextAsync(path, contenido.Replace("\\n", "\n"), Encoding.UTF8, ct);
        }
    }
}
