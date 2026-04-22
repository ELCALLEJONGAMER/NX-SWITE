using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
using System.Diagnostics;
using NX_Suite.Models;

namespace NX_Suite.Core
{
    public class ReglasLogic
    {


        private readonly DownloadLogic _motorDescarga = new DownloadLogic();
        private readonly ZipLogic _motorZip = new ZipLogic();

        // Reemplaza toda la función EjecutarPipelineAsync por esta:
        public async Task<(bool Exito, string MensajeError)> EjecutarPipelineAsync(List<PasoPipeline> pipeline, string letraSD, IProgress<EstadoProgreso> progreso = null)
        {
            if (pipeline == null || pipeline.Count == 0) return (true, "");

            string carpetaAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string rutaBovedaCache = Path.Combine(carpetaAppData, "NX-Suite", "Cache", "Zips");
            string rutaBovedaExtraccion = Path.Combine(carpetaAppData, "NX-Suite", "Cache", "Extracted");
            string rutaBovedaBackups = Path.Combine(carpetaAppData, "NX-Suite", "Backups");

            if (!Directory.Exists(rutaBovedaCache)) Directory.CreateDirectory(rutaBovedaCache);
            if (!Directory.Exists(rutaBovedaExtraccion)) Directory.CreateDirectory(rutaBovedaExtraccion);
            if (!Directory.Exists(rutaBovedaBackups)) Directory.CreateDirectory(rutaBovedaBackups);

            return await Task.Run(async () =>
            {
                try
                {
                    int totalPasos = pipeline.Count;
                    int pasoActual = 0;

                    foreach (var paso in pipeline)
                    {
                        pasoActual++;
                        Reportar(progreso, pasoActual, totalPasos, paso.MensajeUI);

                        switch (paso.TipoAccion.ToUpper())
                        {
                            case "DESCARGAR":
                                string url = paso.Parametros.GetProperty("Url").GetString();
                                string archivoDestino = paso.Parametros.GetProperty("ArchivoDestino").GetString();
                                string rutaZipLocal = Path.Combine(rutaBovedaCache, archivoDestino);
                                if (!File.Exists(rutaZipLocal)) await _motorDescarga.DescargarArchivoAsync(url, rutaZipLocal, progreso);
                                break;

                            case "EXTRAER":
                                string archivoZip = paso.Parametros.GetProperty("ArchivoZip").GetString();
                                string carpetaTemp = paso.Parametros.GetProperty("CarpetaDestinoTemp").GetString();
                                string rutaZipAExtraer = Path.Combine(rutaBovedaCache, archivoZip);
                                string rutaDestinoTemp = Path.Combine(rutaBovedaExtraccion, carpetaTemp);
                                if (!Directory.Exists(rutaDestinoTemp) || Directory.GetFiles(rutaDestinoTemp, "*.*", SearchOption.AllDirectories).Length == 0)
                                    await _motorZip.ExtraerTodoAsync(rutaZipAExtraer, rutaDestinoTemp, progreso);
                                break;

                            case "COPIARSD":
                                string origenTemp = paso.Parametros.GetProperty("OrigenTemp").GetString();
                                string destinoSDJson = paso.Parametros.GetProperty("DestinoSD").GetString();
                                string rutaDestinoSD = Path.Combine(letraSD, destinoSDJson.TrimStart('/'));

                                // Primero intenta buscar como carpeta extraída
                                string rutaOrigenExtraccion = Path.Combine(rutaBovedaExtraccion, origenTemp);
                                if (Directory.Exists(rutaOrigenExtraccion))
                                {
                                    CopiarDirectorio(rutaOrigenExtraccion, rutaDestinoSD);
                                }
                                else
                                {
                                    // Si no es carpeta, intenta como archivo en Zips o Extracted
                                    string rutaOrigenZips = Path.Combine(rutaBovedaCache, origenTemp);
                                    if (File.Exists(rutaOrigenZips))
                                    {
                                        if (!Directory.Exists(rutaDestinoSD)) Directory.CreateDirectory(rutaDestinoSD);
                                        File.Copy(rutaOrigenZips, Path.Combine(rutaDestinoSD, Path.GetFileName(origenTemp)), true);
                                    }
                                }
                                break;

                            case "HEKATE_SET_ICON":
                                string iniArchivoRel = paso.Parametros.GetProperty("ArchivoIni").GetString();
                                string iniTipoIcono  = paso.Parametros.GetProperty("TipoIcono").GetString();
                                string iniRutaIcono  = paso.Parametros.GetProperty("RutaIcono").GetString();
                                string iniFullPath   = Path.Combine(letraSD, iniArchivoRel.TrimStart('/'));

                                if (File.Exists(iniFullPath))
                                {
                                    var iniMgr = new HekateIniManager(iniFullPath);
                                    await iniMgr.LoadAsync();

                                    List<string> seccionesObjetivo = iniTipoIcono.ToLower() switch
                                    {
                                        "emummc"  => iniMgr.ObtenerSeccionesConClave("emummcforce", "1"),
                                        "stock"   => iniMgr.ObtenerSeccionesConClave("stock", "1"),
                                        "sysnand" => iniMgr.ObtenerSeccionesConClave("emummc_force_disable", "1")
                                                          .Intersect(iniMgr.ObtenerSeccionesConClave("atmosphere", "1"))
                                                          .ToList(),
                                        _         => new List<string>()
                                    };

                                    foreach (var sec in seccionesObjetivo)
                                        iniMgr.SetValue(sec, "icon", iniRutaIcono);

                                    if (seccionesObjetivo.Count > 0)
                                        await iniMgr.SaveAsync();
                                }
                                break;

                            case "BORRARARCHIVOS":
                                var rutasBorrar = paso.Parametros.GetProperty("RutasSD").EnumerateArray();
                                foreach (var ruta in rutasBorrar)
                                {
                                    string archivoAbsoluto = Path.Combine(letraSD, ruta.GetString().TrimStart('/'));
                                    if (File.Exists(archivoAbsoluto)) File.Delete(archivoAbsoluto);
                                }
                                break;

                            case "BORRARCARPETAS":
                                var carpetasBorrar = paso.Parametros.GetProperty("CarpetasSD").EnumerateArray();
                                foreach (var carpeta in carpetasBorrar)
                                {
                                    string carpetaAbsoluta = Path.Combine(letraSD, carpeta.GetString().TrimStart('/'));
                                    if (Directory.Exists(carpetaAbsoluta)) Directory.Delete(carpetaAbsoluta, true);
                                }
                                break;

                            case "CREARCARPETA":
                                string nuevaCarpeta = paso.Parametros.GetProperty("CarpetaSD").GetString();
                                string rutaNueva = Path.Combine(letraSD, nuevaCarpeta.TrimStart('/'));
                                if (!Directory.Exists(rutaNueva)) Directory.CreateDirectory(rutaNueva);
                                break;

                            case "MOVERARCHIVO":
                                string origenMove = paso.Parametros.GetProperty("Origen").GetString();
                                string destinoMove = paso.Parametros.GetProperty("Destino").GetString();
                                bool ignorarSiFalta = paso.Parametros.TryGetProperty("IgnorarSiNoExiste", out var prop) && prop.GetBoolean();
                                string rutaOrigenMove = Path.Combine(letraSD, origenMove.TrimStart('/'));
                                string rutaDestinoMove = Path.Combine(letraSD, destinoMove.TrimStart('/'));
                                if (File.Exists(rutaOrigenMove))
                                {
                                    string dirDestino = Path.GetDirectoryName(rutaDestinoMove);
                                    if (!Directory.Exists(dirDestino)) Directory.CreateDirectory(dirDestino);
                                    if (File.Exists(rutaDestinoMove)) File.Delete(rutaDestinoMove);
                                    File.Move(rutaOrigenMove, rutaDestinoMove);
                                }
                                else if (!ignorarSiFalta) throw new Exception($"No existe el archivo a mover: {rutaOrigenMove}");
                                break;

                            case "EJECUTARCMD":
                                string comando = paso.Parametros.GetProperty("Comando").GetString();
                                string argumentos = paso.Parametros.TryGetProperty("Argumentos", out var argProp) ? argProp.GetString() : "";
                                bool oculto = paso.Parametros.TryGetProperty("Oculto", out var ocProp) && ocProp.GetBoolean();
                                var startInfo = new ProcessStartInfo { FileName = comando, Arguments = argumentos, UseShellExecute = false, CreateNoWindow = oculto };
                                using (var proceso = Process.Start(startInfo)) proceso?.WaitForExit();
                                break;

                            case "RESPALDARAPC":
                                string origenSD = paso.Parametros.GetProperty("OrigenSD").GetString();
                                string nombreBack = paso.Parametros.GetProperty("NombreRespaldo").GetString();
                                string fullOrigen = Path.Combine(letraSD, origenSD.TrimStart('/'));
                                string fullDestino = Path.Combine(rutaBovedaBackups, nombreBack);

                                if (File.Exists(fullOrigen)) File.Copy(fullOrigen, fullDestino, true);
                                else if (Directory.Exists(fullOrigen)) CopiarDirectorio(fullOrigen, fullDestino);
                                break;

                            case "RESTAURARDEPC":
                                string nombreRes = paso.Parametros.GetProperty("NombreRespaldo").GetString();
                                string destinoSD = paso.Parametros.GetProperty("DestinoSD").GetString();
                                string resOrigen = Path.Combine(rutaBovedaBackups, nombreRes);
                                string resDestino = Path.Combine(letraSD, destinoSD.TrimStart('/'));

                                if (File.Exists(resOrigen)) File.Copy(resOrigen, resDestino, true);
                                else if (Directory.Exists(resOrigen)) CopiarDirectorio(resOrigen, resDestino);
                                break;

                            case "LIMPIAR_CACHE":
                                if (paso.Parametros.TryGetProperty("ArchivoZip", out var zipProp))
                                {
                                    string z = Path.Combine(rutaBovedaCache, zipProp.GetString());
                                    if (File.Exists(z)) File.Delete(z);
                                }
                                if (paso.Parametros.TryGetProperty("CarpetaTemp", out var dirProp))
                                {
                                    string d = Path.Combine(rutaBovedaExtraccion, dirProp.GetString());
                                    if (Directory.Exists(d)) Directory.Delete(d, true);
                                }
                                break;

                            case "FORMATEARSD":
                                progreso?.Report(new EstadoProgreso { Porcentaje = 0, TareaActual = "Iniciando preparación...", PasoActual = 1 });
                                string urlTool = paso.Parametros.TryGetProperty("UrlHerramienta", out var urlProp) ? urlProp.GetString() : "";

                                await PrepararFormateadorAsync(urlTool, progreso);

                                string conf = paso.Parametros.GetProperty("Confirmacion").GetString();
                                if (conf.ToUpper() != "SI") throw new Exception("Formateo cancelado por falta de confirmación en el JSON.");

                                bool soloFormatear = paso.Parametros.TryGetProperty("SoloFormatear", out var sfProp) && sfProp.GetBoolean();
                                progreso?.Report(new EstadoProgreso { Porcentaje = 50, TareaActual = "Ejecutando operaciones de disco...", PasoActual = 3 });

                                string letraFija = letraSD.Substring(0, 1) + ":";

                                if (!soloFormatear)
                                {
                                    int emuSize = 0;
                                    if (paso.Parametros.TryGetProperty("TamanoEmuMB", out var emuProp))
                                    {
                                        string val = emuProp.GetString();
                                        if (!string.IsNullOrEmpty(val)) int.TryParse(val, out emuSize);
                                    }

                                    int discoIndice = ObtenerIndiceDiscoFisico(letraFija);
                                    if (discoIndice == -1) throw new Exception("No se pudo identificar el disco físico de la SD para particionar.");

                                    string rutaScriptDP = Path.Combine(Path.GetTempPath(), "dp_script.txt");
                                    string comandosDP = emuSize > 0
                                        ? $"select disk {discoIndice}\nclean\ncreate partition primary size={emuSize}\nremove noerr\nset id=E0\ncreate partition primary\nassign letter={letraFija.Replace(":", "")}\nexit"
                                        : $"select disk {discoIndice}\nclean\ncreate partition primary\nassign letter={letraFija.Replace(":", "")}\nexit";

                                    File.WriteAllText(rutaScriptDP, comandosDP);

                                    var pInfoDP = new ProcessStartInfo
                                    {
                                        FileName = "diskpart.exe",
                                        Arguments = $"/s \"{rutaScriptDP}\"",
                                        UseShellExecute = false,
                                        CreateNoWindow = true,
                                        Verb = "runas"
                                    };

                                    using (var pDP = Process.Start(pInfoDP)) pDP?.WaitForExit();
                                }

                                progreso?.Report(new EstadoProgreso { Porcentaje = 75, TareaActual = "Aplicando formato FAT32...", PasoActual = 3 });
                                await Task.Delay(4000);

                                string exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fat32format.exe");
                                if (File.Exists(exePath))
                                {
                                    var pInfoFAT = new ProcessStartInfo
                                    {
                                        FileName = "cmd.exe",
                                        Arguments = $"/c echo y | \"{exePath}\" {letraFija}",
                                        UseShellExecute = false,
                                        CreateNoWindow = true,
                                        Verb = "runas"
                                    };

                                    using (var pFAT = Process.Start(pInfoFAT)) pFAT?.WaitForExit();
                                    progreso?.Report(new EstadoProgreso { Porcentaje = 100, TareaActual = "Formateo Completado con éxito.", PasoActual = 4 });
                                }
                                else
                                {
                                    throw new Exception("fat32format.exe no encontrado tras la descarga.");
                                }
                                break;
                        }
                        await Task.Delay(500);
                    }

                    // Si llegó hasta aquí sin errores, devolvemos TRUE y sin mensaje
                    return (true, "");
                }
                catch (Exception ex)
                {
                    // Si algo falló, devolvemos FALSE y el mensaje exacto del error
                    return (false, ex.Message);
                }
            });
        }

        private void CopiarDirectorio(string origen, string destino)
        {
            if (!Directory.Exists(destino)) Directory.CreateDirectory(destino);
            foreach (string file in Directory.GetFiles(origen)) File.Copy(file, Path.Combine(destino, Path.GetFileName(file)), true);
            foreach (string dir in Directory.GetDirectories(origen)) CopiarDirectorio(dir, Path.Combine(destino, Path.GetFileName(dir)));
        }

        private void Reportar(IProgress<EstadoProgreso> progreso, int pasoActual, int totalPasos, string mensajeUI)
        {
            if (progreso != null)
            {
                double porcentaje = (double)pasoActual / totalPasos * 100;
                progreso.Report(new EstadoProgreso { Porcentaje = porcentaje, TareaActual = mensajeUI, PasoActual = pasoActual });
            }
        }
        private int ObtenerIndiceDiscoFisico(string letra)
        {
            try
            {
                string driveName = letra.TrimEnd('\\');
                using (var searcher = new System.Management.ManagementObjectSearcher(
                    $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{driveName}'}} WHERE AssocClass=Win32_LogicalDiskToPartition"))
                {
                    foreach (var partition in searcher.Get())
                    {
                        using (var driveSearcher = new System.Management.ManagementObjectSearcher(
                            $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partition["DeviceID"]}'}} WHERE AssocClass=Win32_DiskDriveToDiskPartition"))
                        {
                            foreach (var drive in driveSearcher.Get())
                            {
                                return int.Parse(drive["Index"].ToString());
                            }
                        }
                    }
                }
            }
            catch { }
            return -1;
        }
        private async Task PrepararFormateadorAsync(string urlZip, IProgress<EstadoProgreso> progreso)
        {
            string rutaExeFinal = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fat32format.exe");

            // Si ya existe, avisamos a la UI y saltamos
            if (File.Exists(rutaExeFinal))
            {
                progreso?.Report(new EstadoProgreso { Porcentaje = 100, TareaActual = "Herramienta lista desde caché.", PasoActual = 1 });
                return;
            }

            if (string.IsNullOrWhiteSpace(urlZip)) throw new Exception("La receta no incluye una URL válida para descargar la herramienta.");

            string rutaZip = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fat32format.zip");
            string carpetaExtraccionTemp = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TempFormateador");

            try
            {
                // 1. Descargamos el ZIP (¡Ahora conectamos el progreso visual!)
                await _motorDescarga.DescargarArchivoAsync(urlZip, rutaZip, progreso);

                // 2. Extraemos en la carpeta temporal (¡Con progreso visual!)
                bool extraccionExitosa = await _motorZip.ExtraerTodoAsync(rutaZip, carpetaExtraccionTemp, progreso);
                if (!extraccionExitosa) throw new Exception("El descompresor falló silenciosamente.");

                // 3. Buscar y mover el ejecutable
                progreso?.Report(new EstadoProgreso { Porcentaje = 100, TareaActual = "Instalando herramienta en el motor...", PasoActual = 2 });
                var archivosEncontrados = Directory.GetFiles(carpetaExtraccionTemp, "fat32format.exe", SearchOption.AllDirectories);

                if (archivosEncontrados.Length > 0)
                {
                    File.Copy(archivosEncontrados[0], rutaExeFinal, true);
                }
                else
                {
                    throw new Exception("El ZIP se descargó, pero no contenía 'fat32format.exe'.");
                }

                // 4. Limpieza
                if (File.Exists(rutaZip)) File.Delete(rutaZip);
                if (Directory.Exists(carpetaExtraccionTemp)) Directory.Delete(carpetaExtraccionTemp, true);
            }
            catch (Exception ex)
            {
                throw new Exception($"Fallo de auto-aprovisionamiento: {ex.Message}");
            }
        }

    }

}