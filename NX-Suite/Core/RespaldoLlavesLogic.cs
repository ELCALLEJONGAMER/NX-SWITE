using NX_Swite.Core.Configuracion;
using NX_Swite.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NX_Swite.Core
{
    /// <summary>
    /// Lógica de respaldo seguro de llaves de Nintendo Switch.
    ///
    /// Atmosphere crea automáticamente en <c>atmosphere/automatic_backups/</c> dos
    /// volcados por consola:
    /// <list type="bullet">
    ///   <item><c>{SERIAL}_BISKEYS.bin</c> — llaves BIS en binario crudo (128 bytes:
    ///         4 parejas × 32 bytes cada una).</item>
    ///   <item><c>{SERIAL}_PRODINFO.bin</c> — datos de calibración (PRODINFO) cifrados.</item>
    /// </list>
    /// Adicionalmente, tools como lockpick_rcm generan <c>switch/prod.keys</c>, que
    /// contiene las llaves derivadas en formato texto (<c>nombre = hexstring</c>).
    ///
    /// <para><b>Verificación de pertenencia:</b> se comparan los primeros 16 bytes de
    /// <c>BISKEYS.bin</c> (= <c>bis_key_00</c> raw) con el valor de la línea
    /// <c>bis_key_00</c> en <c>prod.keys</c>. Si coinciden, los archivos pertenecen
    /// a la misma consola y es seguro respaldarlos juntos.  Si no coinciden se
    /// muestra una advertencia clara: el usuario puede decidir si proceder.</para>
    ///
    /// <para><b>Destino del respaldo:</b>
    /// <c>Mis Documentos\NX-Swite\Respaldos\{SERIAL}\</c> para que el usuario lo
    /// encuentre fácilmente sin abrir Explorer manualmente.</para>
    /// </summary>
    public class RespaldoLlavesLogic
    {
        // ?? Constantes de rutas dentro de la SD ??????????????????????????

        private const string CarpetaAtmosBackups = "atmosphere/automatic_backups";
        private const string RutaProdKeys         = "switch/prod.keys";

        private const string SufijoBiskeys  = "_BISKEYS.bin";
        private const string SufijoProdinfo = "_PRODINFO.bin";

        /// <summary>
        /// Clave en prod.keys cuyo valor (primeros 16 bytes) debe coincidir
        /// con los primeros 16 bytes del archivo BISKEYS.bin.
        /// </summary>
        private const string ClaveBisKey00 = "bis_key_00";

        // ?? API pública ???????????????????????????????????????????????????

        /// <summary>
        /// Analiza la SD y devuelve el estado de los archivos de llaves sin
        /// copiar nada.  Operación completamente no destructiva.
        /// </summary>
        public AnalisisRespaldoLlaves Analizar(string letraSD)
        {
            var resultado = new AnalisisRespaldoLlaves { LetraSD = letraSD };

            try
            {
                string rutaBackups = Path.Combine(letraSD, CarpetaAtmosBackups);
                resultado.CarpetaAutomaticaExiste = Directory.Exists(rutaBackups);

                if (resultado.CarpetaAutomaticaExiste)
                {
                    // Buscar BISKEYS — el prefijo del nombre es el serial
                    var archivosBiskeys = Directory.GetFiles(rutaBackups, $"*{SufijoBiskeys}");
                    if (archivosBiskeys.Length > 0)
                    {
                        // Si hay varios seriales (consolas distintas conectadas en el pasado),
                        // nos quedamos con el más reciente para ofrecerlo primero.
                        var biskeys = archivosBiskeys
                            .OrderByDescending(f => new FileInfo(f).LastWriteTimeUtc)
                            .First();

                        resultado.RutaBiskeys = biskeys;
                        resultado.Serial      = ExtraerSerial(biskeys, SufijoBiskeys);
                        resultado.HayBiskeys  = true;
                    }

                    // Buscar PRODINFO usando el mismo serial
                    if (!string.IsNullOrEmpty(resultado.Serial))
                    {
                        string rutaProdinfo = Path.Combine(
                            rutaBackups, resultado.Serial + SufijoProdinfo);
                        if (File.Exists(rutaProdinfo))
                        {
                            resultado.RutaProdinfo = rutaProdinfo;
                            resultado.HayProdinfo  = true;
                        }
                    }
                    else if (archivosBiskeys.Length == 0)
                    {
                        // Intentar con PRODINFO si no hay BISKEYS
                        var archivosProdinfo = Directory.GetFiles(rutaBackups, $"*{SufijoProdinfo}");
                        if (archivosProdinfo.Length > 0)
                        {
                            var prodinfo = archivosProdinfo
                                .OrderByDescending(f => new FileInfo(f).LastWriteTimeUtc)
                                .First();
                            resultado.RutaProdinfo = prodinfo;
                            resultado.Serial       = ExtraerSerial(prodinfo, SufijoProdinfo);
                            resultado.HayProdinfo  = true;
                        }
                    }
                }

                // prod.keys
                string rutaProdKeys = Path.Combine(letraSD, RutaProdKeys);
                if (File.Exists(rutaProdKeys))
                {
                    resultado.RutaProdkeys = rutaProdKeys;
                    resultado.HayProdkeys  = true;
                }

                // Verificación criptográfica biskeys ? prod.keys
                if (resultado.HayBiskeys && resultado.HayProdkeys)
                    resultado = VerificarCoincidencia(resultado);
                else if (resultado.HayBiskeys && !resultado.HayProdkeys)
                    resultado.EstadoVerificacion = EstadoVerificacionLlaves.SinProdkeys;
                else if (!resultado.HayBiskeys)
                    resultado.EstadoVerificacion = EstadoVerificacionLlaves.SinBiskeys;

                // Ruta destino sugerida
                if (!string.IsNullOrEmpty(resultado.Serial))
                    resultado.RutaDestino = Path.Combine(
                        ConfiguracionLocal.RutaRespaldosLlaves, resultado.Serial);
            }
            catch (Exception ex)
            {
                resultado.ErrorAnalisis = ex.Message;
            }

            return resultado;
        }

        /// <summary>
        /// Comprueba si ya existe un respaldo en disco para el serial detectado
        /// y si es idéntico al contenido actual de la SD.
        ///
        /// <para>Para <c>prod.keys</c> se usa el <b>número de entradas válidas</b>
        /// como criterio de comparación (más entradas = más reciente), no el tamaño
        /// en bytes ni la fecha del archivo, porque ambos pueden ser engañosos tras
        /// una copia.  Para BISKEYS/PRODINFO se sigue usando tamaño en bytes (son
        /// archivos binarios de tamaño fijo).</para>
        ///
        /// <para>Devuelve <c>true</c> si el respaldo existe y está al día,
        /// <c>false</c> si falta algún archivo o la SD tiene más llaves que el
        /// respaldo local.</para>
        /// </summary>
        public bool RespaldoEstaAlDia(AnalisisRespaldoLlaves analisis)
        {
            if (string.IsNullOrEmpty(analisis.RutaDestino) ||
                !Directory.Exists(analisis.RutaDestino))
                return false;

            bool todoIgual = true;

            // BISKEYS y PRODINFO — comparar por tamaño en bytes (binarios de tamaño fijo)
            void ComprobarArchivoBinario(string? rutaOrigen, string nombreDestino)
            {
                if (rutaOrigen == null) return;
                string rutaDest = Path.Combine(analisis.RutaDestino!, nombreDestino);
                if (!File.Exists(rutaDest))                     { todoIgual = false; return; }
                if (new FileInfo(rutaOrigen).Length !=
                    new FileInfo(rutaDest).Length)              { todoIgual = false; }
            }

            if (analisis.HayBiskeys)
                ComprobarArchivoBinario(analisis.RutaBiskeys,
                                        Path.GetFileName(analisis.RutaBiskeys!));
            if (analisis.HayProdinfo)
                ComprobarArchivoBinario(analisis.RutaProdinfo,
                                        Path.GetFileName(analisis.RutaProdinfo!));

            // prod.keys — comparar por número de entradas válidas
            // La SD puede tener llaves más nuevas aunque el respaldo sea más reciente en fecha.
            if (analisis.HayProdkeys && analisis.RutaProdkeys != null)
            {
                string rutaLocalProdkeys = Path.Combine(analisis.RutaDestino!, "prod.keys");
                if (!File.Exists(rutaLocalProdkeys))
                {
                    todoIgual = false;
                }
                else
                {
                    int entradasSD    = ContarEntradasProdkeys(analisis.RutaProdkeys);
                    int entradasLocal = ContarEntradasProdkeys(rutaLocalProdkeys);
                    // Si la SD tiene más llaves, el respaldo está desactualizado
                    if (entradasSD > entradasLocal)
                        todoIgual = false;
                }
            }

            return todoIgual;
        }

        /// <summary>
        /// Compara las <c>prod.keys</c> de la SD con las del respaldo local y
        /// actualiza el respaldo si la SD tiene más entradas (llaves de firmware
        /// más recientes).
        ///
        /// <para>También actualiza BISKEYS y PRODINFO si difieren en tamaño.</para>
        ///
        /// <para>Se llama <b>antes</b> de la restauración para garantizar que el
        /// respaldo local siempre contiene la versión más valiosa.</para>
        /// </summary>
        /// <returns>
        /// <c>true</c> si se actualizó al menos un archivo; <c>false</c> si el
        /// respaldo ya era el más completo o no había respaldo previo.
        /// </returns>
        public async Task<bool> ActualizarRespaldoSiSDTieneMasLlavesAsync(
            AnalisisRespaldoLlaves analisis)
        {
            if (string.IsNullOrEmpty(analisis.RutaDestino) ||
                !Directory.Exists(analisis.RutaDestino))
                return false;

            bool actualizado = false;

            await Task.Run(() =>
            {
                // ?? prod.keys ??????????????????????????????????????????????
                if (analisis.HayProdkeys && analisis.RutaProdkeys != null)
                {
                    string rutaLocalProdkeys = Path.Combine(analisis.RutaDestino!, "prod.keys");
                    int entradasSD = ContarEntradasProdkeys(analisis.RutaProdkeys);

                    if (File.Exists(rutaLocalProdkeys))
                    {
                        int entradasLocal = ContarEntradasProdkeys(rutaLocalProdkeys);
                        if (entradasSD > entradasLocal)
                        {
                            // SD más completa: actualizar respaldo
                            string bak = rutaLocalProdkeys +
                                         $".bak_{DateTime.Now:yyyyMMdd_HHmmss}";
                            File.Move(rutaLocalProdkeys, bak);
                            File.Copy(analisis.RutaProdkeys, rutaLocalProdkeys, overwrite: false);
                            Logger.RespaldoLlavesActualizadoPorMasEntradas(
                                analisis.Serial ?? "desconocido",
                                entradasLocal, entradasSD);
                            actualizado = true;
                        }
                        // Si local tiene igual o más entradas ? el respaldo local ya es mejor,
                        // NO sobreescribimos (la restauración repondrá la local en la SD).
                    }
                    else
                    {
                        // No hay respaldo previo de prod.keys — copiar desde SD
                        File.Copy(analisis.RutaProdkeys, rutaLocalProdkeys, overwrite: false);
                        actualizado = true;
                    }
                }

                // ?? BISKEYS.bin ????????????????????????????????????????????
                if (analisis.HayBiskeys && analisis.RutaBiskeys != null)
                {
                    string nombre    = Path.GetFileName(analisis.RutaBiskeys);
                    string rutaLocal = Path.Combine(analisis.RutaDestino!, nombre);
                    if (!File.Exists(rutaLocal) ||
                        new FileInfo(analisis.RutaBiskeys).Length !=
                        new FileInfo(rutaLocal).Length)
                    {
                        string bak = rutaLocal + $".bak_{DateTime.Now:yyyyMMdd_HHmmss}";
                        if (File.Exists(rutaLocal)) File.Move(rutaLocal, bak);
                        File.Copy(analisis.RutaBiskeys, rutaLocal, overwrite: false);
                        actualizado = true;
                    }
                }

                // ?? PRODINFO.bin ???????????????????????????????????????????
                if (analisis.HayProdinfo && analisis.RutaProdinfo != null)
                {
                    string nombre    = Path.GetFileName(analisis.RutaProdinfo);
                    string rutaLocal = Path.Combine(analisis.RutaDestino!, nombre);
                    if (!File.Exists(rutaLocal) ||
                        new FileInfo(analisis.RutaProdinfo).Length !=
                        new FileInfo(rutaLocal).Length)
                    {
                        string bak = rutaLocal + $".bak_{DateTime.Now:yyyyMMdd_HHmmss}";
                        if (File.Exists(rutaLocal)) File.Move(rutaLocal, bak);
                        File.Copy(analisis.RutaProdinfo, rutaLocal, overwrite: false);
                        actualizado = true;
                    }
                }
            });

            return actualizado;
        }

        /// <summary>
        /// Cuenta el número de entradas válidas en un archivo <c>prod.keys</c>
        /// (líneas con formato <c>nombre = hexstring</c>).
        ///
        /// <para>Esta métrica es el indicador más fiable de qué versión de firmware
        /// ha sido volcada: cada actualización del sistema añade nuevas entradas.
        /// Una prod.keys con 300 entradas es siempre más reciente/valiosa que una
        /// con 200, independientemente de fecha o tamaño del archivo.</para>
        /// </summary>
        public static int ContarEntradasProdkeys(string rutaArchivo)
        {
            if (!File.Exists(rutaArchivo)) return 0;
            try
            {
                int count = 0;
                foreach (string linea in File.ReadLines(rutaArchivo, Encoding.UTF8))
                {
                    string t = linea.Trim();
                    // Línea válida: contiene '=' y el valor parece hexadecimal (? 16 chars)
                    int idx = t.IndexOf('=');
                    if (idx <= 0) continue;
                    string valor = t[(idx + 1)..].Trim();
                    if (valor.Length >= 16 && IsHex(valor))
                        count++;
                }
                return count;
            }
            catch { return 0; }
        }

        /// <summary>
        /// Devuelve información comparativa entre las <c>prod.keys</c> de la SD
        /// y las del respaldo local, útil para logs y decisiones de restauración.
        /// </summary>
        public ComparacionProdkeys CompararProdkeys(
            string rutaProdkeysSD,
            string rutaDestino)
        {
            string rutaLocal = Path.Combine(rutaDestino, "prod.keys");
            int entradasSD    = ContarEntradasProdkeys(rutaProdkeysSD);
            int entradasLocal = ContarEntradasProdkeys(rutaLocal);

            return new ComparacionProdkeys
            {
                EntradasSD          = entradasSD,
                EntradasLocal       = entradasLocal,
                SDTieneMasEntradas  = entradasSD > entradasLocal,
                LocalTieneMasEntradas = entradasLocal > entradasSD,
                SonIguales          = entradasSD == entradasLocal,
                RutaLocalExiste     = File.Exists(rutaLocal)
            };
        }

        private static bool IsHex(string s)
        {
            foreach (char c in s)
                if (!Uri.IsHexDigit(c)) return false;
            return true;
        }

        // ?? Respaldos locales ?????????????????????????????????????????????

        /// <summary>
        /// Enumera todas las carpetas de respaldo locales existentes en
        /// <c>Documentos\NX-Swite\Respaldos\</c>, una por número de serie.
        /// Devuelve la lista ordenada del más reciente al más antiguo.
        /// </summary>
        public static List<RespaldoLocal> ListarRespaldosLocales()
        {
            var resultado = new List<RespaldoLocal>();
            string raiz = ConfiguracionLocal.RutaRespaldosLlaves;
            if (!Directory.Exists(raiz)) return resultado;

            foreach (string carpeta in Directory.GetDirectories(raiz))
            {
                string serial = Path.GetFileName(carpeta);
                if (string.IsNullOrWhiteSpace(serial)) continue;

                var item = new RespaldoLocal { Serial = serial, RutaCarpeta = carpeta };

                string biskeys  = Directory.GetFiles(carpeta, $"*{SufijoBiskeys}").FirstOrDefault() ?? string.Empty;
                string prodinfo = Directory.GetFiles(carpeta, $"*{SufijoProdinfo}").FirstOrDefault() ?? string.Empty;
                string prodkeys = Path.Combine(carpeta, "prod.keys");
                string cert     = Path.Combine(carpeta, "certificado.txt");

                item.HayBiskeys  = File.Exists(biskeys);
                item.HayProdinfo = File.Exists(prodinfo);
                item.HayProdkeys = File.Exists(prodkeys);
                item.HayCertificado = File.Exists(cert);
                item.EntradasProdkeys = item.HayProdkeys
                    ? ContarEntradasProdkeys(prodkeys) : 0;

                // Fecha del respaldo = más reciente de los archivos
                var fechas = new List<DateTime>();
                if (item.HayBiskeys)  fechas.Add(new FileInfo(biskeys).LastWriteTime);
                if (item.HayProdinfo) fechas.Add(new FileInfo(prodinfo).LastWriteTime);
                if (item.HayProdkeys) fechas.Add(new FileInfo(prodkeys).LastWriteTime);
                item.FechaRespaldo = fechas.Count > 0 ? fechas.Max() : DateTime.MinValue;

                resultado.Add(item);
            }

            return resultado.OrderByDescending(r => r.FechaRespaldo).ToList();
        }

        /// <summary>
        /// Genera el archivo <c>certificado.txt</c> dentro de la carpeta del serial.
        /// Incluye: serial, bis_key_00 descompuesto (4 × 32 hex chars), número de
        /// entradas de prod.keys, fecha y versión de NX-Swite que creó el respaldo.
        /// </summary>
        public static string GenerarCertificadoTxt(AnalisisRespaldoLlaves analisis)
        {
            if (string.IsNullOrEmpty(analisis.RutaDestino) || string.IsNullOrEmpty(analisis.Serial))
                return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("=============================================================");
            sb.AppendLine("  CERTIFICADO DE RESPALDO DE LLAVES — NX-SWITE");
            sb.AppendLine("=============================================================");
            sb.AppendLine($"  Generado por : NX-Swite v{ConfiguracionLocal.VersionActual}");
            sb.AppendLine($"  Fecha        : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("-------------------------------------------------------------");
            sb.AppendLine($"  Número de serie  : {analisis.Serial}");
            sb.AppendLine();

            // Sección BISKEYS — descomponer los 4 pares (bis_key_00 a bis_key_03)
            if (analisis.HayBiskeys && File.Exists(analisis.RutaBiskeys))
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(analisis.RutaBiskeys!);
                    sb.AppendLine("  BISKEYS (bis_key_00 a bis_key_03):");
                    // Cada bis_key = 32 bytes (crypt_key 16 B + tweak_key 16 B)
                    for (int i = 0; i < 4 && (i + 1) * 32 <= bytes.Length; i++)
                    {
                        string hex = BitConverter.ToString(bytes, i * 32, 32)
                                                 .Replace("-", "").ToLowerInvariant();
                        sb.AppendLine($"    bis_key_0{i} = {hex}");
                    }
                }
                catch { sb.AppendLine("  BISKEYS : no se pudo leer"); }
            }
            else
            {
                sb.AppendLine("  BISKEYS : no encontrado");
            }
            sb.AppendLine();

            // Sección prod.keys
            if (analisis.HayProdkeys && File.Exists(analisis.RutaProdkeys))
            {
                int entradas = ContarEntradasProdkeys(analisis.RutaProdkeys!);
                sb.AppendLine($"  prod.keys : {entradas} entradas válidas");
            }
            else
            {
                sb.AppendLine("  prod.keys : no encontrado");
            }
            sb.AppendLine();

            // Estado de verificación
            sb.AppendLine($"  Verificación criptográfica : {analisis.EstadoVerificacion}");
            if (!string.IsNullOrEmpty(analisis.DetalleVerificacion))
                sb.AppendLine($"    Detalle : {analisis.DetalleVerificacion}");

            sb.AppendLine();
            sb.AppendLine("  ?  ESTE ARCHIVO ES SOLO INFORMATIVO.");
            sb.AppendLine("     Las llaves de consola son únicas e intransferibles.");
            sb.AppendLine("     Mezclar llaves de consolas distintas provoca daño permanente.");
            sb.AppendLine("=============================================================");

            string ruta = Path.Combine(analisis.RutaDestino, "certificado.txt");
            try { File.WriteAllText(ruta, sb.ToString(), Encoding.UTF8); }
            catch { }
            return ruta;
        }

        /// <summary>
        /// Restaura los archivos de un respaldo local a la SD indicada.
        /// A diferencia de <see cref="RestaurarAsync"/> (que parte del análisis de
        /// una SD viva), este método opera únicamente desde los archivos en disco.
        ///
        /// <para><b>Seguridad:</b> si la SD actualmente tiene llaves (BISKEYS),
        /// se verifica que el bis_key_00 del respaldo coincida con el de la SD
        /// antes de restaurar, para evitar mezcla de consolas.</para>
        /// </summary>
        public async Task<ResultadoRestauracionLlaves> RestaurarDesdeRespaldoLocalAsync(
            RespaldoLocal respaldo,
            string letraSD,
            int timeoutMs = 15_000)
        {
            var resultado = new ResultadoRestauracionLlaves { Serial = respaldo.Serial };

            bool sdDisponible = await EsperarSDDisponibleAsync(letraSD, timeoutMs);
            if (!sdDisponible)
            {
                resultado.Omitida       = true;
                resultado.MotivoOmision = $"La SD {letraSD} no fue accesible en {timeoutMs / 1000} s.";
                Logger.RestauracionLlavesFallida(resultado.Serial, resultado.MotivoOmision);
                return resultado;
            }

            // Verificar que las llaves pertenecen a la misma SD si ya tiene BISKEYS
            string biskeysEnSD = Directory.Exists(Path.Combine(letraSD, CarpetaAtmosBackups))
                ? Directory.GetFiles(Path.Combine(letraSD, CarpetaAtmosBackups), $"*{SufijoBiskeys}")
                            .FirstOrDefault() ?? string.Empty
                : string.Empty;

            if (File.Exists(biskeysEnSD))
            {
                string biskeysLocal = Directory.GetFiles(respaldo.RutaCarpeta, $"*{SufijoBiskeys}")
                                               .FirstOrDefault() ?? string.Empty;
                if (File.Exists(biskeysLocal))
                {
                    byte[] sdBytes    = File.ReadAllBytes(biskeysEnSD);
                    byte[] localBytes = File.ReadAllBytes(biskeysLocal);
                    bool coincide = sdBytes.Length >= 16 && localBytes.Length >= 16 &&
                                    sdBytes[..16].SequenceEqual(localBytes[..16]);
                    if (!coincide)
                    {
                        resultado.Omitida       = true;
                        resultado.MotivoOmision =
                            "? BLOQUEADO: bis_key_00 del respaldo NO coincide con la SD actual. " +
                            "Son consolas distintas. Restauración cancelada.";
                        Logger.RestauracionLlavesFallida(resultado.Serial, resultado.MotivoOmision);
                        return resultado;
                    }
                }
            }

            Logger.RestauracionLlavesIniciada(resultado.Serial, letraSD);

            var archivosRestaurados = new List<string>();
            var errores             = new List<string>();

            await Task.Run(() =>
            {
                string carpetaBackupsSD = Path.Combine(letraSD, CarpetaAtmosBackups);
                string carpetaSwitchSD  = Path.Combine(letraSD, "switch");

                // BISKEYS.bin
                foreach (string f in Directory.GetFiles(respaldo.RutaCarpeta, $"*{SufijoBiskeys}"))
                    RestaurarArchivo(f, carpetaBackupsSD, archivosRestaurados, errores);

                // PRODINFO.bin
                foreach (string f in Directory.GetFiles(respaldo.RutaCarpeta, $"*{SufijoProdinfo}"))
                    RestaurarArchivo(f, carpetaBackupsSD, archivosRestaurados, errores);

                // prod.keys
                string prodkeys = Path.Combine(respaldo.RutaCarpeta, "prod.keys");
                if (File.Exists(prodkeys))
                    RestaurarArchivo(prodkeys, carpetaSwitchSD, archivosRestaurados, errores,
                                     nombreDestino: "prod.keys");
            });

            resultado.ArchivosRestaurados = archivosRestaurados;
            resultado.Errores             = errores;
            resultado.Exito               = archivosRestaurados.Count > 0;

            if (resultado.Exito)
                Logger.RestauracionLlavesCompletada(resultado.Serial, archivosRestaurados.Count);
            else
                Logger.RestauracionLlavesFallida(resultado.Serial, string.Join("; ", errores));

            return resultado;
        }

        /// <summary>
        /// Restaura los archivos respaldados de vuelta a la SD tras una operación
        /// destructiva (limpieza / formateo / particionado).
        ///
        /// <para><b>Seguridad:</b> solo restaura si la verificación del respaldo fue
        /// <see cref="EstadoVerificacionLlaves.Verificado"/> o
        /// <see cref="EstadoVerificacionLlaves.SinProdkeys"/>.  Si fue
        /// <see cref="EstadoVerificacionLlaves.Discrepancia"/> no restaura
        /// automáticamente para evitar mezclar llaves de consolas distintas.</para>
        ///
        /// <para>La SD puede tardar en volver a ser accesible tras el formateo.
        /// Este método espera hasta <paramref name="timeoutMs"/> ms antes de darse
        /// por vencido.</para>
        /// </summary>
        public async Task<ResultadoRestauracionLlaves> RestaurarAsync(
            AnalisisRespaldoLlaves analisis,
            string letraSD,
            int timeoutMs = 15_000)
        {
            var resultado = new ResultadoRestauracionLlaves { Serial = analisis.Serial ?? "desconocido" };

            // Guardia: no restaurar si hay discrepancia de llaves
            if (analisis.EstadoVerificacion == EstadoVerificacionLlaves.Discrepancia)
            {
                resultado.Omitida   = true;
                resultado.MotivoOmision =
                    "Respaldo omitido: discrepancia de llaves detectada. " +
                    "Restauración manual requerida para evitar mezcla de consolas.";
                Logger.RespaldoLlavesAutoOmitido(resultado.Serial, resultado.MotivoOmision);
                return resultado;
            }

            if (string.IsNullOrEmpty(analisis.RutaDestino) ||
                !Directory.Exists(analisis.RutaDestino))
            {
                resultado.Omitida        = true;
                resultado.MotivoOmision  = "Carpeta de respaldo no encontrada.";
                Logger.RestauracionLlavesFallida(resultado.Serial, resultado.MotivoOmision);
                return resultado;
            }

            Logger.RestauracionLlavesIniciada(resultado.Serial, letraSD);

            try
            {
                // Esperar a que la SD vuelva a estar accesible (el formateo la desmonta)
                bool sdDisponible = await EsperarSDDisponibleAsync(letraSD, timeoutMs);
                if (!sdDisponible)
                {
                    resultado.Omitida       = true;
                    resultado.MotivoOmision =
                        $"La SD {letraSD} no fue accesible en {timeoutMs / 1000} s tras la operación.";
                    Logger.RestauracionLlavesFallida(resultado.Serial, resultado.MotivoOmision);
                    return resultado;
                }

                var archivosRestaurados = new List<string>();
                var errores             = new List<string>();

                await Task.Run(() =>
                {
                    // 1) BISKEYS.bin ? atmosphere/automatic_backups/
                    if (analisis.HayBiskeys && analisis.RutaBiskeys != null)
                    {
                        string nombreBiskeys  = Path.GetFileName(analisis.RutaBiskeys);
                        string origenBiskeys  = Path.Combine(analisis.RutaDestino!, nombreBiskeys);
                        string carpetaBackups = Path.Combine(letraSD, CarpetaAtmosBackups);
                        RestaurarArchivo(origenBiskeys, carpetaBackups,
                                         archivosRestaurados, errores);
                    }

                    // 2) PRODINFO.bin ? atmosphere/automatic_backups/
                    if (analisis.HayProdinfo && analisis.RutaProdinfo != null)
                    {
                        string nombreProdinfo = Path.GetFileName(analisis.RutaProdinfo);
                        string origenProdinfo = Path.Combine(analisis.RutaDestino!, nombreProdinfo);
                        string carpetaBackups = Path.Combine(letraSD, CarpetaAtmosBackups);
                        RestaurarArchivo(origenProdinfo, carpetaBackups,
                                         archivosRestaurados, errores);
                    }

                    // 3) prod.keys ? switch/
                    if (analisis.HayProdkeys)
                    {
                        string origenProdkeys = Path.Combine(analisis.RutaDestino!, "prod.keys");
                        string carpetaSwitch  = Path.Combine(letraSD, "switch");
                        RestaurarArchivo(origenProdkeys, carpetaSwitch,
                                         archivosRestaurados, errores,
                                         nombreDestino: "prod.keys");
                    }
                });

                resultado.ArchivosRestaurados = archivosRestaurados;
                resultado.Errores             = errores;
                resultado.Exito               = archivosRestaurados.Count > 0;

                if (resultado.Exito)
                    Logger.RestauracionLlavesCompletada(resultado.Serial, archivosRestaurados.Count);
                else if (errores.Count > 0)
                    Logger.RestauracionLlavesFallida(resultado.Serial, string.Join("; ", errores));
            }
            catch (Exception ex)
            {
                resultado.Exito = false;
                resultado.Errores.Add(ex.Message);
                Logger.RestauracionLlavesFallida(resultado.Serial, ex.Message);
            }

            return resultado;
        }

        /// <summary>
        /// Espera hasta que la letra de unidad indicada sea accesible (existe la
        /// carpeta raíz).  Útil tras un formateo que desmonta y remonta la SD.
        /// </summary>
        private static async Task<bool> EsperarSDDisponibleAsync(string letraSD, int timeoutMs)
        {
            int transcurrido = 0;
            const int intervalo = 500;

            while (transcurrido < timeoutMs)
            {
                try
                {
                    if (Directory.Exists(letraSD))
                        return true;
                }
                catch { }

                await Task.Delay(intervalo);
                transcurrido += intervalo;
            }
            return false;
        }

        private static void RestaurarArchivo(
            string rutaOrigen,
            string carpetaDestino,
            List<string> restaurados,
            List<string> errores,
            string? nombreDestino = null)
        {
            try
            {
                if (!File.Exists(rutaOrigen))
                {
                    // No hay respaldo de ese archivo específico ? no es un error
                    return;
                }

                Directory.CreateDirectory(carpetaDestino);
                string nombre      = nombreDestino ?? Path.GetFileName(rutaOrigen);
                string rutaDestino = Path.Combine(carpetaDestino, nombre);

                File.Copy(rutaOrigen, rutaDestino, overwrite: true);
                restaurados.Add(nombre);
            }
            catch (Exception ex)
            {
                errores.Add($"{Path.GetFileName(rutaOrigen)}: {ex.Message}");
            }
        }

        /// <summary>
        /// Ejecuta el respaldo: copia los archivos encontrados al destino seguro.
        /// Devuelve el <see cref="ResultadoRespaldoLlaves"/> con el detalle de
        /// cada archivo copiado o el error encontrado.
        /// </summary>
        public async Task<ResultadoRespaldoLlaves> RespaldarAsync(AnalisisRespaldoLlaves analisis)
        {
            var resultado = new ResultadoRespaldoLlaves();

            try
            {
                if (string.IsNullOrEmpty(analisis.RutaDestino))
                    return ResultadoRespaldoLlaves.Error("No se pudo determinar la ruta de destino.");

                Logger.RespaldoLlavesIniciado(analisis.Serial ?? "desconocido", analisis.RutaDestino);

                // Crear carpeta destino (incluye sub-carpeta con el serial)
                Directory.CreateDirectory(analisis.RutaDestino);

                var archivosCopiados = new List<string>();
                var errores = new List<string>();

                await Task.Run(() =>
                {
                    // 1) BISKEYS.bin
                    if (analisis.HayBiskeys && analisis.RutaBiskeys != null)
                        CopiarArchivo(analisis.RutaBiskeys, analisis.RutaDestino,
                                      archivosCopiados, errores);

                    // 2) PRODINFO.bin
                    if (analisis.HayProdinfo && analisis.RutaProdinfo != null)
                        CopiarArchivo(analisis.RutaProdinfo, analisis.RutaDestino,
                                      archivosCopiados, errores);

                    // 3) prod.keys — siempre con nombre fijo para consistencia
                    if (analisis.HayProdkeys && analisis.RutaProdkeys != null)
                        CopiarArchivo(analisis.RutaProdkeys, analisis.RutaDestino,
                                      archivosCopiados, errores, nombreDestino: "prod.keys");
                });

                resultado.ArchivosCopiados = archivosCopiados;
                resultado.Errores          = errores;
                resultado.RutaDestino      = analisis.RutaDestino;
                resultado.Exito            = errores.Count == 0 && archivosCopiados.Count > 0;

                if (resultado.Exito)
                    Logger.RespaldoLlavesCompletado(
                        analisis.Serial ?? "desconocido", archivosCopiados.Count, analisis.RutaDestino);
                else
                    Logger.RespaldoLlavesFallido(
                        analisis.Serial ?? "desconocido", string.Join("; ", errores));
            }
            catch (Exception ex)
            {
                resultado.Exito = false;
                resultado.Errores.Add(ex.Message);
                Logger.RespaldoLlavesFallido(analisis.Serial ?? "desconocido", ex.Message);
            }

            return resultado;
        }

        // ?? Verificación criptográfica ????????????????????????????????????

        /// <summary>
        /// Compara los primeros 16 bytes de BISKEYS.bin (= <c>bis_key_00</c> crudo)
        /// con el valor de <c>bis_key_00</c> parseado de <c>prod.keys</c>.
        ///
        /// <para>Si coinciden ? <see cref="EstadoVerificacionLlaves.Verificado"/>.</para>
        /// <para>Si no coinciden ? <see cref="EstadoVerificacionLlaves.Discrepancia"/>:
        /// las llaves NO pertenecen a la misma consola.</para>
        /// </summary>
        private static AnalisisRespaldoLlaves VerificarCoincidencia(AnalisisRespaldoLlaves analisis)
        {
            try
            {
                // Leer primeros 16 bytes del BISKEYS.bin (= bis_key_00 crypt half)
                byte[] biskeysBytes = File.ReadAllBytes(analisis.RutaBiskeys!);
                if (biskeysBytes.Length < 16)
                {
                    analisis.EstadoVerificacion  = EstadoVerificacionLlaves.ArchivoInvalido;
                    analisis.DetalleVerificacion = "BISKEYS.bin demasiado pequeño (< 16 bytes).";
                    return analisis;
                }

                byte[] bisKey00FromFile = biskeysBytes[..16];

                // Parsear prod.keys y obtener bis_key_00
                byte[]? bisKey00FromProdkeys = LeerBisKey00DeProdkeys(analisis.RutaProdkeys!);
                if (bisKey00FromProdkeys == null)
                {
                    analisis.EstadoVerificacion  = EstadoVerificacionLlaves.ClaveNoEncontrada;
                    analisis.DetalleVerificacion =
                        $"No se encontró '{ClaveBisKey00}' en prod.keys.";
                    return analisis;
                }

                bool coincide = bisKey00FromFile.SequenceEqual(bisKey00FromProdkeys);
                analisis.EstadoVerificacion  = coincide
                    ? EstadoVerificacionLlaves.Verificado
                    : EstadoVerificacionLlaves.Discrepancia;

                if (!coincide)
                    analisis.DetalleVerificacion =
                        "? bis_key_00 de prod.keys NO coincide con BISKEYS.bin. " +
                        "Estos archivos pueden ser de consolas distintas.";
            }
            catch (Exception ex)
            {
                analisis.EstadoVerificacion  = EstadoVerificacionLlaves.ErrorLectura;
                analisis.DetalleVerificacion = $"Error al verificar: {ex.Message}";
            }

            return analisis;
        }

        /// <summary>
        /// Parsea el archivo prod.keys (formato <c>clave = hexstring</c>) y devuelve
        /// los bytes de la línea <c>bis_key_00</c>, o <c>null</c> si no se encuentra.
        /// </summary>
        private static byte[]? LeerBisKey00DeProdkeys(string rutaProdkeys)
        {
            try
            {
                foreach (string linea in File.ReadLines(rutaProdkeys, Encoding.UTF8))
                {
                    string trimmed = linea.Trim();
                    if (!trimmed.StartsWith(ClaveBisKey00, StringComparison.OrdinalIgnoreCase))
                        continue;

                    int signoIgual = trimmed.IndexOf('=');
                    if (signoIgual < 0) continue;

                    string hex = trimmed[(signoIgual + 1)..].Trim();
                    if (hex.Length < 32) continue; // bis_key_00 = 16 bytes = 32 hex chars

                    // Tomar solo los primeros 32 hex chars (16 bytes)
                    return HexToBytes(hex[..32]);
                }
            }
            catch { }
            return null;
        }

        // ?? Helpers ???????????????????????????????????????????????????????

        private static string ExtraerSerial(string rutaArchivo, string sufijo)
        {
            string nombre = Path.GetFileName(rutaArchivo);
            // nombre = "XJW10019013427_BISKEYS.bin"
            int idx = nombre.LastIndexOf(sufijo, StringComparison.OrdinalIgnoreCase);
            if (idx > 0)
                return nombre[..idx];
            // Fallback: antes del primer '_'
            idx = nombre.IndexOf('_');
            return idx > 0 ? nombre[..idx] : nombre;
        }

        private static void CopiarArchivo(
            string rutaOrigen,
            string carpetaDestino,
            List<string> copiados,
            List<string> errores,
            string? nombreDestino = null)
        {
            try
            {
                string nombre       = nombreDestino ?? Path.GetFileName(rutaOrigen);
                string rutaDestino  = Path.Combine(carpetaDestino, nombre);

                // Evitar sobrescribir un respaldo existente sin verificar integridad:
                // si ya existe un archivo con el mismo nombre y mismo tamaño, omitir.
                if (File.Exists(rutaDestino))
                {
                    var infoOrigen  = new FileInfo(rutaOrigen);
                    var infoDestino = new FileInfo(rutaDestino);
                    if (infoOrigen.Length == infoDestino.Length)
                    {
                        copiados.Add($"{nombre} (ya existía, omitido)");
                        return;
                    }
                    // Tamaño distinto ? renombrar el anterior como backup de emergencia
                    string respaldoAnterior = rutaDestino + $".bak_{DateTime.Now:yyyyMMdd_HHmmss}";
                    File.Move(rutaDestino, respaldoAnterior);
                }

                File.Copy(rutaOrigen, rutaDestino, overwrite: false);
                copiados.Add(nombre);
            }
            catch (Exception ex)
            {
                errores.Add($"{Path.GetFileName(rutaOrigen)}: {ex.Message}");
            }
        }

        private static byte[] HexToBytes(string hex)
        {
            hex = hex.Replace(" ", "").Replace("-", "");
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return bytes;
        }
    }

    // ?? Modelos ???????????????????????????????????????????????????????????

    /// <summary>
    /// Resultado del análisis previo al respaldo.  Inmutable desde el constructor;
    /// el overlya lo muestra antes de que el usuario confirme la operación.
    /// </summary>
    public class AnalisisRespaldoLlaves
    {
        public string  LetraSD                 { get; set; } = string.Empty;
        public string? Serial                  { get; set; }
        public bool    CarpetaAutomaticaExiste { get; set; }
        public bool    HayBiskeys              { get; set; }
        public bool    HayProdinfo             { get; set; }
        public bool    HayProdkeys             { get; set; }
        public string? RutaBiskeys             { get; set; }
        public string? RutaProdinfo            { get; set; }
        public string? RutaProdkeys            { get; set; }
        public string? RutaDestino             { get; set; }
        public string? ErrorAnalisis           { get; set; }

        public EstadoVerificacionLlaves EstadoVerificacion  { get; set; } =
            EstadoVerificacionLlaves.NoRealizada;
        public string? DetalleVerificacion { get; set; }

        /// <summary>
        /// <c>true</c> si la verificación criptográfica fue exitosa o si no se pudo
        /// realizar (sin prod.keys) pero hay suficientes archivos para respaldar.
        /// </summary>
        public bool EsSeguroRespaldar =>
            EstadoVerificacion is EstadoVerificacionLlaves.Verificado
                                or EstadoVerificacionLlaves.SinProdkeys
                                or EstadoVerificacionLlaves.ClaveNoEncontrada
                                or EstadoVerificacionLlaves.NoRealizada;

        /// <summary><c>true</c> si hay al menos un archivo que respaldar.</summary>
        public bool TieneArchivos => HayBiskeys || HayProdinfo || HayProdkeys;
    }

    /// <summary>Estado de la comparación criptográfica biskeys ? prod.keys.</summary>
    public enum EstadoVerificacionLlaves
    {
        /// <summary>No se intentó (faltan datos de entrada).</summary>
        NoRealizada,
        /// <summary>bis_key_00 coincide en ambos archivos ? misma consola.</summary>
        Verificado,
        /// <summary>bis_key_00 NO coincide ? consolas distintas, peligro de mezcla.</summary>
        Discrepancia,
        /// <summary>No hay prod.keys en la SD — solo se respalda el BISKEYS/PRODINFO.</summary>
        SinProdkeys,
        /// <summary>No hay BISKEYS.bin — no se puede verificar.</summary>
        SinBiskeys,
        /// <summary>No se encontró la clave bis_key_00 en prod.keys.</summary>
        ClaveNoEncontrada,
        /// <summary>BISKEYS.bin está truncado o dañado.</summary>
        ArchivoInvalido,
        /// <summary>Error de I/O al leer alguno de los archivos.</summary>
        ErrorLectura,
    }

    /// <summary>Resultado de la operación de copia.</summary>
    public class ResultadoRespaldoLlaves
    {
        public bool          Exito            { get; set; }
        public string?       RutaDestino      { get; set; }
        public List<string>  ArchivosCopiados { get; set; } = new();
        public List<string>  Errores          { get; set; } = new();

        public static ResultadoRespaldoLlaves Error(string mensaje) =>
            new() { Exito = false, Errores = new List<string> { mensaje } };
    }

    /// <summary>Resultado de la operación de restauración post-formato.</summary>
    public class ResultadoRestauracionLlaves
    {
        public bool         Exito             { get; set; }
        public string       Serial            { get; set; } = string.Empty;
        public bool         Omitida           { get; set; }
        public string?      MotivoOmision     { get; set; }
        public List<string> ArchivosRestaurados { get; set; } = new();
        public List<string> Errores           { get; set; } = new();
    }

    /// <summary>
    /// Resultado de comparar el número de entradas de dos archivos prod.keys.
    ///
    /// <para>El número de entradas es el indicador canónico de "versión" de un
    /// archivo prod.keys: cada actualización de firmware añade líneas nuevas, por
    /// lo que más entradas siempre significa llaves más recientes/valiosas, sin
    /// importar fecha de archivo ni tamaño en bytes.</para>
    /// </summary>
    public class ComparacionProdkeys
    {
        /// <summary>Entradas válidas en la prod.keys de la microSD.</summary>
        public int  EntradasSD             { get; set; }

        /// <summary>Entradas válidas en la prod.keys del respaldo local.</summary>
        public int  EntradasLocal          { get; set; }

        /// <summary>La SD tiene más entradas que el respaldo ? el respaldo está desactualizado.</summary>
        public bool SDTieneMasEntradas     { get; set; }

        /// <summary>El respaldo local tiene más entradas ? la SD está desactualizada.</summary>
        public bool LocalTieneMasEntradas  { get; set; }

        /// <summary>Ambas fuentes tienen el mismo número de entradas.</summary>
        public bool SonIguales             { get; set; }

        /// <summary>El archivo prod.keys del respaldo local existe en disco.</summary>
        public bool RutaLocalExiste        { get; set; }

        /// <summary>Resumen legible para mostrar en UI o log.</summary>
        public string Resumen =>
            !RutaLocalExiste
                ? "Sin respaldo local de prod.keys"
                : SonIguales
                    ? $"prod.keys iguales ({EntradasSD} entradas)"
                    : SDTieneMasEntradas
                        ? $"SD más completa: {EntradasSD} entradas vs {EntradasLocal} local ? se actualizará el respaldo"
                        : $"Respaldo local más completo: {EntradasLocal} entradas vs {EntradasSD} SD ? se restaurará la local";
    }

    /// <summary>
    /// Representa un respaldo de llaves guardado en el PC (una carpeta por serial).
    /// </summary>
    public class RespaldoLocal
    {
        public string   Serial            { get; set; } = string.Empty;
        public string   RutaCarpeta       { get; set; } = string.Empty;
        public bool     HayBiskeys        { get; set; }
        public bool     HayProdinfo       { get; set; }
        public bool     HayProdkeys       { get; set; }
        public bool     HayCertificado    { get; set; }
        public int      EntradasProdkeys  { get; set; }
        public DateTime FechaRespaldo     { get; set; }

        public string FechaFormateada =>
            FechaRespaldo == DateTime.MinValue ? "—" : FechaRespaldo.ToString("yyyy-MM-dd HH:mm");

        public string ResumenArchivos
        {
            get
            {
                var partes = new List<string>();
                if (HayBiskeys)  partes.Add("BISKEYS");
                if (HayProdinfo) partes.Add("PRODINFO");
                if (HayProdkeys) partes.Add($"prod.keys ({EntradasProdkeys} llaves)");
                return partes.Count > 0 ? string.Join("  ·  ", partes) : "sin archivos";
            }
        }
    }
}
