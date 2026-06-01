using NX_Suite.Services;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NX_Suite.Core.Pipeline.Pasos
{
    /// <summary>
    /// Asigna un valor a una clave de un .ini de Hekate. No crea el archivo
    /// si no existe (a diferencia de EDITARINI).
    ///
    /// Parámetros JSON:
    ///   ArchivoIni : ruta del .ini en la SD
    ///   Seccion    : nombre de la sección
    ///   Clave      : clave a modificar
    ///   Valor      : valor a escribir
    /// </summary>
    public class PasoHekateSetValue : IPasoPipeline
    {
        public string TipoAccion => "HEKATE_SET_VALUE";

        public async Task EjecutarAsync(ContextoPipeline ctx, JsonElement parametros, CancellationToken ct)
        {
            string iniRel  = parametros.GetProperty("ArchivoIni").GetString()!;
            string seccion = parametros.GetProperty("Seccion").GetString()!;
            string clave   = parametros.GetProperty("Clave").GetString()!;
            string valor   = parametros.GetProperty("Valor").GetString()!;
            string path    = PipelineFsHelpers.RutaSDAbsoluta(ctx.LetraSD, iniRel);

            if (!File.Exists(path))
            {
                Logger.HekateArchivoNoEncontrado(iniRel);
                return;
            }

            var ini = new HekateIniManager(path);
            await ini.LoadAsync();
            ini.SetValue(seccion, clave, valor);
            await ini.SaveAsync();
            Logger.HekateValorEstablecido(iniRel, seccion, clave, valor);
        }
    }
}
