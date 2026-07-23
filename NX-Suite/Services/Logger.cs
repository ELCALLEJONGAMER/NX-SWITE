using System;
using System.IO;
using System.Management;
using System.Text;
using NX_Swite.Core.Configuracion;

namespace NX_Swite.Services
{
    /// <summary>
    /// Logger de sesiones. Guarda las últimas <see cref="MaxSesiones"/> sesiones en
    /// %AppData%\NX-Swite\NX-Swite.log. Cada arranque de la app abre una sesión nueva
    /// con una cabecera clara que incluye versión de la app y de Windows.
    /// Thread-safe mediante lock.
    /// </summary>
    public static class Logger
    {
        // ── Configuración ────────────────────────────────────────────────
        private const int MaxSesiones = 5;
        private const string Separador = "════════════════════════════════════════════════════════";

        private static readonly string _logFilePath = ConfiguracionLocal.RutaLog;
        private static readonly object _lock = new();

        // ── Inicio de sesión ─────────────────────────────────────────────

        /// <summary>
        /// Abre una nueva sesión en el log. Debe llamarse una vez al arrancar la app.
        /// Elimina las sesiones más antiguas si se supera <see cref="MaxSesiones"/>.
        /// </summary>
        public static void IniciarSesion()
        {
            try
            {
                Directory.CreateDirectory(ConfiguracionLocal.RutaAppData);
                string contenidoActual = File.Exists(_logFilePath)
                    ? File.ReadAllText(_logFilePath, Encoding.UTF8)
                    : string.Empty;

                string contenidoRecortado = RecortarSesiones(contenidoActual, MaxSesiones - 1);

                string cabecera = BuildCabecera();
                string nuevoContenido = string.IsNullOrWhiteSpace(contenidoRecortado)
                    ? cabecera
                    : contenidoRecortado + Environment.NewLine + cabecera;

                lock (_lock)
                    File.WriteAllText(_logFilePath, nuevoContenido, Encoding.UTF8);
            }
            catch { }
        }

        // ── Métodos genéricos ────────────────────────────────────────────

        public static void Info(string mensaje) => Escribir("INFO ", mensaje);
        public static void Warning(string mensaje) => Escribir("WARN ", mensaje);
        public static void Error(string mensaje, Exception? ex = null)
        {
            string detalle = ex == null ? mensaje
                : $"{mensaje} | {ex.GetType().Name}: {ex.Message}";
            Escribir("ERROR", detalle);
        }

        // ── Métodos semánticos: Descarga ─────────────────────────────────

        public static void DescargaIniciada(string modulo, string url)
            => Escribir("INFO ", $"[{modulo}] Descarga iniciada → {url}");

        public static void DescargaCompletada(string modulo, long bytes)
            => Escribir("OK   ", $"[{modulo}] Descarga completada → {FormatBytes(bytes)}");

        public static void DescargaOmitida(string modulo, string archivo)
            => Escribir("INFO ", $"[{modulo}] Descarga omitida (caché válida) → {archivo}");

        public static void DescargaFallida(string modulo, string url, Exception ex)
            => Escribir("ERROR", $"[{modulo}] Descarga fallida → {ex.GetType().Name}: {ex.Message} | URL: {url}");

        // ── Métodos semánticos: Descompresión ────────────────────────────

        public static void ExtraccionIniciada(string modulo, string archivoZip)
            => Escribir("INFO ", $"[{modulo}] Extracción iniciada → {archivoZip}");

        public static void ExtraccionCompletada(string modulo, int archivos)
            => Escribir("OK   ", $"[{modulo}] Extracción completada → {archivos} archivo(s)");

        public static void ExtraccionOmitida(string modulo, string carpeta)
            => Escribir("INFO ", $"[{modulo}] Extracción omitida (ya extraído) → {carpeta}");

        public static void ExtraccionFallida(string modulo, string archivoZip, Exception ex)
            => Escribir("ERROR", $"[{modulo}] Extracción fallida → {ex.GetType().Name}: {ex.Message} | Archivo: {archivoZip}");

        // ── Métodos semánticos: Copiado a SD ─────────────────────────────

        public static void CopiadoIniciado(string modulo, string destino)
            => Escribir("INFO ", $"[{modulo}] Copiado iniciado → {destino}");

        public static void CopiadoCompletado(string modulo, int archivos)
            => Escribir("OK   ", $"[{modulo}] Copiado completado → {archivos} archivo(s)");

        public static void CopiadoFallido(string modulo, Exception ex)
            => Escribir("ERROR", $"[{modulo}] Copiado fallido → {ex.GetType().Name}: {ex.Message}");

        // ── Métodos semánticos: Pipeline general ─────────────────────────

        public static void InstalacionIniciada(string modulo, string version, string letraSD)
            => Escribir("INFO ", $"[{modulo} v{version}] Instalación iniciada → SD: {letraSD}");

        public static void InstalacionCompletada(string modulo, string version)
            => Escribir("OK   ", $"[{modulo} v{version}] Instalación completada con éxito");

        public static void InstalacionFallida(string modulo, string version, string error)
            => Escribir("ERROR", $"[{modulo} v{version}] Instalación fallida → {error}");

        public static void InstalacionCancelada(string modulo, string version)
            => Escribir("WARN ", $"[{modulo} v{version}] Instalación cancelada por el usuario");

        // ── Métodos semánticos: Formateo y particionado ──────────────────

        public static void FormateoIniciado(string letraSD, string modo, string etiqueta)
            => Escribir("INFO ", $"[Formateo] Iniciado → SD: {letraSD} | Modo: {modo} | Etiqueta: {etiqueta}");

        public static void FormateoCompletado(string letraSD)
            => Escribir("OK   ", $"[Formateo] Completado con éxito → SD: {letraSD}");

        public static void FormateoFallido(string letraSD, Exception ex)
            => Escribir("ERROR", $"[Formateo] Fallido → SD: {letraSD} | {ex.GetType().Name}: {ex.Message}");

        public static void ParticionadoIniciado(string letraSD, string modo, int emuMB)
        {
            string detalle = emuMB > 0 ? $"emuMMC {emuMB} MB + SWITCH SD" : "FAT32 simple";
            Escribir("INFO ", $"[Particionado] Iniciado → SD: {letraSD} | Modo: {modo} | {detalle}");
        }

        public static void ParticionadoCompletado(string letraSD)
            => Escribir("OK   ", $"[Particionado] Completado con éxito → SD: {letraSD}");

        public static void ParticionadoFallido(string letraSD, Exception ex)
            => Escribir("ERROR", $"[Particionado] Fallido → SD: {letraSD} | {ex.GetType().Name}: {ex.Message}");

        // ── Métodos semánticos: Hekate / Personalización ─────────────────

        public static void HekateIconAplicado(string archivoIni, string tipoIcono, int secciones)
            => Escribir("OK   ", $"[Hekate] Icono aplicado → {archivoIni} | Tipo: {tipoIcono} | {secciones} sección(es)");

        public static void HekateIconSinCambios(string archivoIni, string tipoIcono)
            => Escribir("INFO ", $"[Hekate] Sin secciones coincidentes para icono → {archivoIni} | Tipo: {tipoIcono}");

        public static void HekateValorEstablecido(string archivoIni, string seccion, string clave, string valor)
            => Escribir("INFO ", $"[Hekate] Valor establecido → {archivoIni} | [{seccion}] {clave}={valor}");

        public static void HekateArchivoNoEncontrado(string archivoIni)
            => Escribir("WARN ", $"[Hekate] Archivo no encontrado, paso omitido → {archivoIni}");

        // ── Métodos semánticos: Desinstalación de módulo ─────────────────

        public static void DesinstalacionIniciada(string modulo, string letraSD)
            => Escribir("INFO ", $"[{modulo}] Desinstalación iniciada → SD: {letraSD}");

        public static void DesinstalacionCompletada(string modulo)
            => Escribir("OK   ", $"[{modulo}] Desinstalación completada con éxito");

        public static void DesinstalacionFallida(string modulo)
            => Escribir("ERROR", $"[{modulo}] Desinstalación fallida");

        // ── Métodos semánticos: Caché ────────────────────────────────────

        public static void CacheModuloEliminado(string modulo)
            => Escribir("OK   ", $"[{modulo}] Caché local eliminada");

        public static void CacheModuloErrorAlEliminar(string modulo)
            => Escribir("ERROR", $"[{modulo}] Error al eliminar caché local");

        public static void CacheTotalEliminada()
            => Escribir("OK   ", "[Caché] Bóveda completa eliminada");

        // ── Métodos semánticos: Limpieza SD ──────────────────────────────

        public static void LimpiezaSDIniciada(string letraSD, int elementosABorrar)
            => Escribir("INFO ", $"[LimpiezaSD] Iniciada → SD: {letraSD} | {elementosABorrar} elemento(s) a eliminar");

        public static void LimpiezaSDCompletada(string letraSD)
            => Escribir("OK   ", $"[LimpiezaSD] Completada → SD: {letraSD}");

        public static void LimpiezaSDCompletadaConErrores(string letraSD, int errores)
            => Escribir("WARN ", $"[LimpiezaSD] Completada con {errores} error(es) → SD: {letraSD}");

        public static void LimpiezaSDElementoFallido(string nombre, string error)
            => Escribir("ERROR", $"[LimpiezaSD] No se pudo eliminar '{nombre}' → {error}");

        // ── RP2040 / Picofly ─────────────────────────────────────────────

        public static void Rp2040Detectado(string letra)
            => Escribir("INFO ", $"[RP2040] Chip detectado en unidad {letra}");

        public static void Rp2040FlasheoIniciado(string letra, string urlFirmware)
            => Escribir("INFO ", $"[RP2040] Flasheo iniciado → unidad: {letra}, firmware: {urlFirmware}");

        public static void Rp2040FlasheoCompletado(string letra)
            => Escribir("OK   ", $"[RP2040] Flasheo completado → unidad: {letra}");

        public static void Rp2040FlasheoFallido(string letra, Exception ex)
            => Escribir("ERROR", $"[RP2040] Flasheo fallido → unidad: {letra} | {ex.Message}");

        public static void Rp2040GuardadoEnPc(string rutaDestino)
            => Escribir("INFO ", $"[RP2040] Firmware guardado en PC → {rutaDestino}");

        // ── Respaldo de llaves ───────────────────────────────────────────

        public static void RespaldoLlavesIniciado(string serial, string rutaDestino)
            => Escribir("INFO ", $"[Llaves] Respaldo iniciado → serial: {serial}, destino: {rutaDestino}");

        public static void RespaldoLlavesCompletado(string serial, int archivos, string rutaDestino)
            => Escribir("OK   ", $"[Llaves] Respaldo completado → serial: {serial}, {archivos} archivo(s) → {rutaDestino}");

        public static void RespaldoLlavesFallido(string serial, string error)
            => Escribir("ERROR", $"[Llaves] Respaldo fallido → serial: {serial} | {error}");

        public static void RespaldoLlavesVerificado(string serial)
            => Escribir("OK   ", $"[Llaves] Verificación OK → bis_key_00 coincide en BISKEYS.bin y prod.keys (serial: {serial})");

        public static void RespaldoLlavesDiscrepancia(string serial)
            => Escribir("WARN ", $"[Llaves] ADVERTENCIA — bis_key_00 NO coincide → archivos de consolas distintas (serial: {serial})");

        public static void RespaldoLlavesAutoIniciado(string serial, string operacion)
            => Escribir("INFO ", $"[Llaves] Respaldo automático iniciado antes de '{operacion}' → serial: {serial}");

        public static void RespaldoLlavesAutoOmitido(string serial, string motivo)
            => Escribir("INFO ", $"[Llaves] Respaldo automático omitido → serial: {serial} | {motivo}");

        /// <summary>
        /// La SD traía una prod.keys con más entradas que el respaldo local.
        /// El respaldo local fue actualizado antes de la restauración.
        /// </summary>
        public static void RespaldoLlavesActualizadoPorMasEntradas(
            string serial, int entradasAnteriores, int entradasNuevas)
            => Escribir("OK   ",
                $"[Llaves] prod.keys local actualizado desde SD → serial: {serial} " +
                $"| entradas: {entradasAnteriores} → {entradasNuevas} " +
                $"(la SD tenía una versión más completa)");

        public static void RestauracionLlavesIniciada(string serial, string letraSD)
            => Escribir("INFO ", $"[Llaves] Restauración iniciada → serial: {serial}, SD: {letraSD}");

        public static void RestauracionLlavesCompletada(string serial, int archivos)
            => Escribir("OK   ", $"[Llaves] Restauración completada → serial: {serial}, {archivos} archivo(s)");

        public static void RestauracionLlavesFallida(string serial, string error)
            => Escribir("WARN ", $"[Llaves] Restauración fallida (no bloqueante) → serial: {serial} | {error}");

        // ── Escritura interna ────────────────────────────────────────────

        private static void Escribir(string nivel, string mensaje)
        {
            try
            {
                string linea = $"[{DateTime.Now:HH:mm:ss}] [{nivel}] {mensaje}";
                lock (_lock)
                    File.AppendAllText(_logFilePath, linea + Environment.NewLine, Encoding.UTF8);
                System.Diagnostics.Debug.WriteLine(linea);
            }
            catch { }
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private static string BuildCabecera()
        {
            string winVersion = ObtenerVersionWindows();
            var sb = new StringBuilder();
            sb.AppendLine(Separador);
            sb.AppendLine($"  SESIÓN  {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"  App     NX-Swite v{ConfiguracionLocal.VersionActual}");
            sb.AppendLine($"  Windows {winVersion}");
            sb.AppendLine(Separador);
            return sb.ToString();
        }

        private static string ObtenerVersionWindows()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Caption, Version FROM Win32_OperatingSystem");
                foreach (ManagementObject obj in searcher.Get())
                {
                    string caption = obj["Caption"]?.ToString() ?? "Windows";
                    string version = obj["Version"]?.ToString() ?? string.Empty;
                    return $"{caption} (Build {version})";
                }
            }
            catch { }
            return System.Runtime.InteropServices.RuntimeInformation.OSDescription;
        }

        /// <summary>
        /// Recorta el contenido del log conservando solo las últimas
        /// <paramref name="n"/> sesiones completas.
        /// Una sesión empieza con la línea del separador "════…".
        /// </summary>
        private static string RecortarSesiones(string contenido, int n)
        {
            if (string.IsNullOrWhiteSpace(contenido)) return string.Empty;

            var lineas = contenido.Split('\n');
            var inicios = new List<int>();

            for (int i = 0; i < lineas.Length; i++)
            {
                if (lineas[i].TrimEnd().StartsWith("════"))
                    inicios.Add(i);
            }

            // Cada sesión usa 2 líneas de separador (apertura y cierre del bloque de cabecera)
            // Agrupamos por pares para identificar sesiones reales
            var sesiones = new List<int>();
            for (int i = 0; i < inicios.Count; i += 2)
                sesiones.Add(inicios[i]);

            if (sesiones.Count <= n)
                return contenido;

            int lineaCorte = sesiones[sesiones.Count - n];
            return string.Join('\n', lineas.Skip(lineaCorte));
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1_048_576) return $"{bytes / 1_048_576.0:F1} MB";
            if (bytes >= 1_024)     return $"{bytes / 1_024.0:F1} KB";
            return $"{bytes} B";
        }

        // ── API pública para el visor de log ─────────────────────────────

        /// <summary>
        /// Lee el archivo de log y lo devuelve como lista de sesiones parseadas,
        /// la más reciente primero.
        /// </summary>
        public static List<SesionLog> ObtenerSesiones()
        {
            try
            {
                if (!File.Exists(_logFilePath)) return new();
                string contenido = File.ReadAllText(_logFilePath, Encoding.UTF8);
                return ParsearSesiones(contenido);
            }
            catch { return new(); }
        }

        /// <summary>Borra el contenido completo del log y abre una sesión limpia.</summary>
        public static void LimpiarLog()
        {
            try
            {
                lock (_lock)
                    File.WriteAllText(_logFilePath, string.Empty, Encoding.UTF8);
            }
            catch { }
        }

        /// <summary>Devuelve el contenido completo del log como texto plano.</summary>
        public static string ObtenerTextoCompleto()
        {
            try
            {
                return File.Exists(_logFilePath)
                    ? File.ReadAllText(_logFilePath, Encoding.UTF8)
                    : string.Empty;
            }
            catch { return string.Empty; }
        }

        private static List<SesionLog> ParsearSesiones(string contenido)
        {
            var sesiones  = new List<SesionLog>();
            var lineas    = contenido.Split('\n');
            SesionLog?    sesionActual = null;
            bool          enCabecera  = false;
            var           cabeceraBuf = new List<string>();

            foreach (var lineaRaw in lineas)
            {
                string linea = lineaRaw.TrimEnd('\r');

                // Inicio de bloque de cabecera
                if (linea.TrimStart().StartsWith("════"))
                {
                    if (!enCabecera)
                    {
                        // Primer separador: empieza cabecera nueva sesión
                        if (sesionActual != null)
                            sesiones.Add(sesionActual);

                        sesionActual = new SesionLog();
                        cabeceraBuf.Clear();
                        enCabecera = true;
                    }
                    else
                    {
                        // Segundo separador: fin de cabecera
                        // Extraer fecha de la cabecera: línea "  SESIÓN  yyyy-MM-dd HH:mm:ss"
                        foreach (var cLine in cabeceraBuf)
                        {
                            var trimmed = cLine.Trim();
                            if (trimmed.StartsWith("SESIÓN"))
                            {
                                var partes = trimmed.Split(new[] { "SESIÓN" }, StringSplitOptions.None);
                                if (partes.Length > 1 &&
                                    DateTime.TryParse(partes[1].Trim(), out var fecha))
                                    sesionActual!.Fecha = fecha;
                            }
                        }
                        enCabecera = false;
                    }
                    continue;
                }

                if (enCabecera)
                {
                    cabeceraBuf.Add(linea);
                    continue;
                }

                if (sesionActual == null || string.IsNullOrWhiteSpace(linea))
                    continue;

                // Parsear línea: [HH:mm:ss] [NIVEL] mensaje
                // Formato: "[14:32:01] [OK   ] texto"
                string nivel   = "INFO";
                string mensaje = linea;
                if (linea.Length >= 22 && linea[0] == '[')
                {
                    int c1 = linea.IndexOf(']');
                    if (c1 > 0 && linea.Length > c1 + 2 && linea[c1 + 2] == '[')
                    {
                        int c2 = linea.IndexOf(']', c1 + 2);
                        if (c2 > 0)
                        {
                            nivel   = linea.Substring(c1 + 3, c2 - c1 - 3).Trim();
                            mensaje = linea.Substring(c2 + 2).Trim();
                        }
                    }
                }
                sesionActual.Lineas.Add(new LineaLog { Nivel = nivel, Mensaje = mensaje, TextoCompleto = linea });
            }

            if (sesionActual != null)
                sesiones.Add(sesionActual);

            // Más reciente primero
            sesiones.Reverse();
            return sesiones;
        }
    }

    // ── Modelos del visor de log ─────────────────────────────────────────

    public class SesionLog
    {
        public DateTime Fecha  { get; set; } = DateTime.MinValue;
        public List<LineaLog> Lineas { get; } = new();

        public string Titulo => Fecha == DateTime.MinValue
            ? "Sesión sin fecha"
            : Fecha.ToString("yyyy-MM-dd  HH:mm:ss");

        public bool TieneErrores => Lineas.Any(l => l.Nivel is "ERROR");
    }

    public class LineaLog
    {
        public string Nivel        { get; set; } = "INFO";
        public string Mensaje      { get; set; } = string.Empty;
        public string TextoCompleto{ get; set; } = string.Empty;
    }
}