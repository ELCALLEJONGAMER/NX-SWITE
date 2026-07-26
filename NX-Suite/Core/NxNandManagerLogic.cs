using NX_Swite.Core.Configuracion;
using NX_Swite.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace NX_Swite.Core
{
    /// <summary>
    /// Lógica de detección de firmware interno de una emuMMC mediante el CLI
    /// de NxNandManager. Solo soporta lectura (<c>--info</c>) sobre una emuMMC
    /// RAW (<c>\\.\PhysicalDriveN</c>); la emuMMC basada en archivo queda
    /// descartada por ahora.
    ///
    /// Únicos argumentos permitidos: <c>-i</c>, <c>-keyset</c>, <c>--info</c>.
    /// Ninguna operación de escritura/restauración/copia debe añadirse aquí.
    /// </summary>
    public static class NxNandManagerLogic
    {
        // Ej.: "Firmware ver.  : 18.1.0"
        private static readonly Regex RegexFirmwareVer = new(
            @"^\s*Firmware\s+ver\.?\s*:\s*(?<version>[0-9]+(?:\.[0-9]+){1,3})\s*$",
            RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex RegexNandType = new(
            @"^\s*NAND\s+type\s*:",
            RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Ejecuta NxNandManager sobre la partición RAW de la emuMMC del disco
        /// físico indicado y devuelve el firmware detectado (o el estado de error).
        /// </summary>
        /// <param name="numeroDiscoFisico">Índice de disco físico (ej. de <c>SDInfo.DiscoFisico</c>).</param>
        /// <param name="rutaProdKeys">Ruta absoluta a <c>switch/prod.keys</c>.</param>
        /// <param name="ct">Token de cancelación externo (cambio de unidad, cierre de ventana).</param>
        public static async Task<ResultadoFirmwareEmummc> ObtenerFirmwareRawAsync(
            int numeroDiscoFisico, string rutaProdKeys, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(rutaProdKeys) || !File.Exists(rutaProdKeys))
                return ResultadoFirmwareEmummc.De(EstadoFirmwareEmummc.KeysMissing);

            if (numeroDiscoFisico < 0)
                return ResultadoFirmwareEmummc.De(EstadoFirmwareEmummc.Failed,
                    "Índice de disco físico inválido.");

            // Defensa adicional: el manifest ya garantiza admin, esto cubre casos anómalos.
            if (!EsProcesoElevado())
                return ResultadoFirmwareEmummc.De(EstadoFirmwareEmummc.AccessDenied,
                    "ADMINISTRATOR_PRIVILEGES_REQUIRED");

            string rutaExe;
            try
            {
                rutaExe = await GestorHerramientaNxNandManager.ObtenerRutaEjecutableAsync(ct);
            }
            catch (OperationCanceledException)
            {
                throw; // el llamador decide cómo tratar la cancelación
            }
            catch (HerramientaNoDisponibleException ex)
            {
                return ResultadoFirmwareEmummc.De(EstadoFirmwareEmummc.ToolValidationFailed, ex.Message);
            }

            string target = $@"\\.\PhysicalDrive{numeroDiscoFisico}";
            return await EjecutarInfoAsync(rutaExe, target, rutaProdKeys, ct);
        }

        /// <summary>
        /// Comprobación defensiva de privilegios de administrador vía
        /// <see cref="WindowsPrincipal"/>. No intenta elevar ni relanzar el proceso.
        /// </summary>
        private static bool EsProcesoElevado()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                // Si no se puede determinar, no bloqueamos aquí: el manifest
                // ya exige admin y el fallo real se verá al ejecutar el CLI.
                return true;
            }
        }

        private static async Task<ResultadoFirmwareEmummc> EjecutarInfoAsync(
            string rutaExe, string target, string rutaProdKeys, CancellationToken ctExterno)
        {
            using var ctsTimeout   = new CancellationTokenSource(ConfiguracionLocal.TimeoutCliNxNandManager);
            using var ctsCombinado = CancellationTokenSource.CreateLinkedTokenSource(ctExterno, ctsTimeout.Token);

            var psi = new ProcessStartInfo(rutaExe)
            {
                UseShellExecute        = false,
                CreateNoWindow          = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
            };
            psi.ArgumentList.Add("-i");
            psi.ArgumentList.Add(target);
            psi.ArgumentList.Add("-keyset");
            psi.ArgumentList.Add(rutaProdKeys);
            psi.ArgumentList.Add("--info");

            Process? proceso = null;
            try
            {
                proceso = Process.Start(psi)
                    ?? throw new InvalidOperationException("No se pudo iniciar NxNandManager.");

                var outTask = proceso.StandardOutput.ReadToEndAsync(ctsCombinado.Token);
                var errTask = proceso.StandardError.ReadToEndAsync(ctsCombinado.Token);

                try
                {
                    await proceso.WaitForExitAsync(ctsCombinado.Token);
                }
                catch (OperationCanceledException)
                {
                    await MatarProcesoDeFormaSeguraAsync(proceso);

                    if (ctsTimeout.IsCancellationRequested && !ctExterno.IsCancellationRequested)
                        return ResultadoFirmwareEmummc.De(EstadoFirmwareEmummc.TimedOut, "CLI_TIMEOUT");

                    // Cancelación externa (cambio de unidad / cierre de app): no es un error.
                    throw new OperationCanceledException("OPERATION_CANCELLED", ctExterno);
                }

                string salidaOut = await outTask;
                string salidaErr = await errTask;
                string salida    = (salidaOut + "\n" + salidaErr).Trim();

                return ParsearSalida(salida);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return ResultadoFirmwareEmummc.De(EstadoFirmwareEmummc.Failed,
                    $"CLI_EXECUTION_FAILED: {ex.Message}");
            }
            finally
            {
                proceso?.Dispose();
            }
        }

        private static async Task MatarProcesoDeFormaSeguraAsync(Process proceso)
        {
            try
            {
                if (!proceso.HasExited)
                {
                    proceso.Kill(entireProcessTree: true);
                    await proceso.WaitForExitAsync(CancellationToken.None);
                }
            }
            catch { }
        }

        private static ResultadoFirmwareEmummc ParsearSalida(string salida)
        {
            var match = RegexFirmwareVer.Match(salida);
            if (match.Success)
                return ResultadoFirmwareEmummc.Ok(match.Groups["version"].Value);

            if (RegexNandType.IsMatch(salida))
                return ResultadoFirmwareEmummc.De(EstadoFirmwareEmummc.FirmwareNotDetected, null, salida);

            return ResultadoFirmwareEmummc.De(EstadoFirmwareEmummc.Failed, "SALIDA_INESPERADA", salida);
        }
    }
}
