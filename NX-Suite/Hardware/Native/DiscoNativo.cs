using System;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace NX_Suite.Hardware.Native
{
    /// <summary>
    /// P/Invoke compartido para operaciones de disco de bajo nivel:
    /// resolver letras ? disco físico, lock/dismount de volúmenes,
    /// y el "Sniper" que cierra forzosamente todos los handles del sistema
    /// que apuntan a un volumen — la única forma fiable de eliminar los
    /// handles persistentes de SearchIndexer, Defender, etc.
    /// </summary>
    internal static class DiscoNativo
    {
        // ?? Constantes Win32 ?????????????????????????????????????????????????
        private const uint GENERIC_READ           = 0x80000000;
        private const uint GENERIC_WRITE          = 0x40000000;
        private const uint FILE_SHARE_READ        = 0x00000001;
        private const uint FILE_SHARE_WRITE       = 0x00000002;
        private const uint OPEN_EXISTING          = 3;
        private const uint FSCTL_LOCK_VOLUME      = 0x00090018;
        private const uint FSCTL_DISMOUNT_VOLUME  = 0x00090020;
        private const uint PROCESS_DUP_HANDLE     = 0x0040;
        private const uint DUPLICATE_CLOSE_SOURCE = 0x00000001;
        private const int  STATUS_INFO_LENGTH_MISMATCH = unchecked((int)0xC0000004);
        private static readonly IntPtr INVALID_HANDLE_VALUE = new(-1);

        // ?? P/Invoke: kernel32 ???????????????????????????????????????????????
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool DeviceIoControl(
            IntPtr hDevice,
            uint dwIoControlCode,
            IntPtr lpInBuffer,
            uint nInBufferSize,
            ref STORAGE_DEVICE_NUMBER lpOutBuffer,
            uint nOutBufferSize,
            out uint lpBytesReturned,
            IntPtr lpOverlapped);

        // Sobrecarga sin buffer de salida: usada por LOCK / DISMOUNT
        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool DeviceIoControl(
            IntPtr hDevice,
            uint dwIoControlCode,
            IntPtr lpInBuffer,
            uint nInBufferSize,
            IntPtr lpOutBuffer,
            uint nOutBufferSize,
            out uint lpBytesReturned,
            IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DuplicateHandle(
            IntPtr hSourceProcess, IntPtr hSourceHandle,
            IntPtr hTargetProcess, out IntPtr lpTargetHandle,
            uint dwDesiredAccess, bool bInheritHandle, uint dwOptions);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint QueryDosDevice(string lpDeviceName, StringBuilder lpTargetPath, int ucchMax);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern bool SetVolumeLabel(string lpRootPathName, string lpVolumeName);

        // ?? user32: EnumWindows para cerrar ventanas Explorer ????????????????
        private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hwnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hwnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hwnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        private const uint WM_CLOSE = 0x0010;

        // ?? P/Invoke: ntdll ??????????????????????????????????????????????????
        [DllImport("ntdll.dll")]
        private static extern int NtQuerySystemInformation(
            int SystemInformationClass,
            IntPtr SystemInformation,
            int SystemInformationLength,
            out int ReturnLength);

        [DllImport("ntdll.dll")]
        private static extern int NtQueryObject(
            IntPtr Handle,
            int ObjectInformationClass,
            IntPtr ObjectInformation,
            int ObjectInformationLength,
            out int ReturnLength);

        // ?? Estructuras ??????????????????????????????????????????????????????
        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_HANDLE_ENTRY
        {
            public int  OwnerPid;
            public byte ObjectTypeNumber;
            public byte Flags;
            public ushort HandleValue;
            public IntPtr Object;
            public int  GrantedAccess;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct STORAGE_DEVICE_NUMBER
        {
            public int DeviceType;
            public int DeviceNumber;
            public int PartitionNumber;
        }

        /// <summary>
        /// Devuelve el índice del disco físico al que pertenece la letra de unidad
        /// indicada (ej. <c>"E:\"</c>) o <c>-1</c> si no se pudo determinar.
        /// </summary>
        internal static int GetPhysicalDiskNumber(string driveLetter)
        {
            string path       = driveLetter.TrimEnd('\\');
            string devicePath = $@"\\.\{path}";

            IntPtr hDevice = CreateFile(devicePath, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
            if (hDevice == new IntPtr(-1)) return -1;

            STORAGE_DEVICE_NUMBER sdn = new STORAGE_DEVICE_NUMBER();
            const uint IOCTL_STORAGE_GET_DEVICE_NUMBER = 0x2D1080;

            bool success = DeviceIoControl(
                hDevice,
                IOCTL_STORAGE_GET_DEVICE_NUMBER,
                IntPtr.Zero, 0,
                ref sdn,
                (uint)Marshal.SizeOf(sdn),
                out uint _,
                IntPtr.Zero);

            CloseHandle(hDevice);
            return success ? sdn.DeviceNumber : -1;
        }

        /// <summary>
        /// Abre el volumen <c>\\.\X:</c>, lo bloquea (FSCTL_LOCK_VOLUME) y lo
        /// desmonta (FSCTL_DISMOUNT_VOLUME). Esto libera todos los handles que
        /// Windows mantiene sobre el volumen (Explorer, indexador de búsqueda,
        /// antivirus, miniaturas) y deja la unidad disponible para acceso
        /// exclusivo de bajo nivel — exactamente lo que necesita <c>fat32format</c>.
        ///
        /// La letra de unidad SE MANTIENE asignada; al primer acceso Windows
        /// la remontará automáticamente. Por eso es seguro usarlo justo antes
        /// de un formateo: no rompe la enumeración de unidades.
        ///
        /// Devuelve <c>true</c> si el lock + dismount fue exitoso. El handle
        /// devuelto en <paramref name="handle"/> debe cerrarse con
        /// <see cref="CerrarHandle"/> JUSTO ANTES de ejecutar el formateador
        /// para mantener el lock vivo el mayor tiempo posible.
        /// </summary>
        internal static bool BloquearYDesmontarVolumen(string letraRaiz, out IntPtr handle, int reintentosLock = 20)
        {
            handle = INVALID_HANDLE_VALUE;
            string devicePath = $@"\\.\{letraRaiz.TrimEnd('\\').TrimEnd(':')}:";

            handle = CreateFile(devicePath,
                GENERIC_READ | GENERIC_WRITE,
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero,
                OPEN_EXISTING,
                0,
                IntPtr.Zero);

            if (handle == INVALID_HANDLE_VALUE)
            {
                Debug.WriteLine($"[Volumen] CreateFile falló para {devicePath}: GLE={Marshal.GetLastWin32Error()}");
                return false;
            }

            // Lock con reintentos — best-effort: si algún proceso transitorio suelta
            // sus handles durante la espera, obtenemos lock exclusivo.
            bool locked = false;
            for (int i = 0; i < reintentosLock; i++)
            {
                if (DeviceIoControl(handle, FSCTL_LOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero))
                {
                    locked = true;
                    break;
                }
                Thread.Sleep(500);
            }

            if (!locked)
                Debug.WriteLine($"[Volumen] Lock no obtenido en {letraRaiz} tras {reintentosLock} intentos " +
                                $"(GLE={Marshal.GetLastWin32Error()}). Se forzará dismount de todas formas.");

            // FSCTL_DISMOUNT_VOLUME sin lock previo es soportado por Windows y fuerza
            // la desconexión del filesystem del volumen aunque otro proceso tenga handles
            // abiertos. Esos handles quedan inválidos — el volumen queda "crudo" (raw)
            // y listo para acceso de bajo nivel por parte del formateador.
            if (!DeviceIoControl(handle, FSCTL_DISMOUNT_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero))
            {
                Debug.WriteLine($"[Volumen] FSCTL_DISMOUNT_VOLUME falló para {letraRaiz}: GLE={Marshal.GetLastWin32Error()}");
                CloseHandle(handle);
                handle = INVALID_HANDLE_VALUE;
                return false;
            }

            Debug.WriteLine($"[Volumen] {letraRaiz} desmontado (lock={locked}).");
            return true;
        }

        /// <summary>
        /// Cierra un handle obtenido vía <see cref="BloquearYDesmontarVolumen"/>.
        /// Liberar el handle libera implícitamente el lock del volumen.
        /// </summary>
        internal static void CerrarHandle(IntPtr handle)
        {
            if (handle != IntPtr.Zero && handle != INVALID_HANDLE_VALUE)
                CloseHandle(handle);
        }

        /// <summary>
        /// Cierra ventanas del Explorador de Windows que muestren la unidad indicada,
        /// y diálogos estándar de Windows relacionados con la misma.
        /// Implementación idéntica al C++ de referencia: usa <c>EnumWindows</c>
        /// con <c>PostMessage(WM_CLOSE)</c> sobre ventanas <c>CabinetWClass</c> y
        /// <c>#32770</c> cuyo título contenga la letra de unidad.
        /// </summary>
        internal static void CerrarVentanasExplorer(char letra)
        {
            string target = $"{char.ToUpper(letra)}:";
            EnumWindows((hwnd, _) =>
            {
                if (!IsWindowVisible(hwnd)) return true;

                var cls   = new System.Text.StringBuilder(256);
                var title = new System.Text.StringBuilder(256);
                GetClassName(hwnd, cls, cls.Capacity);
                GetWindowText(hwnd, title, title.Capacity);

                string clsStr   = cls.ToString();
                string titleStr = title.ToString();

                // Ventanas del Explorador (CabinetWClass) con la letra en el título
                if (clsStr == "CabinetWClass" &&
                    titleStr.Contains(target, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.WriteLine($"[Sniper] Cerrando Explorer: [{titleStr}]");
                    PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                }

                // Diálogos de sistema (#32770) relacionados con la unidad
                if (clsStr == "#32770" &&
                    (titleStr == "Microsoft Windows" ||
                     titleStr.Contains("no disponible", StringComparison.OrdinalIgnoreCase) ||
                     titleStr.Contains("not available",  StringComparison.OrdinalIgnoreCase)))
                {
                    Debug.WriteLine($"[Sniper] Cerrando diálogo: [{titleStr}]");
                    PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                }

                return true; // continuar enumeración
            }, IntPtr.Zero);
        }

        /// <summary>
        /// apuntan al volumen <paramref name="letraRaiz"/> (ej. "I:\").
        /// Usa <c>NtQuerySystemInformation(SystemHandleInformation)</c> para
        /// enumerar los handles de todos los procesos y <c>DuplicateHandle</c>
        /// con <c>DUPLICATE_CLOSE_SOURCE</c> para cerrarlos en el proceso remoto
        /// sin necesidad de inyectar código.
        ///
        /// Este es el mismo mecanismo que usan handle.exe (Sysinternals) y
        /// Unlocker. Es la única forma 100% fiable de liberar handles
        /// persistentes de SearchIndexer, Defender y el propio kernel de Windows.
        /// </summary>
        internal static void EjecutarSniper(string letraRaiz)
        {
            // Resolver la ruta de dispositivo NT para la letra (ej. \Device\HarddiskVolume5)
            string letra  = letraRaiz.TrimEnd('\\').TrimEnd(':');
            var sbTarget  = new StringBuilder(512);
            if (QueryDosDevice(letra + ":", sbTarget, sbTarget.Capacity) == 0)
            {
                Debug.WriteLine($"[Sniper] QueryDosDevice falló para {letra}: GLE={Marshal.GetLastWin32Error()}");
                return;
            }
            string targetDevice = sbTarget.ToString(); // ej. \Device\HarddiskVolume5
            Debug.WriteLine($"[Sniper] Dispositivo objetivo: {targetDevice}");

            // Obtener la tabla de handles del sistema con NtQuerySystemInformation(0x10)
            int bufSize = 1 << 20; // 1 MB inicial
            IntPtr buf  = IntPtr.Zero;
            int    ret, retLen;
            try
            {
                while (true)
                {
                    buf = Marshal.AllocHGlobal(bufSize);
                    ret = NtQuerySystemInformation(0x10, buf, bufSize, out retLen);
                    if (ret == STATUS_INFO_LENGTH_MISMATCH)
                    {
                        Marshal.FreeHGlobal(buf);
                        buf     = IntPtr.Zero;
                        bufSize = retLen + 4096;
                        continue;
                    }
                    break;
                }

                if (ret != 0)
                {
                    Debug.WriteLine($"[Sniper] NtQuerySystemInformation falló: 0x{ret:X}");
                    return;
                }

                // La estructura devuelta es: DWORD count, SYSTEM_HANDLE_ENTRY[count]
                int count       = Marshal.ReadInt32(buf);
                int entrySize   = Marshal.SizeOf<SYSTEM_HANDLE_ENTRY>();
                IntPtr myProc   = GetCurrentProcess();
                int    myPid    = Process.GetCurrentProcess().Id;

                var handlesCerrados = new HashSet<(int pid, ushort h)>();

                for (int i = 0; i < count; i++)
                {
                    IntPtr entryPtr = buf + 4 + i * entrySize;
                    var    entry    = Marshal.PtrToStructure<SYSTEM_HANDLE_ENTRY>(entryPtr);

                    // Saltar handles del propio proceso y handles sin acceso de lectura
                    if (entry.OwnerPid == myPid) continue;
                    if ((entry.GrantedAccess & 0x0012019F) == 0) continue; // sin lectura de archivo

                    var key = (entry.OwnerPid, entry.HandleValue);
                    if (handlesCerrados.Contains(key)) continue;

                    IntPtr hProc = OpenProcess(PROCESS_DUP_HANDLE, false, entry.OwnerPid);
                    if (hProc == IntPtr.Zero || hProc == INVALID_HANDLE_VALUE) continue;

                    try
                    {
                        // Duplicar el handle en nuestro proceso para poder inspeccionarlo
                        if (!DuplicateHandle(hProc, (IntPtr)entry.HandleValue,
                                             myProc, out IntPtr hDup,
                                             0, false, 0))
                            continue;

                        try
                        {
                            // Obtener el nombre del objeto apuntado por el handle
                            string? nombre = ObtenerNombreHandle(hDup);
                            if (nombre == null || !nombre.StartsWith(targetDevice,
                                                   StringComparison.OrdinalIgnoreCase))
                                continue;

                            // ? El handle apunta a nuestro volumen ? cerrarlo en el proceso remoto
                            // DUPLICATE_CLOSE_SOURCE cierra el handle en el proceso origen
                            DuplicateHandle(hProc, (IntPtr)entry.HandleValue,
                                            myProc, out _,
                                            0, false, DUPLICATE_CLOSE_SOURCE);

                            handlesCerrados.Add(key);
                            Debug.WriteLine($"[Sniper] Cerrado handle 0x{entry.HandleValue:X} " +
                                            $"en PID {entry.OwnerPid} ? {nombre}");
                        }
                        finally
                        {
                            CloseHandle(hDup);
                        }
                    }
                    finally
                    {
                        CloseHandle(hProc);
                    }
                }

                Debug.WriteLine($"[Sniper] Total handles cerrados: {handlesCerrados.Count}");
            }
            finally
            {
                if (buf != IntPtr.Zero) Marshal.FreeHGlobal(buf);
            }
        }

        /// <summary>
        /// Obtiene el nombre NT del objeto apuntado por un handle local usando
        /// NtQueryObject(ObjectNameInformation = 1).
        /// </summary>
        private static string? ObtenerNombreHandle(IntPtr handle)
        {
            // Tamaño inicial generoso para evitar truncamiento
            int bufSize = 512;
            IntPtr buf  = Marshal.AllocHGlobal(bufSize);
            try
            {
                int status = NtQueryObject(handle, 1, buf, bufSize, out int needed);
                if (status != 0 && needed > bufSize)
                {
                    Marshal.FreeHGlobal(buf);
                    buf    = Marshal.AllocHGlobal(needed);
                    status = NtQueryObject(handle, 1, buf, needed, out _);
                }
                if (status != 0) return null;

                // UNICODE_STRING: Length (ushort) + MaxLength (ushort) + Buffer (IntPtr)
                int    len    = Marshal.ReadInt16(buf);
                IntPtr strBuf = Marshal.ReadIntPtr(buf, 4);
                if (len == 0 || strBuf == IntPtr.Zero) return null;
                return Marshal.PtrToStringUni(strBuf, len / 2);
            }
            catch
            {
                return null;
            }
            finally
            {
                Marshal.FreeHGlobal(buf);
            }
        }
    }
}