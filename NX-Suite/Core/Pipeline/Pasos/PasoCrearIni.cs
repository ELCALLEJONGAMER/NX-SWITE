using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NX_Suite.Core.Pipeline.Pasos
{
    /// <summary>
    /// Crea o sustituye un .ini completo desde una estructura declarativa.
    /// Genera el archivo visualmente ordenado con líneas en blanco entre secciones.
    ///
    /// Parámetros JSON:
    ///   RutaSD                : ruta del .ini en la SD
    ///   SobreescribirSiExiste : true | false (opcional, por defecto true)
    ///   Secciones             : array ordenado de:
    ///     {
    ///       "Nombre"    : "exosphere",
    ///       "Comentario": "; texto opcional",
    ///       "Claves"    : [ { "Clave": "k", "Valor": "v" }, ... ]
    ///     }
    /// </summary>
    public class PasoCrearIni : IPasoPipeline
    {
        public string TipoAccion => "CREARINI";

        public async Task EjecutarAsync(ContextoPipeline ctx, JsonElement parametros, CancellationToken ct)
        {
            string ruta = parametros.GetProperty("RutaSD").GetString()!;
            bool sobreescribir = !parametros.TryGetProperty("SobreescribirSiExiste", out var sobProp)
                                 || sobProp.GetBoolean();
            string path = PipelineFsHelpers.RutaSDAbsoluta(ctx.LetraSD, ruta);

            if (!sobreescribir && File.Exists(path)) return;

            var secciones = parametros.GetProperty("Secciones").EnumerateArray().ToList();
            var sb = new StringBuilder();
            bool primera = true;

            foreach (var seccion in secciones)
            {
                if (!primera) sb.AppendLine();
                primera = false;

                if (seccion.TryGetProperty("Comentario", out var comProp))
                {
                    string comentario = comProp.GetString()!;
                    if (!comentario.TrimStart().StartsWith(';') &&
                        !comentario.TrimStart().StartsWith('#'))
                        comentario = "; " + comentario;
                    sb.AppendLine(comentario);
                }

                string nombre = seccion.GetProperty("Nombre").GetString()!;
                sb.AppendLine($"[{nombre}]");

                foreach (var claveProp in seccion.GetProperty("Claves").EnumerateArray())
                {
                    string k = claveProp.GetProperty("Clave").GetString()!;
                    string v = claveProp.GetProperty("Valor").GetString()!;
                    sb.AppendLine($"{k}={v}");
                }
            }

            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            await File.WriteAllTextAsync(path, sb.ToString(), Encoding.UTF8, ct);
        }
    }
}
