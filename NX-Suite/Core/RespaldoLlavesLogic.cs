using NX_Swite.Core.Configuracion;
using NX_Swite.Services;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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

                // Info de master key máxima
                if (resultado.HayProdkeys && !string.IsNullOrEmpty(resultado.RutaProdkeys))
                    resultado.InfoMasterKey = AnalizarMasterKeys(resultado.RutaProdkeys);

                // Ruta destino sugerida
                if (!string.IsNullOrEmpty(resultado.Serial))
                    resultado.RutaDestino = Path.Combine(
                        ConfiguracionLocal.RutaRespaldosLlaves, resultado.Serial);

                // Modelo y región a partir del serial
                var entradaModelo = ModeloSwitchTable.Resolver(resultado.Serial);
                resultado.Modelo  = entradaModelo?.Modelo;
                resultado.Region  = entradaModelo?.Region;
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
                            // SD más completa: actualizar respaldo directamente
                            File.Copy(analisis.RutaProdkeys, rutaLocalProdkeys, overwrite: true);
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
                        File.Copy(analisis.RutaBiskeys, rutaLocal, overwrite: true);
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
                        File.Copy(analisis.RutaProdinfo, rutaLocal, overwrite: true);
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

                var entradaModelo    = ModeloSwitchTable.Resolver(serial);
                item.Modelo          = entradaModelo?.Modelo;
                item.Region          = entradaModelo?.Region;
                item.HayCertificado = File.Exists(cert) || File.Exists(Path.ChangeExtension(cert, ".png"));
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
            if (!string.IsNullOrEmpty(analisis.Modelo))
                sb.AppendLine($"  Modelo           : {analisis.Modelo}");
            if (!string.IsNullOrEmpty(analisis.Region))
                sb.AppendLine($"  Región           : {analisis.Region}");
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

            // Sección master key / compatibilidad de firmware
            if (analisis.InfoMasterKey is { } mk)
            {
                sb.AppendLine($"  Master key máxima          : {mk.MasterKeyMaxima}");
                sb.AppendLine($"  Compatibilidad confirmada  : HOS {mk.RangoHosCompatible}");
                string integridadTxt = mk.IntegridadGeneraciones
                    ? "OK - Completa"
                    : mk.ClavesFaltantes > 0
                        ? $"AVISO - Faltan {mk.ClavesFaltantes} claves hasta {mk.MaximaConocida}"
                        : "AVISO - Faltan claves intermedias";
                sb.AppendLine($"  Integridad de generaciones : {integridadTxt}");
                sb.AppendLine($"  Atmosphere desde           : v{mk.AtmosphereDesde}");
            }
            else if (analisis.HayProdkeys)
            {
                sb.AppendLine("  Master key máxima          : no determinada");
            }
            sb.AppendLine();

            // Estado de verificación criptográfica
            sb.AppendLine($"  Verificación criptográfica : {analisis.EstadoVerificacion}");
            if (!string.IsNullOrEmpty(analisis.DetalleVerificacion))
                sb.AppendLine($"    Detalle : {analisis.DetalleVerificacion}");

            sb.AppendLine();
            sb.AppendLine("  !  ESTE ARCHIVO ES SOLO INFORMATIVO.");
            sb.AppendLine("     Las llaves de consola son únicas e intransferibles.");
            sb.AppendLine("     Mezclar llaves de consolas distintas provoca daño permanente.");
            sb.AppendLine("=============================================================");

            string ruta = Path.Combine(analisis.RutaDestino, "certificado.txt");
            try { File.WriteAllText(ruta, sb.ToString(), Encoding.UTF8); }
            catch { }
            return ruta;
        }


        /// <summary>
        /// Genera el archivo <c>certificado.png</c> junto al <c>certificado.txt</c>,
        /// superponiendo los datos del respaldo sobre la plantilla visual.
        /// </summary>
        /// <returns>Ruta del PNG generado, o <see cref="string.Empty"/> si falla.</returns>
        public static string GenerarCertificadoPng(AnalisisRespaldoLlaves analisis)
        {
            if (string.IsNullOrEmpty(analisis.RutaDestino) || string.IsNullOrEmpty(analisis.Serial))
                return string.Empty;

            try
            {
                // Cargar plantilla embebida
                using var stream = ObtenerStreamPlantilla();
                if (stream == null) return string.Empty;

                using var img = SixLabors.ImageSharp.Image.Load(stream);

                // Construir colección de fuentes del sistema
                var fc = new FontCollection();
                FontFamily fontFamily;
                try
                {
                    // Intentar cargar Segoe UI (presente en Windows)
                    fontFamily = SystemFonts.Get("Segoe UI");
                }
                catch
                {
                    // Fallback a cualquier fuente del sistema
                    fontFamily = SystemFonts.Collection.Families.FirstOrDefault();
                }

                var fontNormal = fontFamily.CreateFont(22, FontStyle.Regular);
                var fontMono   = fontFamily.CreateFont(19, FontStyle.Regular);
                var colorText  = SixLabors.ImageSharp.Color.FromRgb(30, 30, 45);
                var colorMono  = SixLabors.ImageSharp.Color.FromRgb(15, 15, 30);
                var colorNota  = SixLabors.ImageSharp.Color.FromRgb(100, 70, 10);

                img.Mutate(ctx =>
                {
                    var optNormal = new RichTextOptions(fontNormal) { HorizontalAlignment = HorizontalAlignment.Left };
                    var optMono   = new RichTextOptions(fontMono)   { HorizontalAlignment = HorizontalAlignment.Left };

                    // — Generado por / Fecha —
                    DrawAt(ctx, optNormal, colorText,
                        $"NX-Swite v{ConfiguracionLocal.VersionActual}",
                        CertificadoLayout.XGeneradoPorValor, CertificadoLayout.YGeneradoPor);
                    DrawAt(ctx, optNormal, colorText,
                        DateTime.Now.ToString("yyyy-MM-dd  HH:mm:ss"),
                        CertificadoLayout.XFechaValor, CertificadoLayout.YFecha);

                    // — Número de serie (+ modelo y región en la misma línea) —
                    string serialLinea = analisis.Serial ?? string.Empty;
                    if (!string.IsNullOrEmpty(analisis.Modelo))
                        serialLinea += $"   ·   {analisis.Modelo}";
                    if (!string.IsNullOrEmpty(analisis.Region))
                        serialLinea += $"   ·   {analisis.Region}";
                    DrawAt(ctx, optNormal, colorText,
                        serialLinea,
                        CertificadoLayout.XSerialValor, CertificadoLayout.YSerial);

                    // — BISKEYS: hex de cada bis_key debajo de la etiqueta de la plantilla —
                    if (analisis.HayBiskeys && File.Exists(analisis.RutaBiskeys))
                    {
                        try
                        {
                            byte[] bytes = File.ReadAllBytes(analisis.RutaBiskeys!);
                            int[] yRows = [
                                CertificadoLayout.YBiskey0,
                                CertificadoLayout.YBiskey1,
                                CertificadoLayout.YBiskey2,
                                CertificadoLayout.YBiskey3
                            ];
                            for (int i = 0; i < 4 && (i + 1) * 32 <= bytes.Length; i++)
                            {
                                string hex = BitConverter.ToString(bytes, i * 32, 32)
                                                         .Replace("-", "").ToLowerInvariant();
                                DrawAt(ctx, optMono, colorMono,
                                    $"bis_key_0{i} = {hex}",
                                    CertificadoLayout.XBiskeyHexValor, yRows[i]);
                            }
                        }
                        catch { /* no bloquear */ }
                    }
                    else
                    {
                        DrawAt(ctx, optNormal, colorText, "no encontrado",
                            CertificadoLayout.XBiskeyHexValor, CertificadoLayout.YBiskey0);
                    }

                    // — prod.keys —
                    string prodVal = analisis.HayProdkeys && File.Exists(analisis.RutaProdkeys)
                        ? $"{ContarEntradasProdkeys(analisis.RutaProdkeys!)} entradas válidas"
                        : "no encontrado";
                    DrawAt(ctx, optNormal, colorText, prodVal,
                        CertificadoLayout.XProdkeysValor, CertificadoLayout.YProdkeys);

                    // — Master key máxima / compatibilidad / integridad —
                    if (analisis.InfoMasterKey is { } mk)
                    {
                        DrawAt(ctx, optNormal, colorText, mk.MasterKeyMaxima,
                            CertificadoLayout.XMasterKeyValor, CertificadoLayout.YMasterKey);

                        DrawAt(ctx, optNormal, colorText,
                            $"HOS {mk.RangoHosCompatible}  ·  Atmosphere ≥ v{mk.AtmosphereDesde}",
                            CertificadoLayout.XCompatibilidadValor, CertificadoLayout.YCompatibilidad);

                        string integridadTxt = mk.IntegridadGeneraciones
                            ? "Completa — sin huecos"
                            : mk.ClavesFaltantes > 0
                                ? $"Incompleta — faltan {mk.ClavesFaltantes} claves hasta {mk.MaximaConocida}"
                                : "Incompleta — faltan claves intermedias";
                        var colorIntegridad = mk.IntegridadGeneraciones
                            ? SixLabors.ImageSharp.Color.FromRgb(10, 100, 30)
                            : SixLabors.ImageSharp.Color.FromRgb(160, 80, 0);
                        DrawAt(ctx, optNormal, colorIntegridad, integridadTxt,
                            CertificadoLayout.XIntegridadValor, CertificadoLayout.YIntegridad);
                    }
                    else if (analisis.HayProdkeys)
                    {
                        DrawAt(ctx, optNormal, colorText, "no determinada",
                            CertificadoLayout.XMasterKeyValor, CertificadoLayout.YMasterKey);
                    }

                    // — Verificación criptográfica —
                    string verfVal = analisis.EstadoVerificacion.ToString();
                    if (!string.IsNullOrEmpty(analisis.DetalleVerificacion))
                        verfVal += $"  —  {analisis.DetalleVerificacion}";
                    DrawAt(ctx, optNormal, colorText, verfVal,
                        CertificadoLayout.XVerificacionValor, CertificadoLayout.YVerificacion);

                    // — NOTAS (texto del Gist, máx 4 líneas, wrapping manual) —
                    string notaRaw = ConfiguracionRemota.Ui.NotaCertificado;
                    if (string.IsNullOrWhiteSpace(notaRaw))
                        notaRaw = "Es ilegal distribuir estas llaves. Son para uso personal y no son transferibles.\nLas llaves de consola son únicas e intransferibles entre consolas.\nMezclar llaves de consolas distintas provoca daño permanente.";
                    // Normalizar "\n" literal (tal como se escribe en el Gist) a salto de línea real
                    notaRaw = notaRaw.Replace("\\n", "\n");

                    var lineas = PartirEnLineas(notaRaw, fontMono,
                        CertificadoLayout.XNotasMax - CertificadoLayout.XNotasValor, maxLineas: 4);
                    for (int li = 0; li < lineas.Count; li++)
                        DrawAt(ctx, optMono, colorNota, lineas[li],
                            CertificadoLayout.XNotasValor,
                            CertificadoLayout.YNotasLinea1 + li * CertificadoLayout.YNotasLineaH);
                });

                string rutaPng = Path.Combine(analisis.RutaDestino, "certificado.png");
                img.SaveAsPng(rutaPng);
                return rutaPng;
            }
            catch
            {
                return string.Empty;
            }
        }

        // Dibuja texto en una posición absoluta dentro del contexto de la imagen.
        private static void DrawAt(IImageProcessingContext ctx, RichTextOptions baseOpt,
            SixLabors.ImageSharp.Color color, string texto, float x, float y)
        {
            var opt = new RichTextOptions(baseOpt.Font)
            {
                Origin                = new System.Numerics.Vector2(x, y),
                HorizontalAlignment   = baseOpt.HorizontalAlignment,
            };
            ctx.DrawText(opt, texto, color);
        }

        // Parte el texto en líneas sin superar el ancho máximo en píxeles, respetando saltos de línea.
        private static List<string> PartirEnLineas(string texto, Font font, float anchoMax, int maxLineas)
        {
            var resultado = new List<string>();
            foreach (string parrafo in texto.Split('\n'))
            {
                string[] palabras = parrafo.Split(' ');
                var linea = new StringBuilder();
                foreach (string palabra in palabras)
                {
                    string candidato = linea.Length == 0 ? palabra : linea + " " + palabra;
                    var medidas = TextMeasurer.MeasureSize(candidato, new TextOptions(font));
                    if (medidas.Width > anchoMax && linea.Length > 0)
                    {
                        resultado.Add(linea.ToString());
                        if (resultado.Count >= maxLineas) return resultado;
                        linea.Clear();
                        linea.Append(palabra);
                    }
                    else
                    {
                        linea.Clear();
                        linea.Append(candidato);
                    }
                }
                if (linea.Length > 0)
                {
                    resultado.Add(linea.ToString());
                    if (resultado.Count >= maxLineas) return resultado;
                }
            }
            return resultado;
        }

        // Devuelve el stream de la plantilla embebida en el ensamblado.
        private static Stream? ObtenerStreamPlantilla()
        {
            var asm = Assembly.GetExecutingAssembly();
            // El nombre del recurso sigue el patrón: <AssemblyName>.<ruta con puntos>
            string nombre = "NX_Swite.Assets.certificado_plantilla.png";
            return asm.GetManifestResourceStream(nombre);
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
            int timeoutMs = 15_000,
            bool forzar = false)
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
                            if (!forzar)
                            {
                                // Detectar el serial de la consola en la SD para informar al usuario
                                string serialEnSD = Path.GetFileNameWithoutExtension(biskeysEnSD)
                                    .Replace(SufijoBiskeys.TrimStart('_').Replace(".bin", ""), "",
                                        StringComparison.OrdinalIgnoreCase)
                                    .Trim('_');
                                // Fallback: extraer prefijo antes del sufijo conocido
                                string nombreArchivo = Path.GetFileNameWithoutExtension(biskeysEnSD);
                                if (nombreArchivo.EndsWith("_BISKEYS", StringComparison.OrdinalIgnoreCase))
                                    serialEnSD = nombreArchivo[..^8]; // quitar "_BISKEYS"

                                resultado.DiscrepanciaSerial = true;
                                resultado.SerialEnSD         = serialEnSD;
                                resultado.MotivoOmision      =
                                    $"La SD contiene llaves de otra consola ({serialEnSD}). " +
                                    "Confirma para sobrescribir.";
                                return resultado;
                            }
                            // forzar = true: el usuario confirmó conscientemente — continuar
                            Logger.Warning(
                                $"[RestaurarForzado] Serial respaldo={respaldo.Serial}, " +
                                $"serial SD distinto. El usuario confirmó la restauración.");
                        }
                }
            }

            Logger.RestauracionLlavesIniciada(resultado.Serial, letraSD);

            var archivosRestaurados = new List<string>();
            var errores             = new List<string>();
            var omitidos            = new List<string>();

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

                // prod.keys (con guard: nunca sobreescribir con una versión inferior)
                string prodkeys = Path.Combine(respaldo.RutaCarpeta, "prod.keys");
                if (File.Exists(prodkeys))
                    RestaurarProdkeysConGuard(prodkeys, carpetaSwitchSD,
                                              archivosRestaurados, errores, omitidos);
            });

            if (omitidos.Count > 0)
                Logger.Info($"[RestaurarDesdeRespaldoLocal] {string.Join("; ", omitidos)}");

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
                var omitidos            = new List<string>();

                await Task.Run(() =>
                {
                    // 1) BISKEYS.bin → atmosphere/automatic_backups/
                    if (analisis.HayBiskeys && analisis.RutaBiskeys != null)
                    {
                        string nombreBiskeys  = Path.GetFileName(analisis.RutaBiskeys);
                        string origenBiskeys  = Path.Combine(analisis.RutaDestino!, nombreBiskeys);
                        string carpetaBackups = Path.Combine(letraSD, CarpetaAtmosBackups);
                        RestaurarArchivo(origenBiskeys, carpetaBackups,
                                         archivosRestaurados, errores);
                    }

                    // 2) PRODINFO.bin → atmosphere/automatic_backups/
                    if (analisis.HayProdinfo && analisis.RutaProdinfo != null)
                    {
                        string nombreProdinfo = Path.GetFileName(analisis.RutaProdinfo);
                        string origenProdinfo = Path.Combine(analisis.RutaDestino!, nombreProdinfo);
                        string carpetaBackups = Path.Combine(letraSD, CarpetaAtmosBackups);
                        RestaurarArchivo(origenProdinfo, carpetaBackups,
                                         archivosRestaurados, errores);
                    }

                    // 3) prod.keys → switch/  (con guard: nunca sobreescribir con una versión inferior)
                    if (analisis.HayProdkeys)
                    {
                        string origenProdkeys = Path.Combine(analisis.RutaDestino!, "prod.keys");
                        string carpetaSwitch  = Path.Combine(letraSD, "switch");
                        RestaurarProdkeysConGuard(origenProdkeys, carpetaSwitch,
                                                  archivosRestaurados, errores, omitidos);
                    }
                });

                if (omitidos.Count > 0)
                    Logger.Info($"[RestaurarAsync] {string.Join("; ", omitidos)}");

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
                    // No hay respaldo de ese archivo específico → no es un error
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
        /// Restaura <c>prod.keys</c> desde el respaldo al destino solo si el respaldo
        /// tiene MÁS entradas que el archivo ya existente en destino.
        /// Si el destino tiene igual o más entradas, se omite para proteger las llaves
        /// más recientes y se registra en <paramref name="omitidos"/>.
        /// </summary>
        private static void RestaurarProdkeysConGuard(
            string rutaOrigen,
            string carpetaDestino,
            List<string> restaurados,
            List<string> errores,
            List<string> omitidos)
        {
            try
            {
                if (!File.Exists(rutaOrigen)) return;

                Directory.CreateDirectory(carpetaDestino);
                string rutaDestino = Path.Combine(carpetaDestino, "prod.keys");

                if (File.Exists(rutaDestino))
                {
                    int entradasOrigen  = ContarEntradasProdkeys(rutaOrigen);
                    int entradasDestino = ContarEntradasProdkeys(rutaDestino);

                    if (entradasDestino >= entradasOrigen)
                    {
                        // El destino ya tiene llaves iguales o más recientes → no sobreescribir
                        omitidos.Add(
                            $"prod.keys omitido: destino tiene {entradasDestino} entradas " +
                            $"vs {entradasOrigen} del respaldo — se conserva el más completo.");
                        return;
                    }
                }

                File.Copy(rutaOrigen, rutaDestino, overwrite: true);
                restaurados.Add("prod.keys");
            }
            catch (Exception ex)
            {
                errores.Add($"prod.keys: {ex.Message}");
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

                // ?? Guard de downgrade de prod.keys ??????????????????????????????
                // Si ya existe un respaldo local con prod.keys, comparar el número de
                // entradas. Si el local tiene más entradas que el de la SD, se está
                // intentando sobreescribir un respaldo superior con uno inferior (downgrade).
                // Esto se bloquea silenciosamente para proteger el respaldo más completo.
                if (analisis.HayProdkeys && analisis.RutaProdkeys != null &&
                    Directory.Exists(analisis.RutaDestino))
                {
                    string rutaLocalProdkeys = Path.Combine(analisis.RutaDestino, "prod.keys");
                    if (File.Exists(rutaLocalProdkeys))
                    {
                        int entradasSD    = ContarEntradasProdkeys(analisis.RutaProdkeys);
                        int entradasLocal = ContarEntradasProdkeys(rutaLocalProdkeys);
                        if (entradasSD < entradasLocal)
                        {
                            string motivo =
                                $"Respaldo bloqueado: el archivo de la SD tiene {entradasSD} entradas " +
                                $"pero el respaldo existente tiene {entradasLocal}. " +
                                "No se puede sobreescribir un respaldo superior con uno inferior.";
                            Logger.Warning($"[RespaldoLlaves] {motivo}");
                            return ResultadoRespaldoLlaves.Bloquear(motivo);
                        }
                    }
                }

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

                // Refrescar InfoMasterKey desde la copia local para que el certificado
                // refleje los archivos realmente respaldados, no el estado del análisis previo.
                if (resultado.Exito && analisis.RutaDestino != null)
                {
                    string localProdkeys = Path.Combine(analisis.RutaDestino, "prod.keys");
                    if (File.Exists(localProdkeys))
                        analisis.InfoMasterKey = AnalizarMasterKeys(localProdkeys);
                }

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

        // ?? Master key analysis ??????????????????????????????????????????????

        /// <summary>
        /// Escanea el archivo prod.keys y devuelve información sobre la master key máxima
        /// y la integridad de la cadena de generaciones.
        /// </summary>
        private static InfoMasterKey? AnalizarMasterKeys(string rutaProdkeys)
        {
            try
            {
                // Recopilar todas las master_key_XX presentes
                var keysEncontradas = new System.Collections.Generic.HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

                foreach (string linea in File.ReadLines(rutaProdkeys, Encoding.UTF8))
                {
                    string t = linea.Trim();
                    int idx  = t.IndexOf('=');
                    if (idx <= 0) continue;
                    string nombre = t[..idx].Trim();
                    if (nombre.StartsWith("master_key_", StringComparison.OrdinalIgnoreCase))
                        keysEncontradas.Add(nombre.ToLowerInvariant());
                }

                if (keysEncontradas.Count == 0) return null;

                // Determinar la máxima (mayor índice numérico de las que están en la tabla)
                MasterKeyTable.Entry? maxEntry = null;
                foreach (string k in keysEncontradas)
                {
                    var e = MasterKeyTable.Buscar(k);
                    if (e == null) continue;
                    if (maxEntry == null ||
                        string.Compare(e.MasterKey, maxEntry.MasterKey,
                            StringComparison.OrdinalIgnoreCase) > 0)
                        maxEntry = e;
                }

                if (maxEntry == null) return null;

                // Clave máxima conocida en la tabla (referencia global)
                MasterKeyTable.Entry? maxTabla = null;
                for (int t = MasterKeyTable.Total - 1; t >= 0; t--)
                {
                    // Recorrer desde el índice más alto hasta encontrar una entrada válida
                    string candidatoTabla = $"master_key_{t:x2}";
                    var et = MasterKeyTable.Buscar(candidatoTabla);
                    if (et != null) { maxTabla = et; break; }
                }
                // Si no encontramos la máxima de la tabla, usamos la máxima del archivo
                if (maxTabla == null) maxTabla = maxEntry;

                // Índice numérico de la máxima del archivo y de la tabla
                string sufArchivo = maxEntry.MasterKey["master_key_".Length..];
                string sufTabla   = maxTabla.MasterKey["master_key_".Length..];

                int.TryParse(sufArchivo, System.Globalization.NumberStyles.HexNumber, null, out int idxArchivo);
                int.TryParse(sufTabla,   System.Globalization.NumberStyles.HexNumber, null, out int idxTabla);

                // Verificar que no haya huecos desde 00 hasta la máxima DEL ARCHIVO
                bool sinHuecos = true;
                for (int i = 0; i <= idxArchivo; i++)
                {
                    if (!keysEncontradas.Contains($"master_key_{i:x2}"))
                    {
                        sinHuecos = false;
                        break;
                    }
                }

                // La cadena es íntegra solo si no hay huecos Y el archivo llega hasta la máxima conocida
                bool integra       = sinHuecos && (idxArchivo >= idxTabla);
                int  clavesFaltantes = integra ? 0 : Math.Max(0, idxTabla - idxArchivo);

                return new InfoMasterKey
                {
                    MasterKeyMaxima        = maxEntry.MasterKey,
                    RangoHosCompatible     = maxEntry.RangoHosCompatible,
                    AtmosphereDesde        = maxEntry.AtmosphereDesde,
                    IntegridadGeneraciones = integra,
                    MaximaConocida         = maxTabla.MasterKey,
                    ClavesFaltantes        = clavesFaltantes,
                    TotalMasterKeys        = keysEncontradas.Count,
                };
            }
            catch { return null; }
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
                    // Tamaño distinto → el guard externo ya garantizó que este es mejor; sobrescribir.
                }

                File.Copy(rutaOrigen, rutaDestino, overwrite: true);
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
        /// <summary>Modelo detectado (p.ej. "Nintendo Switch OLED"). <c>null</c> si no se reconoce el serial.</summary>
        public string? Modelo                  { get; set; }
        /// <summary>Región detectada (p.ej. "América"). <c>null</c> si no se reconoce el serial.</summary>
        public string? Region                  { get; set; }

        public EstadoVerificacionLlaves EstadoVerificacion  { get; set; } =
            EstadoVerificacionLlaves.NoRealizada;
        public string? DetalleVerificacion { get; set; }

        /// <summary>
        /// Información de compatibilidad derivada de la master key máxima encontrada en prod.keys.
        /// <c>null</c> si no hay prod.keys o no se pudo determinar.
        /// </summary>
        public InfoMasterKey? InfoMasterKey { get; set; }

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

        /// <summary>
        /// <c>true</c> cuando el respaldo fue bloqueado por política (ej. intento de downgrade).
        /// En este caso <see cref="Exito"/> es <c>false</c> y <see cref="MotivoBloqueado"/>
        /// contiene el mensaje legible para mostrar en la UI.
        /// </summary>
        public bool    Bloqueado       { get; set; }
        public string? MotivoBloqueado { get; set; }

        public static ResultadoRespaldoLlaves Error(string mensaje) =>
            new() { Exito = false, Errores = new List<string> { mensaje } };

        public static ResultadoRespaldoLlaves Bloquear(string motivo) =>
            new() { Exito = false, Bloqueado = true, MotivoBloqueado = motivo };
    }

    /// <summary>Resultado de la operación de restauración post-formato.</summary>
    public class ResultadoRestauracionLlaves
    {
        public bool         Exito               { get; set; }
        public string       Serial              { get; set; } = string.Empty;
        public bool         Omitida             { get; set; }
        public string?      MotivoOmision       { get; set; }
        /// <summary>
        /// <c>true</c> cuando el respaldo seleccionado pertenece a una consola distinta
        /// a la que está en la SD. La UI debe pedir confirmación explícita al usuario
        /// y rellamar con <c>forzar = true</c> si acepta.
        /// </summary>
        public bool         DiscrepanciaSerial  { get; set; }
        /// <summary>Serial de las llaves actualmente en la SD (consola distinta).</summary>
        public string?      SerialEnSD          { get; set; }
        public List<string> ArchivosRestaurados { get; set; } = new();
        public List<string> Errores             { get; set; } = new();
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
    /// Información derivada de la master key máxima detectada en prod.keys.
    /// Indica la generación criptográfica y el rango de firmware compatible.
    /// </summary>
    public class InfoMasterKey
    {
        /// <summary>Nombre de la master key más alta encontrada (p.ej. <c>master_key_15</c>).</summary>
        public string MasterKeyMaxima       { get; init; } = string.Empty;
        /// <summary>Rango de versiones HOS compatibles con esta generación criptográfica.</summary>
        public string RangoHosCompatible    { get; init; } = string.Empty;
        /// <summary>Primera versión de Atmosphere que soportó este firmware.</summary>
        public string AtmosphereDesde       { get; init; } = string.Empty;
        /// <summary>
        /// <c>true</c> solo si todas las master keys desde _00 hasta la máxima conocida
        /// en la tabla están presentes sin huecos. Si el archivo tiene claves hasta _05
        /// pero la tabla llega hasta _15, será <c>false</c>.
        /// </summary>
        public bool IntegridadGeneraciones  { get; init; }
        /// <summary>Clave máxima conocida en la tabla en el momento del análisis.</summary>
        public string MaximaConocida        { get; init; } = string.Empty;
        /// <summary>Número de master keys que faltan hasta la máxima conocida.</summary>
        public int   ClavesFaltantes        { get; init; }
        /// <summary>Número total de master keys encontradas en prod.keys.</summary>
        public int   TotalMasterKeys        { get; init; }
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
        /// <summary>Modelo detectado (p.ej. "Nintendo Switch OLED"). <c>null</c> si no se reconoce el serial.</summary>
        public string?  Modelo            { get; set; }
        /// <summary>Región detectada (p.ej. "América"). <c>null</c> si no se reconoce el serial.</summary>
        public string?  Region            { get; set; }

        public string FechaFormateada =>
            FechaRespaldo == DateTime.MinValue ? "—" : FechaRespaldo.ToString("yyyy-MM-dd HH:mm");

        /// <summary>Resumen de modelo y región para mostrar en la tarjeta UI.</summary>
        public string ModeloRegionResumen
        {
            get
            {
                if (!string.IsNullOrEmpty(Modelo) && !string.IsNullOrEmpty(Region))
                    return $"{Modelo}  ·  {Region}";
                return Modelo ?? Region ?? string.Empty;
            }
        }

        /// <summary><c>true</c> si se detectó al menos el modelo o la región.</summary>
        public bool TieneModelo => !string.IsNullOrEmpty(Modelo) || !string.IsNullOrEmpty(Region);

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
