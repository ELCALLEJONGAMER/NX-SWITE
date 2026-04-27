using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;

// ????????????????????????????????????????????????????????????????????????????
//  NX-Suite.Updater
//  Argumentos: <zipPath> <targetDir> <mainExePath> <parentPid>
//
//  1. Espera a que el proceso padre (NX-Suite) termine.
//  2. Extrae el ZIP sobre el directorio de instalación (gestionando carpeta raíz).
//  3. Relanza NX-Suite.exe.
// ????????????????????????????????????????????????????????????????????????????

string logPath = Path.Combine(Path.GetTempPath(), "NX-Suite-Updater.log");
void Log(string msg)
{
    string line = $"[{DateTime.Now:HH:mm:ss.fff}] {msg}";
    Console.WriteLine(line);
    try { File.AppendAllText(logPath, line + Environment.NewLine); } catch { }
}

File.WriteAllText(logPath, $"=== NX-Suite Updater {DateTime.Now:yyyy-MM-dd HH:mm:ss} ==={Environment.NewLine}");
Log($"Argumentos recibidos: {args.Length} ? [{string.Join(", ", args.Select(a => $"\"{a}\""))}]");

if (args.Length < 4)
{
    Log("ERROR: Se requieren 4 argumentos: <zipPath> <targetDir> <mainExePath> <parentPid>");
    return 1;
}

string zipPath   = args[0];
string targetDir = args[1];
string mainExe   = args[2];

if (!int.TryParse(args[3], out int parentPid))
{
    Log($"ERROR: PID inválido: '{args[3]}'");
    return 1;
}

try
{
    // 1. Esperar a que la app principal cierre
    Log($"Esperando al proceso padre (PID {parentPid})...");
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

    // 2. Extraer ZIP
    Log($"ZIP: '{zipPath}'");
    Log($"targetDir: '{targetDir}'");
    if (!File.Exists(zipPath))
        throw new FileNotFoundException("No se encontró el ZIP de actualización.", zipPath);

    string updaterFileName = Path.GetFileName(Environment.ProcessPath ?? "NX-Suite.Updater.exe");
    string targetDirFull   = Path.GetFullPath(targetDir);

    using (var zip = ZipFile.OpenRead(zipPath))
    {
        // Detectar carpeta raíz común del ZIP (GitHub releases suelen incluir una)
        // Ej: "NX-Suite-0.5.0/NX-Suite.exe" ? prefijo "NX-Suite-0.5.0/"
        string rootPrefix = string.Empty;
        var fileEntries   = zip.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).ToList();
        if (fileEntries.Count > 0)
        {
            string firstPart = fileEntries[0].FullName.Split('/')[0] + "/";
            if (fileEntries.All(e => e.FullName.StartsWith(firstPart, StringComparison.OrdinalIgnoreCase)))
                rootPrefix = firstPart;
        }

        Log($"Prefijo raíz del ZIP: '{rootPrefix}'");
        Log($"Total entradas: {fileEntries.Count}");

        foreach (var entry in fileEntries)
        {
            string relativePath = rootPrefix.Length > 0
                ? entry.FullName[rootPrefix.Length..]
                : entry.FullName;

            if (string.IsNullOrEmpty(relativePath)) continue;

            // No sobreescribir el propio updater mientras está en ejecución
            if (Path.GetFileName(relativePath).Equals(updaterFileName, StringComparison.OrdinalIgnoreCase))
            {
                Log($"  OMITIDO (updater en uso): {relativePath}");
                continue;
            }

            string destPath = Path.GetFullPath(Path.Combine(targetDirFull, relativePath));

            // Seguridad: no salir del targetDir (zip slip)
            if (!destPath.StartsWith(targetDirFull, StringComparison.OrdinalIgnoreCase))
            {
                Log($"  OMITIDO (fuera de targetDir): {destPath}");
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
        }
    }

    // 3. Limpiar ZIP temporal
    try { File.Delete(zipPath); Log("ZIP temporal eliminado."); } catch { }

    // 4. Relanzar la app principal
    Log($"Relanzando: '{mainExe}'");
    if (File.Exists(mainExe))
    {
        Process.Start(new ProcessStartInfo
        {
            FileName        = mainExe,
            UseShellExecute = true,
        });
        Log("Proceso relanzado correctamente.");
    }
    else
    {
        Log($"ERROR: No se encontró el ejecutable principal en '{mainExe}'.");
    }

    Log("Actualización completada.");
    return 0;
}
catch (Exception ex)
{
    Log($"EXCEPCIÓN: {ex.GetType().Name}: {ex.Message}");
    Log(ex.StackTrace ?? string.Empty);

    // Mostrar log al usuario en caso de fallo
    try
    {
        Process.Start(new ProcessStartInfo
        {
            FileName        = "notepad.exe",
            Arguments       = logPath,
            UseShellExecute = true,
        });
    }
    catch { }

    return 2;
}
