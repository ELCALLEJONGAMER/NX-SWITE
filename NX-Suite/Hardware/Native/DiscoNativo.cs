using System;
using System.Runtime.InteropServices;

namespace NX_Suite.Hardware.Native
{
    /// <summary>
    /// P/Invoke compartido para resolver letras de unidad ? índice de disco
    /// físico (vía <c>IOCTL_STORAGE_GET_DEVICE_NUMBER</c>) y aplicar etiquetas
    /// de volumen sin abrir ventanas. Lo consumen <see cref="EscanerDiscos"/> y
    /// <see cref="ParticionadorDiscos"/>.
    /// </summary>
    internal static class DiscoNativo
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

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern bool SetVolumeLabel(string lpRootPathName, string lpVolumeName);

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
    }
}
