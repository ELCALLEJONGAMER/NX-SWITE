using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NX_Suite.Core.Pipeline.Pasos
{
    /// <summary>
    /// Ejecuta un comando del sistema operativo. La app ya corre como
    /// administrador, así que cualquier proceso lanzado hereda esos permisos.
    ///
    /// Parámetros JSON:
    ///   Comando    : ruta del ejecutable
    ///   Argumentos : argumentos (opcional)
    ///   Oculto     : true | false (opcional) ? CreateNoWindow
    /// </summary>
    public class PasoEjecutarCmd : IPasoPipeline
    {
        public string TipoAccion => "EJECUTARCMD";

        public Task EjecutarAsync(ContextoPipeline ctx, JsonElement parametros, CancellationToken ct)
        {
            string comando    = parametros.GetProperty("Comando").GetString()!;
            string argumentos = parametros.TryGetProperty("Argumentos", out var argProp) ? argProp.GetString() ?? "" : "";
            bool   oculto     = parametros.TryGetProperty("Oculto", out var ocProp) && ocProp.GetBoolean();

            var startInfo = new ProcessStartInfo
            {
                FileName        = comando,
                Arguments       = argumentos,
                UseShellExecute = false,
                CreateNoWindow  = oculto
            };

            using var proceso = Process.Start(startInfo);
            proceso?.WaitForExit();

            return Task.CompletedTask;
        }
    }
}
