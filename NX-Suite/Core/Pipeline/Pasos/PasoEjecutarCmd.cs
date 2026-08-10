using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NX_Swite.Services;

namespace NX_Swite.Core.Pipeline.Pasos
{
    /// <summary>
    /// Ejecuta un comando del sistema operativo. La app ya corre como
    /// administrador, as� que cualquier proceso lanzado hereda esos permisos.
    ///
    /// Par�metros JSON:
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

            // No se registra la línea de argumentos completa: podría contener
            // rutas personales, tokens u otros datos sensibles del pipeline remoto.
            string nombreEjecutable = Path.GetFileName(comando);
            Logger.Info($"Ejecutando comando externo → {nombreEjecutable}");

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName        = comando,
                    Arguments       = argumentos,
                    UseShellExecute = false,
                    CreateNoWindow  = oculto
                };

                using var proceso = Process.Start(startInfo);
                proceso?.WaitForExit();

                if (proceso == null)
                {
                    Logger.Error($"No se pudo iniciar el comando externo → {nombreEjecutable}");
                }
                else if (proceso.ExitCode != 0)
                {
                    Logger.Warning($"Comando externo finalizado con código {proceso.ExitCode} → {nombreEjecutable}");
                }
                else
                {
                    Logger.Info($"Comando externo completado → {nombreEjecutable}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Fallo al ejecutar comando externo → {nombreEjecutable}", ex);
                throw;
            }

            return Task.CompletedTask;
        }
    }
}
