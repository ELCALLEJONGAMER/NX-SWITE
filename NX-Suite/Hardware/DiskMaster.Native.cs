using System;
using System.Runtime.InteropServices;

namespace NX_Suite.Hardware
{
    /// <summary>
    /// P/Invokes y structs nativos de Windows compartidos por todas las partes
    /// de <see cref="DiskMaster"/> que necesitan obtener el índice de disco físico
    /// de una letra de unidad (escaneo de unidades y particionado).
    /// </summary>
    public partial class DiskMaster
    {
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

        /// <summary>
        /// Devuelve el índice del disco físico al que pertenece la letra de unidad
        /// indicada (ej. "E:\") o -1 si no se pudo determinar.
        /// </summary>
        private int GetPhysicalDiskNumber(string driveLetter)
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
    }
}
