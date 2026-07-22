using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace NX_Suite_Updater
{
    public partial class VentanaActualizacion : Window
    {
        private readonly string[] _args;
        private string _logPath = Path.Combine(Path.GetTempPath(), "NX-Suite-Updater.log");

        // Ancho total de la barra de progreso (en px, igual al Grid contenedor)
        private const double BarraMaxAncho = 420;

        private readonly bool _modoPreview;

        public VentanaActualizacion(string[] args)
        {
            InitializeComponent();
            _args = args;
            _modoPreview = args.Length == 0;
            Loaded += async (_, _) =>
            {
                if (_modoPreview)
                    await EjecutarPreviewAsync();
                else
                    await EjecutarActualizacionAsync();
            };
        }

        // ?????????????????????????????????????????????????????????????????
        //  Helpers de UI
        // ?????????????????????????????????????????????????????????????????

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

        // ?????????????????????????????????????????????????????????????????
        //  Log
        // ?????????????????????????????????????????????????????????????????

        private void Log(string msg)
        {
            string line = $"[{DateTime.Now:HH:mm:ss.fff}] {msg}";
            try { File.AppendAllText(_logPath, line + Environment.NewLine); } catch { }
        }

        // ?????????????????????????????????????????????????????????????????
        //  Modo preview (sin argumentos)
        // ?????????????????????????????????????????????????????????????????

        private async Task EjecutarPreviewAsync()
        {
            Title = "NX-SWITE Updater - PREVIEW";

            var pasos = new (string estado, string detalle, double progreso, int ms)[]
            {
                ("Esperando que NX-SWITE se cierre...",    "PID 12345",             0.05, 1200),
                ("Verificando paquete de actualizacion...", "NX-SWITE-1.2.0.zip",  0.10,  800),
                ("Copiando archivos nuevos...",             "NX-Swite.exe",         0.25,  600),
                ("Copiando archivos nuevos...",             "NX-Swite.Updater.exe", 0.40,  600),
                ("Copiando archivos nuevos...",             "assets/logo.png",      0.55,  600),
                ("Copiando archivos nuevos...",             "Sounds/intro.mp3",     0.70,  600),
                ("Copiando archivos nuevos...",             "README.md",            0.85,  600),
                ("Limpiando archivos temporales...",        "",                     0.88,  700),
                ("Lanzando NX-SWITE...",                   "NX-Swite.exe",         0.95,  900),
                ("Actualizacion completada!",               "",                     1.00, 1500),
            };

            foreach (var (estado, detalle, progreso, ms) in pasos)
            {
                SetEstado(estado, detalle, progreso);
                await Task.Delay(ms);
            }

            Application.Current.Shutdown(0);
        }

        // ?????????????????????????????????????????????????????????????????
        //  Logica principal
        // ?????????????????????????????????????????????????????????????????

        private async Task EjecutarActualizacionAsync()
        {
            File.WriteAllText(_logPath, $"=== NX-Suite Updater {DateTime.Now:yyyy-MM-dd HH:mm:ss} ==={Environment.NewLine}");
            Log($"Argumentos recibidos: {_args.Length} ? [{string.Join(", ", _args.Select(a => $"\"{a}\""))}]");

            if (_args.Length < 4)
            {
                SetEstado("Error: argumentos insuficientes.", "", 0);
                Log("ERROR: Se requieren 4 argumentos: <zipPath> <targetDir> <mainExePath> <parentPid>");
                await Task.Delay(4000);
                Application.Current.Shutdown(1);
                return;
            }

            string zipPath   = _args[0];
            string targetDir = _args[1];
            string mainExe   = _args[2];

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
                // ?? Paso 1: Esperar cierre de la app principal ????????????
                SetEstado("Esperando que NX-SWITE se cierre...", $"PID {parentPid}", 0.05);
                Log($"Esperando al proceso padre (PID {parentPid})...");

                await Task.Run(() =>
                {
                    try
                    {
                        using var parent = Process.GetProcessById(parentPid);
                        if (!parent.WaitForExit(20_000))
                        {
                            Log("ADVERTENCIA: El proceso no cerró en 20 s, se fuerza la terminación.");
                            parent.Kill(entireProcessTree: true);
                        }
                    }
                    catch (ArgumentException)
                    {
                        Log("El proceso padre ya no existe, continuando.");
                    }
                    Thread.Sleep(500);
                });

                // ?? Paso 2: Validar ZIP ???????????????????????????????????
                SetEstado("Verificando paquete de actualización...", Path.GetFileName(zipPath), 0.10);
                Log($"ZIP: '{zipPath}'");
                Log($"targetDir: '{targetDir}'");

                if (!File.Exists(zipPath))
                    throw new FileNotFoundException("No se encontró el ZIP de actualización.", zipPath);

                // ?? Paso 3: Extraer archivos ??????????????????????????????
                SetEstado("Copiando archivos nuevos...", "", 0.15);
                Log("Extrayendo ZIP...");

                string updaterFileName = Path.GetFileName(Environment.ProcessPath ?? "NX-Suite.Updater.exe");
                string targetDirFull   = Path.GetFullPath(targetDir);

                await Task.Run(() =>
                {
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
                        string relativePath = rootPrefix.Length > 0
                            ? entry.FullName[rootPrefix.Length..]
                            : entry.FullName;

                        if (string.IsNullOrEmpty(relativePath)) { copiados++; continue; }

                        // No sobreescribir el updater en ejecución
                        if (Path.GetFileName(relativePath).Equals(updaterFileName, StringComparison.OrdinalIgnoreCase))
                        {
                            Log($"  OMITIDO (updater en uso): {relativePath}");
                            copiados++;
                            continue;
                        }

                        string destPath = Path.GetFullPath(Path.Combine(targetDirFull, relativePath));

                        // Seguridad zip-slip
                        if (!destPath.StartsWith(targetDirFull, StringComparison.OrdinalIgnoreCase))
                        {
                            Log($"  OMITIDO (fuera de targetDir): {destPath}");
                            copiados++;
                            continue;
                        }

                        string? destDir = Path.GetDirectoryName(destPath);
                        if (destDir != null && !Directory.Exists(destDir))
                            Directory.CreateDirectory(destDir);

                        // Reintentar hasta 3 veces si el archivo está bloqueado
                        bool extraido = false;
                        for (int intento = 0; intento < 3; intento++)
                        {
                            try
                            {
                                entry.ExtractToFile(destPath, overwrite: true);
                                extraido = true;
                                break;
                            }
                            catch (IOException ex) when (intento < 2)
                            {
                                Log($"  Intento {intento + 1} fallido para '{relativePath}': {ex.Message}");
                                Thread.Sleep(300);
                            }
                        }

                        Log($"  {(extraido ? "OK" : "FALLÓ")}: {relativePath}");
                        copiados++;

                        // Actualizar progreso en el hilo de UI (0.15 ? 0.85)
                        double progreso = 0.15 + (copiados / (double)total) * 0.70;
                        SetEstado("Copiando archivos nuevos...", relativePath, progreso);
                    }
                });

                // ?? Paso 4: Limpiar ZIP temporal ??????????????????????????
                SetEstado("Limpiando archivos temporales...", "", 0.88);
                await Task.Run(() =>
                {
                    try { File.Delete(zipPath); Log("ZIP temporal eliminado."); } catch { }
                });

                // ?? Paso 5: Lanzar la app principal ???????????????????????
                // Si el exe recibido no existe (updater antiguo que apuntaba a NX-Suite.exe),
                // buscar cualquier exe principal en el directorio: primero NX-Swite.exe,
                // luego NX-Suite.exe como fallback, y por último cualquier exe que no sea
                // el updater ni fat32format.
                string exeALanzar = mainExe;
                if (!File.Exists(exeALanzar))
                {
                    Log($"'{mainExe}' no encontrado, buscando ejecutable alternativo en '{targetDirFull}'...");
                    string[] candidatos = new[]
                    {
                        Path.Combine(targetDirFull, "NX-Swite.exe"),
                        Path.Combine(targetDirFull, "NX-Suite.exe"),
                    };
                    foreach (var c in candidatos)
                    {
                        if (File.Exists(c)) { exeALanzar = c; break; }
                    }
                    if (!File.Exists(exeALanzar))
                    {
                        // Último recurso: primer exe que no sea el updater ni fat32format
                        string updaterName = Path.GetFileName(Environment.ProcessPath ?? string.Empty);
                        var otros = Directory.GetFiles(targetDirFull, "*.exe")
                            .Where(f => !Path.GetFileName(f).Equals(updaterName, StringComparison.OrdinalIgnoreCase)
                                     && !Path.GetFileName(f).Equals("fat32format.exe", StringComparison.OrdinalIgnoreCase))
                            .ToArray();
                        if (otros.Length > 0) exeALanzar = otros[0];
                    }
                    Log($"Ejecutable alternativo encontrado: '{exeALanzar}'");
                }

                SetEstado("Lanzando NX-SWITE...", Path.GetFileName(exeALanzar), 0.95);
                Log($"Relanzando: '{exeALanzar}'");
                await Task.Delay(600); // pequeña pausa para que el usuario vea el mensaje

                if (File.Exists(exeALanzar))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName        = exeALanzar,
                        UseShellExecute = true,
                    });
                    Log("Proceso relanzado correctamente.");
                }
                else
                {
                    Log($"ERROR: No se encontró ningún ejecutable principal en '{targetDirFull}'.");
                }

                // ?? Fin ???????????????????????????????????????????????????
                SetEstado("¡Actualización completada!", "", 1.0);
                Log("Actualización completada.");
                await Task.Delay(1200);
                Application.Current.Shutdown(0);
            }
            catch (Exception ex)
            {
                Log($"EXCEPCIÓN: {ex.GetType().Name}: {ex.Message}");
                Log(ex.StackTrace ?? string.Empty);

                SetEstado("Error durante la actualización.", ex.Message, 0);

                await Task.Delay(3000);

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
    }
}
