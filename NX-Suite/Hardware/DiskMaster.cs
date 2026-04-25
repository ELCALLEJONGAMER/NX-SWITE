using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using NX_Suite.Services;

namespace NX_Suite.Hardware
{
    // 1. LA ESTRUCTURA ÚNICA DE DATOS PARA LA SD
    public class SDInfo
    {
        public string Letra { get; set; }
        public string Etiqueta { get; set; }
        public string CapacidadTotal { get; set; }
        public string Formato { get; set; }
        public string Serial { get; set; }
        public int DiscoFisico { get; set; } // Necesario para Diskpart
        public string FullName => $"{Letra} ({Etiqueta}) - {CapacidadTotal}GB";
    }

    public class DiskMaster
    {
        // ==========================================
        // 1. DETECCIÓN AUTOMÁTICA (Plug & Play)
        // ==========================================
        public event EventHandler UnidadConectada;
        private const int WM_DEVICECHANGE = 0x0219;
        private Window _ventana;

        public void IniciarEscucha(Window ventana)
        {
            _ventana = ventana;
            _ventana.SourceInitialized += (s, e) =>
            {
                HwndSource source = HwndSource.FromHwnd(new WindowInteropHelper(_ventana).Handle);
                source?.AddHook(HwndHandler);
            };
        }

        private const int DBT_DEVICEARRIVAL        = 0x8000;
        private const int DBT_DEVICEREMOVECOMPLETE  = 0x8004;

        private IntPtr HwndHandler(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_DEVICECHANGE)
            {
                int tipo = wParam.ToInt32();
                if (tipo == DBT_DEVICEARRIVAL || tipo == DBT_DEVICEREMOVECOMPLETE)
                    UnidadConectada?.Invoke(this, EventArgs.Empty);
            }
            return IntPtr.Zero;
        }

        // ==========================================
        // 2. ESCÁNER DE UNIDADES (El que lee la SD)
        // ==========================================
        public List<SDInfo> ObtenerUnidadesRemovibles()
        {
            List<SDInfo> lista = new List<SDInfo>();
            DriveInfo[] unidades = DriveInfo.GetDrives();

            foreach (DriveInfo d in unidades)
            {
                if (d.DriveType == DriveType.Removable)
                {
                    SDInfo info = new SDInfo { Letra = d.Name };
                    try
                    {
                        if (d.IsReady)
                        {
                            info.Etiqueta = string.IsNullOrEmpty(d.VolumeLabel) ? "Disco Extraíble" : d.VolumeLabel;
                            info.CapacidadTotal = (d.TotalSize / 1024 / 1024 / 1024).ToString();
                            info.Formato = d.DriveFormat;
                        }
                        else // Memorias RAW
                        {
                            info.Etiqueta = "Sin Formato (RAW)";
                            info.CapacidadTotal = "0";
                            info.Formato = "RAW";
                        }
                        info.Serial = ObtenerSerialWMI(d.Name.Substring(0, 2));
                        info.DiscoFisico = GetPhysicalDiskNumber(d.Name);
                        lista.Add(info);
                    }
                    catch
                    {
                        info.Etiqueta = "Inaccesible";
                        info.CapacidadTotal = "0";
                        info.Formato = "RAW";
                        lista.Add(info);
                    }
                }
            }
            return lista;
        }

        private string ObtenerSerialWMI(string letra)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher($"SELECT VolumeSerialNumber FROM Win32_LogicalDisk WHERE DeviceID = '{letra}'"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        return obj["VolumeSerialNumber"]?.ToString() ?? "N/A";
                    }
                }
            }
            catch { return "N/A"; }
            return "N/A";
        }

        // ==========================================
        // 3. P/INVOKE Y FUNCIONES DE BAJO NIVEL (Diskpart, Snipper)
        // ==========================================

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool DeviceIoControl(IntPtr hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize, ref STORAGE_DEVICE_NUMBER lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        private struct STORAGE_DEVICE_NUMBER
        {
            public int DeviceType;
            public int DeviceNumber;
            public int PartitionNumber;
        }

        private int GetPhysicalDiskNumber(string driveLetter)
        {
            string path = driveLetter.TrimEnd('\\');
            string devicePath = $@"\\.\{path}";

            IntPtr hDevice = CreateFile(devicePath, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
            if (hDevice == new IntPtr(-1)) return -1;

            STORAGE_DEVICE_NUMBER sdn = new STORAGE_DEVICE_NUMBER();
            uint IOCTL_STORAGE_GET_DEVICE_NUMBER = 0x2D1080;

            bool success = DeviceIoControl(hDevice, IOCTL_STORAGE_GET_DEVICE_NUMBER, IntPtr.Zero, 0, ref sdn, (uint)Marshal.SizeOf(sdn), out uint bytesReturned, IntPtr.Zero);

            CloseHandle(hDevice);
            return success ? sdn.DeviceNumber : -1;
        }

        // --- SNIPER DE VENTANAS ---
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const uint WM_CLOSE = 0x0010;

        // ==========================================
        // 4. PARTICIONADO Y FORMATEO FAT32 (Asistido Completo)
        // ==========================================

        // P/Invoke para establecer la etiqueta del volumen sin abrir ventanas.
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetVolumeLabel(string lpRootPathName, string lpVolumeName);

        /// <summary>
        /// Particiona el disco físico exactamente como lo hace Hekate:
        ///   - Partición 1 (emuMMC) : id=E0 + sin letra → invisible para Windows.
        ///   - Partición 2 (SWITCH SD): FAT32, etiqueta "SWITCH SD", letra asignada por Windows.
        /// Todo el proceso es silencioso (sin ventanas ni diálogos al usuario).
        /// </summary>
        /// <param name="numeroDisco">Índice del disco físico (ej. 4).</param>
        /// <param name="gbEmuMMC">Tamaño en GB de la partición emuMMC oculta.</param>
        /// <param name="urlFat32FormatZip">URL del ZIP con fat32format.exe (del JSON). Si el exe
        ///   ya existe en la carpeta de la app, se omite la descarga.</param>
        public async Task ParticionarYFormatearAsync(
            int        numeroDisco,
            int        gbEmuMMC,
            string     urlFat32FormatZip,
            IProgress<(int Pct, string Msg)> progreso,
            CancellationToken ct = default)
        {
            // ── PASO 1: Diskpart ─────────────────────────────────────────────────
            // Orden crítico (igual que ReglasLogic / hekate):
            //   • create partition primary size=N  → crea emuMMC (queda seleccionada)
            //   • remove noerr                     → fuerza quitar cualquier letra auto-asignada
            //   • set id=E0                        → tipo desconocido → Windows la ignora
            //   • create partition primary          → crea SWITCH SD (queda seleccionada)
            //   • assign                            → Windows asigna la siguiente letra libre
            // Sin "Verb=runas" porque el manifest ya pide requireAdministrator.
            string script = $@"select disk {numeroDisco}
clean
convert mbr
create partition primary size={gbEmuMMC * 1024}
remove noerr
set id=E0
create partition primary
assign
exit";

            progreso.Report((5, "Preparando diskpart…"));
            ct.ThrowIfCancellationRequested();

            string scriptPath = Path.Combine(Path.GetTempPath(), "nxsuite_diskpart.txt");
            await File.WriteAllTextAsync(scriptPath, script, System.Text.Encoding.ASCII, ct);

            progreso.Report((10, "Particionando disco…"));
            await EjecutarDiskpartAsync(scriptPath, ct);
            progreso.Report((40, "Particiones creadas. Esperando a Windows…"));
            try { File.Delete(scriptPath); } catch { }

            // Windows necesita registrar el nuevo layout antes de que podamos detectar la letra.
            await Task.Delay(3000, ct);

            // ── PASO 2: Detectar la letra de la partición SWITCH SD ──────────────
            // La emuMMC (id=E0) no tiene letra; la SWITCH SD sí (por "assign").
            progreso.Report((45, "Detectando letra de la partición SWITCH SD…"));
            string? letraRaiz = EncontrarLetraEnDisco(numeroDisco);
            if (string.IsNullOrEmpty(letraRaiz))
                throw new InvalidOperationException(
                    "No se detectó ninguna partición con letra asignada en el disco. " +
                    "El paso 'assign' de diskpart pudo haber fallado.");

            char letra = letraRaiz[0]; // ej. 'H'

            // ── PASO 3: Asegurar que fat32format.exe está disponible ─────────────
            progreso.Report((50, "Preparando fat32format.exe…"));
            string exePath = await AsegurarFat32FormatAsync(urlFat32FormatZip, ct);

            // ── PASO 4: Formatear como FAT32 silenciosamente ─────────────────────
            // fat32format.exe acepta "y" por stdin para confirmar el formateo.
            // CreateNoWindow=true → cero ventanas visibles para el usuario.
            progreso.Report((60, $"Formateando {letra}: como FAT32…"));
            var psiFmt = new ProcessStartInfo("cmd.exe", $"/c echo y | \"{exePath}\" {letra}:")
            {
                UseShellExecute  = false,
                CreateNoWindow   = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
            };

            using (var procFmt = Process.Start(psiFmt)
                ?? throw new InvalidOperationException("No se pudo iniciar fat32format.exe."))
            {
                await procFmt.WaitForExitAsync(ct);
                // fat32format devuelve 0 en éxito; cualquier otro código es error.
                if (procFmt.ExitCode != 0)
                {
                    string err = await procFmt.StandardError.ReadToEndAsync(ct);
                    throw new InvalidOperationException(
                        $"fat32format terminó con código {procFmt.ExitCode}. {err}");
                }
            }

            // ── PASO 5: Establecer la etiqueta "SWITCH SD" ───────────────────────
            // Usamos la API de Windows directamente → sin ventanas, sin procesos extra.
            progreso.Report((90, "Aplicando etiqueta SWITCH SD…"));
            await Task.Delay(1500, ct); // pequeña pausa para que Windows monte la partición formateada
            SetVolumeLabel(letraRaiz, "SWITCH SD");

            progreso.Report((100, "Listo"));
        }

        /// <summary>
        /// Recorre todas las unidades del sistema y devuelve la ruta raíz (ej. "H:\")
        /// de la partición con letra asignada que vive en el disco físico indicado.
        /// Funciona con unidades RAW (recién asignadas, sin formatear) porque no
        /// depende de DriveInfo.IsReady.
        /// </summary>
        private string? EncontrarLetraEnDisco(int numeroDisco)
        {
            foreach (DriveInfo d in DriveInfo.GetDrives())
            {
                try
                {
                    if (GetPhysicalDiskNumber(d.Name) == numeroDisco)
                        return d.Name; // ej. "H:\"
                }
                catch { /* la unidad no es accesible, continuamos */ }
            }
            return null;
        }

        /// <summary>
        /// Garantiza que fat32format.exe existe en la carpeta de la aplicación.
        /// Si ya existe lo reutiliza (caché). Si no, lo descarga del ZIP indicado.
        /// </summary>
        private static async Task<string> AsegurarFat32FormatAsync(string urlZip, CancellationToken ct)
        {
            string exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fat32format.exe");
            if (File.Exists(exePath)) return exePath;

            if (string.IsNullOrWhiteSpace(urlZip))
                throw new InvalidOperationException(
                    "fat32format.exe no encontrado y no hay URL de descarga en el JSON " +
                    "(ConfiguracionUI.UrlFat32Format).");

            string zipPath    = Path.Combine(Path.GetTempPath(), "nxsuite_fat32format.zip");
            string tempFolder = Path.Combine(Path.GetTempPath(), "nxsuite_fat32format_tmp");

            try
            {
                // Descarga silenciosa del ZIP.
                using var http = new System.Net.Http.HttpClient();
                http.Timeout = TimeSpan.FromSeconds(60);
                var bytes = await http.GetByteArrayAsync(urlZip, ct);
                await File.WriteAllBytesAsync(zipPath, bytes, ct);

                // Extracción y búsqueda del ejecutable.
                if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true);
                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, tempFolder);

                string? found = Directory.GetFiles(tempFolder, "fat32format.exe", SearchOption.AllDirectories)
                                         .FirstOrDefault();
                if (found == null)
                    throw new InvalidOperationException("El ZIP descargado no contiene fat32format.exe.");

                File.Copy(found, exePath, overwrite: true);
            }
            finally
            {
                try { File.Delete(zipPath); }       catch { }
                try { Directory.Delete(tempFolder, true); } catch { }
            }

            return exePath;
        }

        private static async Task EjecutarDiskpartAsync(string scriptPath, CancellationToken ct)
        {
            // La app tiene requireAdministrator en el manifest, así que diskpart
            // hereda los permisos de admin sin necesitar Verb="runas".
            // CreateNoWindow=true → cero ventanas visibles para el usuario.
            var psi = new ProcessStartInfo("diskpart.exe", $"/s \"{scriptPath}\"")
            {
                UseShellExecute = false,
                CreateNoWindow  = true,
            };

            using var proc = Process.Start(psi)
                ?? throw new InvalidOperationException("No se pudo iniciar diskpart.");

            await proc.WaitForExitAsync(ct);

            // diskpart puede devolver códigos ≠ 0 por advertencias no fatales
            // (ej. "remove noerr" cuando no había letra que quitar).
            // No lanzamos excepción aquí; el éxito se verifica en la detección de letra.
        }

        public void EjecutarSniper(string driveLetter)
        {
            char letter = driveLetter[0];

            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd)) return true;

                StringBuilder className = new StringBuilder(256);
                GetClassName(hWnd, className, 256);

                StringBuilder windowText = new StringBuilder(256);
                GetWindowText(hWnd, windowText, 256);

                string cName = className.ToString();
                string wText = windowText.ToString();

                // Cerrar explorador
                if (cName == "CabinetWClass" && wText.Contains($"{letter}:"))
                {
                    Logger.Info($"[SNIPER] Cerrando explorador: {wText}");
                    PostMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                }
                // Cerrar errores
                else if (cName == "#32770" && (wText == "Microsoft Windows" || wText.Contains("no disponible")))
                {
                    Logger.Info($"[SNIPER] Cerrando error: {wText}");
                    PostMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                }

                return true;
            }, IntPtr.Zero);
        }
    }
}