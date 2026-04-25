using System;
using System.Runtime.InteropServices;
using System.Text;
using NX_Suite.Services;

namespace NX_Suite.Hardware
{
    /// <summary>
    /// "Sniper" de ventanas: cierra automáticamente las ventanas modales que
    /// Windows abre al detectar una unidad RAW recién particionada
    /// (Explorador con la letra, diálogos "Microsoft Windows / no disponible").
    /// </summary>
    public partial class DiskMaster
    {
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
