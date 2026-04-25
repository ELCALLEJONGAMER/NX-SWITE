using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NX_Suite.Core.Pipeline.Pasos
{
    /// <summary>
    /// Crea o edita una clave dentro de una sección de cualquier .ini en la SD.
    /// Crea el archivo, el directorio y/o la sección si no existen.
    ///
    /// Parámetros JSON:
    ///   RutaSD  : ruta del .ini en la SD
    ///   Seccion : nombre de la sección
    ///   Clave   : clave a editar
    ///   Valor   : valor a escribir
    ///   Modo    : "SOBREESCRIBIR" | "SOLO_SI_VACIO" | "SOLO_SI_NO_EXISTE_CLAVE"
    ///             (opcional, por defecto SOBREESCRIBIR)
    /// </summary>
    public class PasoEditarIni : IPasoPipeline
    {
        public string TipoAccion => "EDITARINI";

        public async Task EjecutarAsync(ContextoPipeline ctx, JsonElement parametros, CancellationToken ct)
        {
            string ruta    = parametros.GetProperty("RutaSD").GetString()!;
            string seccion = parametros.GetProperty("Seccion").GetString()!;
            string clave   = parametros.GetProperty("Clave").GetString()!;
            string valor   = parametros.GetProperty("Valor").GetString()!;
            string modo    = parametros.TryGetProperty("Modo", out var modoProp)
                ? modoProp.GetString()!.ToUpperInvariant()
                : "SOBREESCRIBIR";
            string path    = PipelineFsHelpers.RutaSDAbsoluta(ctx.LetraSD, ruta);

            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var ini = new HekateIniManager(path);
            await ini.LoadAsync();

            bool escribir = modo switch
            {
                "SOLO_SI_VACIO"           => string.IsNullOrWhiteSpace(ini.GetValue(seccion, clave)),
                "SOLO_SI_NO_EXISTE_CLAVE" => ini.GetValue(seccion, clave) == null,
                _                         => true   // SOBREESCRIBIR
            };

            if (escribir)
            {
                ini.SetValue(seccion, clave, valor);
                await ini.SaveAsync();
            }
        }
    }
}
