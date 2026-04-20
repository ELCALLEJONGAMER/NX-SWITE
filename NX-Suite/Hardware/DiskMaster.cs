using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using NX_Suite.Services; // Asegúrate de que esto apunta a donde está tu Logger

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