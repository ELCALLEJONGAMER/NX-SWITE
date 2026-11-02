using NX_Swite.Core.Configuracion;
using NX_Swite.Models;
using NX_Swite.Services;
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
            Logger.Info("[emuMMC] Detectando firmware");

            if (string.IsNullOrWhiteSpace(rutaProdKeys) || !File.Exists(rutaProdKeys))
            {
                Logger.Warning("[emuMMC] No se pudo detectar firmware — faltan prod.keys");
                return ResultadoFirmwareEmummc.De(EstadoFirmwareEmummc.KeysMissing);
            }

            if (numeroDiscoFisico < 0)
            {
                Logger.Error("[emuMMC] No se pudo detectar firmware — índice de disco físico inválido");
                return ResultadoFirmwareEmummc.De(EstadoFirmwareEmummc.Failed,
                    "Índice de disco físico inválido.");
            }

            // Defensa adicional: el manifest ya garantiza admin, esto cubre casos anómalos.
            if (!EsProcesoElevado())
            {
                Logger.Error("[emuMMC] No se pudo detectar firmware — privilegios de administrador insuficientes");
                return ResultadoFirmwareEmummc.De(EstadoFirmwareEmummc.AccessDenied,
                    "ADMINISTRATOR_PRIVILEGES_REQUIRED");
            }

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
                Logger.Error($"[emuMMC] No se pudo detectar firmware — herramienta no disponible: {ex.Message}");
                return ResultadoFirmwareEmummc.De(EstadoFirmwareEmummc.ToolValidationFailed, ex.Message);
            }

            string target = $@"\\.\PhysicalDrive{numeroDiscoFisico}";
            var resultado = await EjecutarInfoAsync(rutaExe, target, rutaProdKeys, ct);


            if (resultado.Estado == EstadoFirmwareEmummc.Detected)
                Logger.Info($"[emuMMC] Firmware detectado: {resultado.Version}");
            else if (resultado.Estado == EstadoFirmwareEmummc.FirmwareNotDetected)
                Logger.Warning("[emuMMC] No se pudo detectar firmware — NAND leída sin firmware identificable");
            else if (resultado.Estado == EstadoFirmwareEmummc.Failed || resultado.Estado == EstadoFirmwareEmummc.TimedOut)
                Logger.Error($"[emuMMC] No se pudo detectar firmware — {resultado.MensajeError}");

            return resultado;
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

        private const long BytesSector       = 512;
        private const long SectoresBoot      = 0x4000; // BOOT0 + BOOT1 (8 MB)
        private const int  MaxEntradasGpt    = 128;

        /// <summary>
        /// Verificación alterna, solo a petición del usuario: copia BOOT+GPT+SYSTEM
        /// de la emuMMC (sector de emummc.ini) a un archivo temporal y lo analiza.
        /// Útil cuando NxNandManager no localiza la emuMMC (p.ej. microSD de 1 TB).
        /// </summary>
        public static async Task<ResultadoFirmwareEmummc> ObtenerFirmwareAlternoAsync(
            int numeroDiscoFisico, string rutaProdKeys,
            IProgress<(int Pct, string Msg)> progreso, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(rutaProdKeys) || !File.Exists(rutaProdKeys))
                return ResultadoFirmwareEmummc.De(EstadoFirmwareEmummc.KeysMissing);

            string? raizSd = Path.GetPathRoot(rutaProdKeys);
            long? sectorBase = string.IsNullOrEmpty(raizSd)
                ? null
                : LeerSectorEmummcIni(Path.Combine(raizSd, "emuMMC", "emummc.ini"));
            if (sectorBase == null)
                return ResultadoFirmwareEmummc.De(EstadoFirmwareEmummc.EmuMmcNotFound);

            progreso.Report((2, "Preparando herramienta de lectura…"));
            string rutaExe;
            try
            {
                rutaExe = await GestorHerramientaNxNandManager.ObtenerRutaEjecutableAsync(ct);
            }
            catch (HerramientaNoDisponibleException ex)
            {
                return ResultadoFirmwareEmummc.De(EstadoFirmwareEmummc.ToolValidationFailed, ex.Message);
            }

            string disco    = $@"\\.\PhysicalDrive{numeroDiscoFisico}";
            string rutaTemp = Path.Combine(ConfiguracionLocal.RutaCache, $"emummc_parcial_{Guid.NewGuid():N}.bin");

            try
            {
                Logger.Info("[emuMMC] Verificación alterna: copiando BOOT+GPT+SYSTEM a archivo temporal");
                bool copiado = await Task.Run(() => CopiarHastaSystem(disco, sectorBase.Value, rutaTemp, progreso, ct), ct);
                if (!copiado)
                    return ResultadoFirmwareEmummc.De(EstadoFirmwareEmummc.FirmwareNotDetected);

                progreso.Report((92, "Analizando la copia con NxNandManager…"));
                var resultado = await EjecutarInfoAsync(rutaExe, rutaTemp, rutaProdKeys, ct);
                if (resultado.Estado == EstadoFirmwareEmummc.Detected && !string.IsNullOrEmpty(resultado.Version))
                {
                    progreso.Report((96, "Guardando la versión verificada…"));
                    string? huella = await Task.Run(() => CalcularHuellaEmummc(numeroDiscoFisico, rutaProdKeys), ct);
                    if (huella != null) RecordarFirmware(huella, resultado.Version);
                }
                progreso.Report((100, "Listo"));
                return resultado;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Warning($"[emuMMC] Verificación alterna fallida: {ex.Message}");
                return ResultadoFirmwareEmummc.De(EstadoFirmwareEmummc.Failed, ex.Message);
            }
            finally
            {
                try { if (File.Exists(rutaTemp)) File.Delete(rutaTemp); } catch { }
            }
        }

        private static long? LeerSectorEmummcIni(string rutaIni)
        {
            if (!File.Exists(rutaIni)) return null;
            foreach (string linea in File.ReadLines(rutaIni))
            {
                string l = linea.Trim();
                if (!l.StartsWith("sector=", StringComparison.OrdinalIgnoreCase)) continue;
                string valor = l["sector=".Length..].Trim();
                if (valor.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) valor = valor[2..];
                return long.TryParse(valor, System.Globalization.NumberStyles.HexNumber, null, out long s) && s > 0
                    ? s : null;
            }
            return null;
        }

        // ── Huella de la emuMMC: recordar la versión verificada ─────────────────

        private const int BytesHuellaSystem = 1024 * 1024;
        private static string RutaCacheFirmware => Path.Combine(ConfiguracionLocal.RutaCache, "firmware_emummc.json");

        /// <summary>
        /// Calcula una huella rápida (~1 MB leído) de la emuMMC: sector de inicio,
        /// tabla GPT y el comienzo de SYSTEM. Una actualización de firmware reescribe
        /// SYSTEM, por lo que la huella cambia y la versión guardada deja de usarse.
        /// </summary>
        public static string? CalcularHuellaEmummc(int numeroDiscoFisico, string rutaProdKeys)
        {
            try
            {
                string? raizSd = Path.GetPathRoot(rutaProdKeys);
                long? sectorBase = string.IsNullOrEmpty(raizSd)
                    ? null
                    : LeerSectorEmummcIni(Path.Combine(raizSd, "emuMMC", "emummc.ini"));
                if (sectorBase == null) return null;

                using var origen = new FileStream($@"\\.\PhysicalDrive{numeroDiscoFisico}",
                    FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var gpt = LeerGptEmummc(origen, sectorBase.Value);
                if (gpt == null) return null;

                var datos = new byte[BytesHuellaSystem];
                origen.Position = (sectorBase.Value + SectoresBoot + gpt.Value.InicioSystem) * BytesSector;
                int leidos = 0;
                while (leidos < datos.Length)
                {
                    int r = origen.Read(datos, leidos, datos.Length - leidos);
                    if (r <= 0) return null;
                    leidos += r;
                }

                using var sha = System.Security.Cryptography.SHA256.Create();
                byte[] prefijo = System.Text.Encoding.ASCII.GetBytes($"{sectorBase}|{gpt.Value.InicioSystem}|{gpt.Value.FinSystem}|");
                sha.TransformBlock(prefijo, 0, prefijo.Length, null, 0);
                sha.TransformBlock(gpt.Value.Entradas, 0, gpt.Value.Entradas.Length, null, 0);
                sha.TransformFinalBlock(datos, 0, datos.Length);
                return Convert.ToHexString(sha.Hash!);
            }
            catch (Exception ex)
            {
                Logger.Warning($"[emuMMC] No se pudo calcular la huella: {ex.Message}");
                return null;
            }
        }

        public static string? ObtenerFirmwareRecordado(string huella)
        {
            var cache = CargarCacheFirmware();
            return cache.TryGetValue(huella, out var v) ? v : null;
        }

        public static void RecordarFirmware(string huella, string version)
        {
            try
            {
                var cache = CargarCacheFirmware();
                cache[huella] = version;
                Directory.CreateDirectory(ConfiguracionLocal.RutaCache);
                File.WriteAllText(RutaCacheFirmware, System.Text.Json.JsonSerializer.Serialize(cache));
            }
            catch (Exception ex)
            {
                Logger.Warning($"[emuMMC] No se pudo guardar el firmware verificado: {ex.Message}");
            }
        }

        private static System.Collections.Generic.Dictionary<string, string> CargarCacheFirmware()
        {
            try
            {
                if (File.Exists(RutaCacheFirmware))
                    return System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, string>>(
                        File.ReadAllText(RutaCacheFirmware)) ?? new();
            }
            catch { }
            return new();
        }

        private static (byte[] Entradas, long InicioSystem, long FinSystem)? LeerGptEmummc(FileStream origen, long sectorBase)
        {
            long inicioGpt = (sectorBase + SectoresBoot) * BytesSector;
            var cabecera = new byte[BytesSector];
            origen.Position = inicioGpt + BytesSector;
            if (origen.Read(cabecera, 0, cabecera.Length) != cabecera.Length ||
                System.Text.Encoding.ASCII.GetString(cabecera, 0, 8) != "EFI PART")
                return null;

            var entradas = new byte[MaxEntradasGpt * 128];
            origen.Position = inicioGpt + 2 * BytesSector;
            if (origen.Read(entradas, 0, entradas.Length) != entradas.Length) return null;

            for (int i = 0; i < MaxEntradasGpt; i++)
            {
                int o = i * 128;
                string nombre = System.Text.Encoding.Unicode.GetString(entradas, o + 56, 72).TrimEnd('\0');
                if (nombre == "SYSTEM")
                    return (entradas, BitConverter.ToInt64(entradas, o + 32), BitConverter.ToInt64(entradas, o + 40));
            }
            return null;
        }

        /// <summary>
        /// Copia (solo lectura) desde el inicio de la emuMMC hasta el final de la
        /// partición SYSTEM, que es lo mínimo que NxNandManager necesita para
        /// identificar el firmware.
        /// </summary>
        private static bool CopiarHastaSystem(string disco, long sectorBase, string rutaDestino,
            IProgress<(int Pct, string Msg)> progreso, CancellationToken ct)
        {
            using var origen = new FileStream(disco, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            var gpt = LeerGptEmummc(origen, sectorBase);
            if (gpt == null) return false;
            long finSystem = gpt.Value.FinSystem;
            if (finSystem <= 0) return false;

            long total = (SectoresBoot + finSystem + 1) * BytesSector;
            Directory.CreateDirectory(Path.GetDirectoryName(rutaDestino)!);
            using var destino = new FileStream(rutaDestino, FileMode.Create, FileAccess.Write, FileShare.None);

            origen.Position = sectorBase * BytesSector;
            var buffer = new byte[4 * 1024 * 1024];
            long copiado = 0;
            while (copiado < total)
            {
                ct.ThrowIfCancellationRequested();
                int pedir = (int)Math.Min(buffer.Length, total - copiado);
                int leidos = origen.Read(buffer, 0, pedir);
                if (leidos <= 0) return false;
                destino.Write(buffer, 0, leidos);
                copiado += leidos;
                progreso.Report(((int)(5 + copiado * 85 / total),
                    $"Leyendo la emuMMC desde la microSD… {copiado / (1024 * 1024)} de {total / (1024 * 1024)} MB"));
            }
            return true;
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
