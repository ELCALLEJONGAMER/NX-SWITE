using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace NX_Suite_Updater
{
    public partial class VentanaActualizacion : Window
    {
        private readonly string[] _args;
        private string _logPath = Path.Combine(Path.GetTempPath(), "NX-Suite-Updater.log");

        // Ancho total de la barra de progreso (en px, igual al Grid contenedor)
        private const double BarraMaxAncho = 400;

        private const string SufijoBackup = ".nxbak";

        private readonly bool _modoPreview;

        /// <summary>
        /// Archivos tocados durante la extracción: ruta destino y si existía antes
        /// (en cuyo caso hay una copia <see cref="SufijoBackup"/> para restaurar).
        /// </summary>
        private readonly List<(string destino, bool existia)> _tocados = new();

        public VentanaActualizacion(string[] args)
        {
            InitializeComponent();
            _args = args;
            _modoPreview = args.Length == 0;

            if (args.Length >= 5)
                CargarLogo(args[4]);

            Loaded += async (_, _) =>
            {
                AnimarEntrada();
                if (_modoPreview)
                    await EjecutarPreviewAsync();
                else
                    await EjecutarActualizacionAsync();
            };
        }

        // ─────────────────────────────────────────────────────────────────
        //  Helpers de UI
        // ─────────────────────────────────────────────────────────────────

        private void SetEstado(string mensaje, string detalle = "", double progreso = -1)
        {
            Dispatcher.Invoke(() =>
            {
                TxtEstado.Text   = mensaje;
                TxtDetalle.Text  = detalle;

                if (progreso >= 0)
                {
                    double pct = Math.Clamp(progreso, 0, 1);
                    TxtPorcentaje.Text   = $"{(int)(pct * 100)}%";
                    BarraProgreso.Width  = pct * BarraMaxAncho;
                }
            });
        }

        /// <summary>
        /// Carga el logo de NX-SWITE (ruta local en caché pasada por la app principal)
        /// para que el updater tenga el mismo aspecto que el splash. Si no existe,
        /// se usa el logo embebido en el updater.
        /// </summary>
        private void CargarLogo(string ruta)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ruta) || !File.Exists(ruta)) return;

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource   = new Uri(ruta);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();

                ImgLogo.Source        = bmp;
            }
            catch { /* best-effort: se queda el logo embebido */ }
        }

        private void AnimarEntrada()
        {
            var duracion = new Duration(TimeSpan.FromMilliseconds(480));
            var ease     = new CubicEase { EasingMode = EasingMode.EaseOut };

            PanelContenido.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0, 1, duracion) { EasingFunction = ease });
            EscalaLogo.BeginAnimation(ScaleTransform.ScaleXProperty,
                new DoubleAnimation(0.88, 1.0, duracion) { EasingFunction = ease });
            EscalaLogo.BeginAnimation(ScaleTransform.ScaleYProperty,
                new DoubleAnimation(0.88, 1.0, duracion) { EasingFunction = ease });
        }

        // ─────────────────────────────────────────────────────────────────
        //  Log
        // ─────────────────────────────────────────────────────────────────

        private void Log(string msg)
        {
            string line = $"[{DateTime.Now:HH:mm:ss.fff}] {msg}";
            try { File.AppendAllText(_logPath, line + Environment.NewLine); } catch { }
        }

        // ─────────────────────────────────────────────────────────────────
        //  Modo preview (sin argumentos)
        // ─────────────────────────────────────────────────────────────────

        private async Task EjecutarPreviewAsync()
        {
            Title = "NX-SWITE Updater - PREVIEW";

            var pasos = new (string estado, string detalle, double progreso, int ms)[]
            {
                ("Esperando que NX-SWITE se cierre...",    "PID 12345",             0.05, 1200),
                ("Verificando paquete de actualización...", "NX-SWITE-1.2.0.zip",  0.10,  800),
                ("Copiando archivos nuevos...",             "NX-Swite.exe",         0.25,  600),
                ("Copiando archivos nuevos...",             "NX-Swite.Updater.exe", 0.40,  600),
                ("Copiando archivos nuevos...",             "assets/logo.png",      0.55,  600),
                ("Copiando archivos nuevos...",             "Sounds/intro.mp3",     0.70,  600),
                ("Copiando archivos nuevos...",             "README.md",            0.85,  600),
                ("Limpiando archivos temporales...",        "",                     0.88,  700),
                ("Lanzando NX-SWITE...",                   "NX-Swite.exe",         0.95,  900),
                ("¡Actualización completada!",              "",                     1.00, 1500),
            };

            foreach (var (estado, detalle, progreso, ms) in pasos)
            {
                SetEstado(estado, detalle, progreso);
                await Task.Delay(ms);
            }

            Application.Current.Shutdown(0);
        }

        // ─────────────────────────────────────────────────────────────────
        //  Logica principal
        // ─────────────────────────────────────────────────────────────────

        private async Task EjecutarActualizacionAsync()
        {
            File.WriteAllText(_logPath, $"=== NX-Suite Updater {DateTime.Now:yyyy-MM-dd HH:mm:ss} ==={Environment.NewLine}");
            Log($"Argumentos recibidos: {_args.Length} -> [{string.Join(", ", _args.Select(a => $"\"{a}\""))}]");

            if (_args.Length < 4)
            {
                SetEstado("Error: argumentos insuficientes.", "", 0);
                Log("ERROR: Se requieren 4 argumentos: <zipPath> <targetDir> <mainExePath> <parentPid> [logoPath]");
                await Task.Delay(4000);
                Application.Current.Shutdown(1);
                return;
            }

            string zipPath       = _args[0];
            string targetDirFull = Path.GetFullPath(_args[1]);
            string mainExe       = _args[2];

            if (!int.TryParse(_args[3], out int parentPid))
            {
                SetEstado($"Error: PID inválido '{_args[3]}'.", "", 0);
                Log($"ERROR: PID inválido: '{_args[3]}'");
                await Task.Delay(4000);
                Application.Current.Shutdown(1);
                return;
            }

            try
            {
                // Paso 1: Esperar cierre de la app principal ────────────
                SetEstado("Esperando que NX-SWITE se cierre...", $"PID {parentPid}", 0.05);
                Log($"Esperando al proceso padre (PID {parentPid})...");

                await Task.Run(() =>
                {
                    try
                    {
                        using var parent = Process.GetProcessById(parentPid);
                        if (!parent.WaitForExit(20_000))
                        {
                            // Solo el proceso de la app: con entireProcessTree se
                            // mataría también a este updater (es hijo suyo).
                            Log("ADVERTENCIA: El proceso no cerró en 20 s, se fuerza la terminación.");
                            parent.Kill();
                            parent.WaitForExit(5_000);
                        }
                    }
                    catch (ArgumentException)
                    {
                        Log("El proceso padre ya no existe, continuando.");
                    }
                    Thread.Sleep(500);
                });

                // Paso 2: Validar ZIP ───────────────────────────────────
                SetEstado("Verificando paquete de actualización...", Path.GetFileName(zipPath), 0.10);
                Log($"ZIP: '{zipPath}'");
                Log($"targetDir: '{targetDirFull}'");

                if (!File.Exists(zipPath))
                    throw new FileNotFoundException("No se encontró el ZIP de actualización.", zipPath);

                // Paso 3: Extraer archivos (con copia de seguridad)
                SetEstado("Copiando archivos nuevos...", "", 0.15);
                Log("Extrayendo ZIP...");

                await Task.Run(() => ExtraerConRespaldo(zipPath, targetDirFull));

                // Todo copiado: las copias de seguridad ya no hacen falta
                EliminarRespaldos();

                // Paso 4: Limpiar ZIP temporal ──────────────────────────
                SetEstado("Limpiando archivos temporales...", "", 0.88);
                await Task.Run(() =>
                {
                    try { File.Delete(zipPath); Log("ZIP temporal eliminado."); } catch { }
                });

                // Paso 5: Lanzar la app principal ───────────────────────
                SetEstado("Lanzando NX-SWITE...", Path.GetFileName(mainExe), 0.95);
                await Task.Delay(600); // pequeña pausa para que el usuario vea el mensaje
                LanzarApp(mainExe, targetDirFull);

                // Fin ───────────────────────────────────────────────────
                SetEstado("¡Actualización completada!", "", 1.0);
                Log("Actualización completada.");
                await Task.Delay(1200);
                Application.Current.Shutdown(0);
            }
            catch (Exception ex)
            {
                Log($"EXCEPCIÓN: {ex.GetType().Name}: {ex.Message}");
                Log(ex.StackTrace ?? string.Empty);

                SetEstado("Error durante la actualización.", "Restaurando la versión anterior...", 0);
                await Task.Run(RestaurarRespaldos);

                SetEstado("No se pudo actualizar.", "Se ha restaurado la versión anterior.", 0);
                await Task.Delay(2500);

                // Siempre volver a abrir NX-SWITE: el usuario no se queda sin app
                LanzarApp(mainExe, targetDirFull);

                // Mostrar log al usuario
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName        = "notepad.exe",
                        Arguments       = _logPath,
                        UseShellExecute = true,
                    });
                }
                catch { }

                Application.Current.Shutdown(1);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  Extracción transaccional
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Extrae el ZIP sobre <paramref name="targetDirFull"/>. Antes de sobrescribir
        /// un archivo lo renombra a <c>*.nxbak</c>; si algo falla se lanza excepción
        /// y <see cref="RestaurarRespaldos"/> deja la instalación como estaba.
        /// </summary>
        private void ExtraerConRespaldo(string zipPath, string targetDirFull)
        {
            string updaterFileName = Path.GetFileName(Environment.ProcessPath ?? "NX-Swite.Updater.exe");
            string raizSegura      = targetDirFull.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

            using var zip = ZipFile.OpenRead(zipPath);

            var fileEntries = zip.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).ToList();

            // Detectar carpeta raíz común del ZIP
            string rootPrefix = string.Empty;
            if (fileEntries.Count > 0)
            {
                string firstPart = fileEntries[0].FullName.Split('/')[0] + "/";
                if (fileEntries.All(e => e.FullName.StartsWith(firstPart, StringComparison.OrdinalIgnoreCase)))
                    rootPrefix = firstPart;
            }

            Log($"Prefijo raíz del ZIP: '{rootPrefix}'");
            Log($"Total entradas: {fileEntries.Count}");

            int total    = fileEntries.Count;
            int copiados = 0;

            foreach (var entry in fileEntries)
            {
                copiados++;

                string relativePath = rootPrefix.Length > 0
                    ? entry.FullName[rootPrefix.Length..]
                    : entry.FullName;

                if (string.IsNullOrEmpty(relativePath)) continue;

                // No sobreescribir el updater en ejecución
                if (Path.GetFileName(relativePath).Equals(updaterFileName, StringComparison.OrdinalIgnoreCase))
                {
                    Log($"  OMITIDO (updater en uso): {relativePath}");
                    continue;
                }

                string destPath = Path.GetFullPath(Path.Combine(targetDirFull, relativePath));

                // Seguridad zip-slip
                if (!destPath.StartsWith(raizSegura, StringComparison.OrdinalIgnoreCase))
                {
                    Log($"  OMITIDO (fuera de targetDir): {destPath}");
                    continue;
                }

                string? destDir = Path.GetDirectoryName(destPath);
                if (destDir != null && !Directory.Exists(destDir))
                    Directory.CreateDirectory(destDir);

                // Reintentar hasta 3 veces si el archivo está bloqueado
                for (int intento = 0; ; intento++)
                {
                    try
                    {
                        ReemplazarArchivo(entry, destPath);
                        break;
                    }
                    catch (IOException ex) when (intento < 2)
                    {
                        Log($"  Intento {intento + 1} fallido para '{relativePath}': {ex.Message}");
                        Thread.Sleep(300);
                    }
                }

                Log($"  OK: {relativePath}");

                // Actualizar progreso en el hilo de UI (0.15 -> 0.85)
                double progreso = 0.15 + (copiados / (double)total) * 0.70;
                SetEstado("Copiando archivos nuevos...", relativePath, progreso);
            }
        }

        private void ReemplazarArchivo(ZipArchiveEntry entry, string destPath)
        {
            bool yaRegistrado = _tocados.Any(t => t.destino.Equals(destPath, StringComparison.OrdinalIgnoreCase));

            if (!yaRegistrado)
            {
                bool existia = File.Exists(destPath);
                if (existia)
                {
                    string backup = destPath + SufijoBackup;
                    if (File.Exists(backup)) File.Delete(backup);
                    File.Move(destPath, backup);
                }
                _tocados.Add((destPath, existia));
            }

            entry.ExtractToFile(destPath, overwrite: true);
        }

        private void RestaurarRespaldos()
        {
            Log($"Restaurando {_tocados.Count} archivo(s)...");
            foreach (var (destino, existia) in Enumerable.Reverse(_tocados))
            {
                try
                {
                    if (File.Exists(destino)) File.Delete(destino);
                    if (existia)
                    {
                        string backup = destino + SufijoBackup;
                        if (File.Exists(backup)) File.Move(backup, destino);
                    }
                }
                catch (Exception ex)
                {
                    Log($"  ERROR restaurando '{destino}': {ex.Message}");
                }
            }
            _tocados.Clear();
        }

        private void EliminarRespaldos()
        {
            foreach (var (destino, existia) in _tocados)
            {
                if (!existia) continue;
                try { File.Delete(destino + SufijoBackup); } catch { /* inofensivo */ }
            }
            _tocados.Clear();
        }

        // ─────────────────────────────────────────────────────────────────
        //  Relanzar app
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Lanza la app principal. Si el exe recibido no existe (updater antiguo que
        /// apuntaba a NX-Suite.exe) busca NX-Swite.exe, NX-Suite.exe y, en último
        /// recurso, cualquier exe que no sea el updater ni fat32format.
        /// </summary>
        private void LanzarApp(string mainExe, string targetDirFull)
        {
            try
            {
                string exeALanzar = mainExe;
                if (!File.Exists(exeALanzar))
                {
                    Log($"'{mainExe}' no encontrado, buscando ejecutable alternativo en '{targetDirFull}'...");
                    foreach (var c in new[]
                    {
                        Path.Combine(targetDirFull, "NX-Swite.exe"),
                        Path.Combine(targetDirFull, "NX-Suite.exe"),
                    })
                    {
                        if (File.Exists(c)) { exeALanzar = c; break; }
                    }
                    if (!File.Exists(exeALanzar))
                    {
                        string updaterName = Path.GetFileName(Environment.ProcessPath ?? string.Empty);
                        var otros = Directory.GetFiles(targetDirFull, "*.exe")
                            .Where(f => !Path.GetFileName(f).Equals(updaterName, StringComparison.OrdinalIgnoreCase)
                                     && !Path.GetFileName(f).Equals("fat32format.exe", StringComparison.OrdinalIgnoreCase))
                            .ToArray();
                        if (otros.Length > 0) exeALanzar = otros[0];
                    }
                    Log($"Ejecutable alternativo encontrado: '{exeALanzar}'");
                }

                if (!File.Exists(exeALanzar))
                {
                    Log($"ERROR: No se encontró ningún ejecutable principal en '{targetDirFull}'.");
                    return;
                }

                Log($"Relanzando: '{exeALanzar}'");
                Process.Start(new ProcessStartInfo
                {
                    FileName        = exeALanzar,
                    UseShellExecute = true,
                });
                Log("Proceso relanzado correctamente.");
            }
            catch (Exception ex)
            {
                Log($"ERROR relanzando la app: {ex.Message}");
            }
        }
    }
}
