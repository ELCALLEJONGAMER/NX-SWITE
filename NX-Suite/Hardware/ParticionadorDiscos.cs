using NX_Suite.Core.Configuracion;
using NX_Suite.Hardware.Native;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NX_Suite.Hardware
{
    /// <summary>
    /// Particionado y formateo FAT32 silencioso. Es la ÚNICA implementación de
    /// estas operaciones en el proyecto: tanto el modo Asistido Completo como
    /// el paso "FORMATEARSD" del pipeline JSON delegan aquí.
    ///
    /// Tres modos públicos:
    /// <list type="bullet">
    ///   <item><see cref="ParticionarYFormatearAsync"/>          ? emuMMC oculta + SWITCH SD FAT32 (estilo Hekate).</item>
    ///   <item><see cref="ParticionarSimpleYFormatearAsync"/>    ? 1 partición primary FAT32 (sin emuMMC).</item>
    ///   <item><see cref="FormatearSoloFAT32Async"/>             ? re-formatea la unidad existente sin tocar particiones.</item>
    /// </list>
    /// </summary>
    public class ParticionadorDiscos
    {
        /// <summary>
        /// Devuelve el índice del disco físico al que pertenece la letra
        /// indicada (ej. "E:\") o -1 si no se pudo determinar. Wrapper público
        /// sobre <see cref="DiscoNativo"/> para callers que necesitan resolver
        /// la letra antes de llamar a los modos de particionado.
        /// </summary>
        public int ObtenerIndiceDiscoFisico(string letraSD) => DiscoNativo.GetPhysicalDiskNumber(letraSD);

        // ????????????????????????????????????????????????????????????????????
        //  API PÚBLICA — 3 modos
        // ????????????????????????????????????????????????????????????????????

        /// <summary>
        /// <summary>
        /// Particiona el disco físico exactamente como lo hace Hekate:
        ///   - Partición 1 (SWITCH SD) : FAT32, id=07, letra asignada por Windows.
        ///   - Partición 2 (emuMMC)    : RAW,   id=E0, sin letra (Windows la ignora).
        /// El tamaño de la emuMMC lo determina <paramref name="gbEmuMMC"/> (elegido
        /// por el usuario en el slider); la FAT32 ocupa el resto del disco.
        /// El proceso se divide en dos llamadas a diskpart con una pausa de 5 s
        /// entre ellas para evitar colisiones con el indexador de Windows.
        /// </summary>
        public async Task ParticionarYFormatearAsync(
            int    numeroDisco,
            int    gbEmuMMC,
            string urlFat32FormatZip,
            IProgress<(int Pct, string Msg)> progreso,
            CancellationToken ct = default)
            => await ParticionarYFormatearAsync(
                numeroDisco, gbEmuMMC, urlFat32FormatZip,
                ConfiguracionLocal.EtiquetaSwitchSd, progreso, ct);

        public async Task ParticionarYFormatearAsync(
            int    numeroDisco,
            int    gbEmuMMC,
            string urlFat32FormatZip,
            string etiqueta,
            IProgress<(int Pct, string Msg)> progreso,
            CancellationToken ct = default)
        {
            etiqueta = string.IsNullOrWhiteSpace(etiqueta)
                ? ConfiguracionLocal.EtiquetaSwitchSd
                : etiqueta;

            progreso.Report((5, "Calculando tamaño del disco…"));
            ct.ThrowIfCancellationRequested();

            long totalMb = await ObtenerTamanoDiscoMbAsync(numeroDisco, ct);
            long emuMb   = (long)gbEmuMMC * 1024;
            long fat32Mb = totalMb - emuMb - 2; // 2 MB de margen para MBR + alineación

            if (fat32Mb <= 64)
                throw new InvalidOperationException(
                    $"La SD ({totalMb} MB) es demasiado pequeña para el emuMMC " +
                    $"de {gbEmuMMC} GB más la partición SWITCH SD.");

            // ?? FASE 1: Limpiar y convertir a MBR ???????????????????????????
            // Se ejecuta aparte y se espera 5 segundos antes de crear particiones.
            // Sin esta pausa, el indexador de Windows puede interferir con
            // diskpart y provocar errores al crear particiones en el disco limpio.
            string scriptFase1 = $@"select disk {numeroDisco}
clean
convert mbr
exit";

            progreso.Report((8, "Limpiando disco y convirtiendo a MBR…"));
            await EjecutarScriptDiskpartAsync(scriptFase1, ct);

            progreso.Report((12, "Pausa de seguridad (5 s) antes de particionar…"));
            await Task.Delay(5_000, ct);

            // ?? FASE 2: Crear particiones ????????????????????????????????????
            // Estructura idéntica a Hekate:
            //   create partition primary size={fat32Mb}
            //       ? SWITCH SD ocupa todo menos el bloque final de emuMMC.
            //   format fs=fat32 quick label="SWITCH SD" unit=32768 noerr
            //       ? Intento de formato nativo de diskpart (32 KB = 32768 bytes).
            //         Para SDs > 32 GB este paso FALLA, pero "noerr" garantiza que
            //         diskpart continúe con el resto del script en lugar de abortarlo.
            //         fat32format.exe completará el formato correcto en la siguiente fase.
            //   set id=07  ? tipo "IFS / NTFS" ? Windows asigna letra sin pedir formato.
            //   assign     ? letra de unidad lista para que el usuario copie archivos.
            //   create partition primary
            //       ? emuMMC llena exactamente los {emuMb} MB restantes.
            //   set id=E0  ? tipo de sistema Hekate; Windows lo ignora.
            //   remove noerr ? quita la letra si Windows la asignó; "noerr" evita
            //                  que diskpart aborte si la partición no tenía letra.
            string etiquetaDiskpart = etiqueta.Replace("\"", string.Empty);
            string scriptFase2 = $@"select disk {numeroDisco}
create partition primary size={fat32Mb}
format fs=fat32 quick label=""{etiquetaDiskpart}"" unit=32768 noerr
set id=07
assign
create partition primary
set id=E0
remove noerr
exit";

            progreso.Report((15, "Creando particiones (SWITCH SD + emuMMC)…"));
            await EjecutarScriptDiskpartAsync(scriptFase2, ct);
            progreso.Report((42, "Particiones creadas. Esperando a Windows…"));

            await Task.Delay(3_000, ct);

            progreso.Report((45, "Detectando letra de la partición SWITCH SD…"));
            string? letraRaiz = EncontrarLetraEnDisco(numeroDisco)
                ?? throw new InvalidOperationException(
                    "No se detectó ninguna partición con letra asignada en el disco. " +
                    "El paso 'assign' de diskpart pudo haber fallado.");

            await FormatearYEtiquetarAsync(letraRaiz, urlFat32FormatZip, etiqueta, progreso, ct);
        }

        /// <summary>
        /// Crea una única partición primary que ocupa todo el disco y la formatea
        /// como FAT32. Útil cuando no se necesita emuMMC (instalaciones sysNAND
        /// o reseteo total de la SD).
        /// </summary>
        public async Task ParticionarSimpleYFormatearAsync(
            int    numeroDisco,
            string urlFat32FormatZip,
            string etiqueta,
            IProgress<(int Pct, string Msg)> progreso,
            CancellationToken ct = default)
        {
            string script = $@"select disk {numeroDisco}
clean
convert mbr
create partition primary
assign
exit";

            progreso.Report((5, "Preparando diskpart…"));
            ct.ThrowIfCancellationRequested();

            progreso.Report((10, "Particionando disco (1 partición FAT32)…"));
            await EjecutarScriptDiskpartAsync(script, ct);
            progreso.Report((40, "Partición creada. Esperando a Windows…"));

            await Task.Delay(3000, ct);

            progreso.Report((45, "Detectando letra de la partición…"));
            string? letraRaiz = EncontrarLetraEnDisco(numeroDisco)
                ?? throw new InvalidOperationException(
                    "No se detectó ninguna partición con letra asignada en el disco. " +
                    "El paso 'assign' de diskpart pudo haber fallado.");

            await FormatearYEtiquetarAsync(letraRaiz, urlFat32FormatZip, etiqueta, progreso, ct);
        }

        /// <summary>
        /// Re-formatea la unidad indicada como FAT32 sin tocar la tabla de
        /// particiones. Útil cuando la SD ya está particionada correctamente
        /// y solo hay que limpiar el contenido.
        /// </summary>
        /// <param name="letraRaiz">Ruta raíz de la unidad (ej. "E:\").</param>
        public async Task FormatearSoloFAT32Async(
            string letraRaiz,
            string urlFat32FormatZip,
            string etiqueta,
            IProgress<(int Pct, string Msg)> progreso,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            await FormatearYEtiquetarAsync(letraRaiz, urlFat32FormatZip, etiqueta, progreso, ct);
        }

        // ????????????????????????????????????????????????????????????????????
        //  HELPERS PRIVADOS — compartidos por los 3 modos
        // ????????????????????????????????????????????????????????????????????

        /// <summary>
        /// Recorre todas las unidades del sistema y devuelve la ruta raíz
        /// (ej. "H:\") de la partición con letra asignada que vive en el disco
        /// físico indicado. Funciona con unidades RAW (recién asignadas, sin
        /// formatear) porque no depende de <see cref="DriveInfo.IsReady"/>.
        /// </summary>
        private static string? EncontrarLetraEnDisco(int numeroDisco)
        {
            foreach (DriveInfo d in DriveInfo.GetDrives())
            {
                try
                {
                    if (DiscoNativo.GetPhysicalDiskNumber(d.Name) == numeroDisco)
                        return d.Name; // ej. "H:\"
                }
                catch { /* la unidad no es accesible, continuamos */ }
            }
            return null;
        }

        /// <summary>
        /// Descarga fat32format.exe si no está, formatea la letra como FAT32 y
        /// aplica la etiqueta de volumen — todo silencioso. Reportes de progreso
        /// 50% (preparando) ? 60% (formateando) ? 90% (etiqueta) ? 100% (listo).
        ///
        /// Estrategia anti-fallo (en este orden):
        /// 1. Verifica que la unidad responda a I/O básica (sin esto: "device geometry").
        /// 2. Cierra ventanas de Explorer abiertas en esa unidad (best-effort).
        /// 3. Hace LOCK + DISMOUNT del volumen vía FSCTL_LOCK_VOLUME / FSCTL_DISMOUNT_VOLUME
        ///    para echar a Explorer/indexador/antivirus (sin esto: ERROR_SHARING_VIOLATION exit=32).
        /// 4. Reintenta hasta 3 veces si fat32format falla, con re-lock entre intentos.
        /// 5. Traduce los errores comunes de fat32format a mensajes claros en español.
        /// </summary>
        private static async Task FormatearYEtiquetarAsync(
            string letraRaiz,
            string urlZip,
            string etiqueta,
            IProgress<(int Pct, string Msg)> progreso,
            CancellationToken ct)
        {
            progreso.Report((50, "Preparando fat32format.exe…"));
            string exePath = await AsegurarFat32FormatAsync(urlZip, ct);

            char letra = letraRaiz[0];

            // 1. Esperar a que la unidad esté lista para operaciones de bajo nivel.
            progreso.Report((55, $"Esperando que la unidad {letra}: esté lista…"));
            await EsperarUnidadAccesibleAsync(letraRaiz, ct);

            // 2. Cerrar Explorer en esa ruta (best-effort: no falla si no hay nada que cerrar).
            CerrarExplorerEnUnidad(letra);

            progreso.Report((60, $"Formateando {letra}: como FAT32…"));

            // 3-4. Reintentar con lock+dismount fresco antes de cada intento.
            Exception? ultimoError = null;
            for (int intento = 1; intento <= 3; intento++)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    await EjecutarFat32FormatConDismountAsync(exePath, letra, letraRaiz, ct);
                    ultimoError = null;
                    break;
                }
                catch (Exception ex)
                {
                    ultimoError = ex;
                    Debug.WriteLine($"[Formato] Intento {intento}/3 falló: {ex.Message}");
                    if (intento < 3)
                    {
                        progreso.Report((60 + intento * 5, $"Reintento {intento}/3 en {2 * intento}s…"));
                        await Task.Delay(TimeSpan.FromSeconds(2 * intento), ct);
                        await EsperarUnidadAccesibleAsync(letraRaiz, ct);
                    }
                }
            }
            if (ultimoError != null) throw ultimoError;

            // 5. Etiqueta vía API directa de Windows ? sin ventanas, sin procesos extra.
            progreso.Report((90, $"Aplicando etiqueta {etiqueta}…"));
            await Task.Delay(1500, ct);
            try { DiscoNativo.SetVolumeLabel(letraRaiz, etiqueta); }
            catch (Exception ex) { Debug.WriteLine($"[Formato] No se pudo aplicar etiqueta: {ex.Message}"); }

            progreso.Report((100, "Listo"));
        }

        /// <summary>
        /// Formatea la unidad usando PowerShell <c>Format-Volume</c> como método primario
        /// (maneja su propio locking internamente, sin race condition) y cae en
        /// fat32format.exe como fallback si PowerShell no está disponible o falla.
        /// </summary>
        private static async Task EjecutarFat32FormatConDismountAsync(
            string exePath, char letra, string letraRaiz, CancellationToken ct)
        {
            // ?? Preparación ??????????????????????????????????????????????????
            // El código C++ de referencia que funciona correctamente:
            //   1. EnumWindows para cerrar ventanas Explorer con la letra en el título
            //   2. Corre fat32format directamente con -c64 (sin lock/dismount previo)
            //
            // Nuestros intentos de FSCTL_DISMOUNT_VOLUME previos contraproducen:
            // el dismount fuerza un re-mount automático que otra aplicación captura
            // antes de que fat32format pueda adquirir su propio lock.
            // fat32format ya implementa FSCTL_LOCK_VOLUME internamente — hay que
            // dejarle hacer su trabajo sin interferir.

            // 1. Cerrar ventanas Explorer con la unidad (P/Invoke EnumWindows, igual que el C++)
            DiscoNativo.CerrarVentanasExplorer(letra);

            // 2. Detener Windows Search (principal fuente de handles persistentes)
            DetenerServicio("WSearch");

            // 3. Pequeña pausa para que los handles liberados lleguen al SO
            await Task.Delay(1500, ct);

            try
            {
                // ?? Método 1: fat32format.exe (mismo flujo que el C++ que funciona) ???
                // -c64 = cluster size 64 sectores × 512 bytes = 32 KB (óptimo para Switch SD)
                var psiFmt = new ProcessStartInfo(
                    "cmd.exe", $"/c echo y | \"{exePath}\" -c64 {letra}:")
                {
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                };

                using var procFmt = Process.Start(psiFmt)
                    ?? throw new InvalidOperationException("No se pudo iniciar fat32format.exe.");

                var outTask = procFmt.StandardOutput.ReadToEndAsync(ct);
                var errTask = procFmt.StandardError.ReadToEndAsync(ct);
                await procFmt.WaitForExitAsync(ct);
                string salida = ((await outTask) + "\n" + (await errTask)).Trim();

                if (procFmt.ExitCode == 0 &&
                    !salida.Contains("failed", StringComparison.OrdinalIgnoreCase) &&
                    !salida.Contains("error",  StringComparison.OrdinalIgnoreCase))
                    return; // ? éxito

                Debug.WriteLine($"[Formato] fat32format falló (exit={procFmt.ExitCode}): {salida}");

                // ?? Método 2: PowerShell Format-Volume (fallback) ????????????
                // Solo si fat32format falla — PS gestiona su propio lock exclusivo.
                try
                {
                    await FormatearConPowerShellAsync(letra, ct);
                    return;
                }
                catch (Exception exPs)
                {
                    Debug.WriteLine($"[Formato] PowerShell Format-Volume también falló: {exPs.Message}");
                }

                // Ambos métodos fallaron ? propagar el error de fat32format
                throw new InvalidOperationException(TraducirErrorFat32(procFmt.ExitCode, salida, letra));
            }
            finally
            {
                IniciarServicio("WSearch");
            }
        }

        /// <summary>
        /// Formatea la unidad indicada como FAT32 mediante PowerShell
        /// <c>Format-Volume</c>. PowerShell gestiona internamente el bloqueo
        /// exclusivo del volumen, eliminando la sharing violation de fat32format.
        /// </summary>
        private static async Task FormatearConPowerShellAsync(char letra, CancellationToken ct)
        {
            // AllocationUnitSize 32768 (32 KB) coincide exactamente con fat32format -c64
            // (64 sectores x 512 bytes = 32768 bytes) y con el cluster size que usa Hekate.
            string cmd = $"Format-Volume -DriveLetter {letra} -FileSystem FAT32 " +
                         $"-AllocationUnitSize 32768 -Force -Confirm:$false";

            var psi = new ProcessStartInfo("powershell.exe",
                $"-NonInteractive -NoProfile -ExecutionPolicy Bypass -Command \"{cmd}\"")
            {
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
            };

            using var proc = Process.Start(psi)
                ?? throw new InvalidOperationException("No se pudo iniciar powershell.exe.");

            var outTask = proc.StandardOutput.ReadToEndAsync(ct);
            var errTask = proc.StandardError.ReadToEndAsync(ct);
            await proc.WaitForExitAsync(ct);
            string salida = ((await outTask) + "\n" + (await errTask)).Trim();

            if (proc.ExitCode != 0 || salida.Contains("Error", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Format-Volume falló (exit={proc.ExitCode}): {salida}");
        }

        /// <summary>
        /// Convierte la salida cruda de fat32format en un mensaje claro y accionable
        /// para usuarios sin conocimientos técnicos.
        /// </summary>
        private static string TraducirErrorFat32(int exitCode, string salida, char letra)
        {
            string baja = salida.ToLowerInvariant();

            if (exitCode == 32 || baja.Contains("sharing violation") || baja.Contains("being used by another process") ||
                baja.Contains("siendo utilizado por otro proceso") || baja.Contains("failed to open device"))
            {
                return $"? Otro programa tiene la unidad {letra}: en uso.\n\n" +
                       "Soluciones:\n" +
                       "  1. Cierra TODAS las ventanas del Explorador de Windows que muestren la unidad.\n" +
                       "  2. Desactiva temporalmente el antivirus si está escaneando la SD.\n" +
                       "  3. Espera 10 segundos a que termine el indexador de Windows y vuelve a intentarlo.\n" +
                       "  4. Si el problema persiste, extrae y vuelve a insertar la SD.";
            }

            if (baja.Contains("admin rights") || baja.Contains("administrator"))
            {
                return $"? Faltan permisos de Administrador.\n\n" +
                       "Cierra NX-Suite, haz clic derecho sobre el ícono y selecciona\n" +
                       "\"Ejecutar como administrador\".";
            }

            if (baja.Contains("device geometry") || baja.Contains("not ready"))
            {
                return $"? La unidad {letra}: no está lista.\n\n" +
                       "Verifica que la SD esté bien insertada en el lector.\n" +
                       "Si acabas de insertarla, espera 5 segundos y vuelve a intentarlo.";
            }

            if (baja.Contains("too large") || baja.Contains("too small"))
            {
                return $"? El tamaño de la unidad {letra}: no es compatible con FAT32.\n\n" +
                       "FAT32 admite particiones de 32 MB hasta 2 TB.";
            }

            // Fallback: devolver salida cruda con contexto
            return $"? El formateo de {letra}: falló (código {exitCode}).\n\nDetalles técnicos:\n{salida}";
        }

        /// <summary>
        /// Cierra ventanas del Explorador de Windows que estén mostrando la
        /// unidad indicada. Best-effort: si falla, no aborta el formateo.
        /// Esto reduce significativamente los <c>ERROR_SHARING_VIOLATION</c>
        /// porque Explorer mantiene handles abiertos para miniaturas y caché.
        /// </summary>
        private static void CerrarExplorerEnUnidad(char letra)
        {
            // Cierre fiable vía Shell COM: busca ventanas de Explorer cuya URL
            // corresponda a la unidad y las cierra limpiamente.
            try
            {
                string ps = $"$sh = New-Object -ComObject Shell.Application; " +
                            $"$sh.Windows() | Where-Object {{ $_.LocationURL -like '*{letra}:*' -or " +
                            $"$_.LocationURL -like '*{letra}%3A*' }} | ForEach-Object {{ $_.Quit() }}";
                var psi = new ProcessStartInfo("powershell.exe",
                    $"-NonInteractive -NoProfile -ExecutionPolicy Bypass -Command \"{ps}\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow  = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                };
                using var p = Process.Start(psi);
                p?.WaitForExit(3000);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Formato] No se pudieron cerrar ventanas de Explorer: {ex.Message}");
            }
        }

        /// <summary>
        /// Ejecuta <c>mountvol</c> con el argumento indicado (<c>/N</c> o <c>/E</c>)
        /// de forma silenciosa. Best-effort: nunca lanza excepción.
        /// </summary>
        private static void EjecutarMountvol(string arg)
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo("mountvol", arg)
                {
                    UseShellExecute = false,
                    CreateNoWindow  = true,
                });
                p?.WaitForExit(3000);
                Debug.WriteLine($"[Formato] mountvol {arg} ejecutado.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Formato] mountvol {arg} falló: {ex.Message}");
            }
        }

        /// <summary>Detiene un servicio de Windows por nombre (best-effort, sin excepción).</summary>
        private static void DetenerServicio(string nombre)
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo("net", $"stop \"{nombre}\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow  = true,
                });
                p?.WaitForExit(5000);
                Debug.WriteLine($"[Formato] Servicio '{nombre}' detenido.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Formato] No se pudo detener '{nombre}': {ex.Message}");
            }
        }

        /// <summary>Inicia un servicio de Windows por nombre (best-effort, sin excepción).</summary>
        private static void IniciarServicio(string nombre)
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo("net", $"start \"{nombre}\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow  = true,
                });
                p?.WaitForExit(5000);
                Debug.WriteLine($"[Formato] Servicio '{nombre}' iniciado.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Formato] No se pudo iniciar '{nombre}': {ex.Message}");
            }
        }

        /// <summary>
        /// Espera hasta que la unidad responda a una operación de stat básica.
        /// Cubre el caso típico de SD recién particionada donde Windows tarda
        /// 1-3 segundos en montar el volumen aunque la letra ya esté asignada.
        /// </summary>
        private static async Task EsperarUnidadAccesibleAsync(string letraRaiz, CancellationToken ct)
        {
            for (int i = 0; i < 20; i++) // hasta 20 s
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var di = new DriveInfo(letraRaiz);
                    // No exigimos IsReady (RAW devuelve false). Solo que GetDrives la vea
                    // y que podamos abrir un handle al volumen para verificar acceso bajo nivel.
                    if (DriveInfo.GetDrives().Any(d => d.Name.Equals(letraRaiz, StringComparison.OrdinalIgnoreCase)))
                    {
                        // Probar acceso al volumen físico — esto es lo que fat32format hace
                        try
                        {
                            using var fs = new FileStream($@"\\.\{letraRaiz[0]}:",
                                FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                            return; // ? accesible
                        }
                        catch { /* aún no listo, reintentar */ }
                    }
                }
                catch { /* la unidad aún no aparece en DriveInfo */ }
                await Task.Delay(1000, ct);
            }
            throw new InvalidOperationException(
                $"La unidad {letraRaiz} no está accesible tras 20 segundos. " +
                "Verifica que esté insertada y reconocida por Windows.");
        }

        /// <summary>
        /// Garantiza que fat32format.exe existe en la carpeta de la aplicación.
        /// Si ya existe lo reutiliza (caché). Si no, lo descarga de la URL indicada.
        /// Soporta dos formatos de URL automáticamente:
        ///   • <c>...fat32format.exe</c>  ? descarga directa al destino final.
        ///   • <c>...whatever.zip</c>     ? descarga el ZIP y extrae fat32format.exe de su interior.
        /// La detección se hace por la extensión final del path de la URL.
        /// </summary>
        private static async Task<string> AsegurarFat32FormatAsync(string urlDescarga, CancellationToken ct)
        {
            string exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfiguracionLocal.NombreFat32FormatExe);
            if (File.Exists(exePath)) return exePath;

            if (string.IsNullOrWhiteSpace(urlDescarga))
                throw new InvalidOperationException(
                    "fat32format.exe no encontrado y no hay URL de descarga en el JSON " +
                    "(ConfiguracionUI.UrlFat32Format o paso FORMATEARSD.UrlHerramienta).");

            // Detectar tipo por extensión del path (ignorando query string).
            bool esExeDirecto;
            try
            {
                string pathSolo = new Uri(urlDescarga).LocalPath;
                esExeDirecto    = pathSolo.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                esExeDirecto = urlDescarga.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
            }

            using var http = new System.Net.Http.HttpClient();
            http.Timeout = TimeSpan.FromSeconds(60);

            if (esExeDirecto)
            {
                // Descarga directa: bytes ? exePath
                Debug.WriteLine($"[Fat32] Descarga directa de exe: {urlDescarga}");
                var bytes = await http.GetByteArrayAsync(urlDescarga, ct);
                await File.WriteAllBytesAsync(exePath, bytes, ct);
                return exePath;
            }

            // Flujo legacy: ZIP que contiene fat32format.exe
            string zipPath    = Path.Combine(Path.GetTempPath(), ConfiguracionLocal.NombreFat32FormatZip);
            string tempFolder = Path.Combine(Path.GetTempPath(), ConfiguracionLocal.NombreFat32FormatTemp);

            try
            {
                Debug.WriteLine($"[Fat32] Descarga de ZIP: {urlDescarga}");
                var bytes = await http.GetByteArrayAsync(urlDescarga, ct);
                await File.WriteAllBytesAsync(zipPath, bytes, ct);

                if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true);
                ZipFile.ExtractToDirectory(zipPath, tempFolder);

                string? found = Directory.GetFiles(tempFolder, ConfiguracionLocal.NombreFat32FormatExe, SearchOption.AllDirectories)
                                         .FirstOrDefault();
                if (found == null)
                    throw new InvalidOperationException("El ZIP descargado no contiene fat32format.exe.");

                File.Copy(found, exePath, overwrite: true);
            }
            finally
            {
                try { File.Delete(zipPath); }              catch { }
                try { Directory.Delete(tempFolder, true); } catch { }
            }

            return exePath;
        }

        /// <summary>
        /// Escribe el script de diskpart a un archivo temporal y lo ejecuta de
        /// forma silenciosa. La app tiene <c>requireAdministrator</c> en el
        /// manifest, por lo que diskpart hereda los permisos sin necesitar
        /// <c>Verb="runas"</c>. El exit code de diskpart NO se valida porque
        /// devuelve códigos no estándar para advertencias no fatales (ej.
        /// "remove noerr" sin letra). El éxito real se verifica al detectar la
        /// letra con <see cref="EncontrarLetraEnDisco"/>.
        /// </summary>
        private static async Task EjecutarScriptDiskpartAsync(string script, CancellationToken ct)
        {
            string scriptPath = Path.Combine(Path.GetTempPath(), ConfiguracionLocal.NombreDiskpartScript);
            await File.WriteAllTextAsync(scriptPath, script, System.Text.Encoding.ASCII, ct);

            try
            {
                var psi = new ProcessStartInfo("diskpart.exe", $"/s \"{scriptPath}\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow  = true,
                };

                using var proc = Process.Start(psi)
                    ?? throw new InvalidOperationException("No se pudo iniciar diskpart.");

                await proc.WaitForExitAsync(ct);
            }
            finally
            {
                try { File.Delete(scriptPath); } catch { }
            }
        }

        /// <summary>
        /// Devuelve el tamaño total del disco físico indicado en megabytes,
        /// usando PowerShell <c>Get-Disk</c> para evitar dependencia de WMI/COM.
        /// Lanza excepción si el disco no se puede consultar.
        /// </summary>
        private static async Task<long> ObtenerTamanoDiscoMbAsync(int numeroDisco, CancellationToken ct)
        {
            // Get-Disk devuelve el tamaño en bytes; dividimos en PowerShell para evitar
            // problemas de formato numérico según el locale del sistema.
            var psi = new ProcessStartInfo(
                "powershell.exe",
                $"-NonInteractive -NoProfile -Command " +
                $"\"[Math]::Floor((Get-Disk -Number {numeroDisco}).Size / 1MB)\"")
            {
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
            };

            using var proc = Process.Start(psi)
                ?? throw new InvalidOperationException("No se pudo iniciar PowerShell para consultar el disco.");

            string salida = (await proc.StandardOutput.ReadToEndAsync(ct)).Trim();
            await proc.WaitForExitAsync(ct);

            if (proc.ExitCode != 0 || !long.TryParse(salida, out long mb) || mb <= 0)
                throw new InvalidOperationException(
                    $"No se pudo determinar el tamaño del disco {numeroDisco}. " +
                    $"Salida de PowerShell: '{salida}'");

            return mb;
        }
    }
}
