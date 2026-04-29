using System;
using System.Windows;
using System.Windows.Interop;

namespace NX_Suite.Hardware
{
    /// <summary>
    /// Detección automática Plug &amp; Play de unidades extraíbles. Engancha un
    /// hook a la ventana indicada y dispara <see cref="UnidadConectada"/> cuando
    /// Windows notifica conexión o desconexión de cualquier dispositivo.
    /// </summary>
    public class NotificadorDiscos
    {
        /// <summary>Disparado tanto en conexión como en desconexión (refresco genérico).</summary>
        public event EventHandler? UnidadConectada;

        /// <summary>Disparado únicamente cuando se desconecta una unidad.</summary>
        public event EventHandler? UnidadDesconectada;

        private const int WM_DEVICECHANGE          = 0x0219;
        private const int DBT_DEVICEARRIVAL        = 0x8000;
        private const int DBT_DEVICEREMOVECOMPLETE = 0x8004;

        private Window? _ventana;

        public void IniciarEscucha(Window ventana)
        {
            _ventana = ventana;
            _ventana.SourceInitialized += (s, e) =>
            {
                HwndSource? source = HwndSource.FromHwnd(new WindowInteropHelper(_ventana!).Handle);
                source?.AddHook(HwndHandler);
            };
        }

        private IntPtr HwndHandler(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_DEVICECHANGE)
            {
                int tipo = wParam.ToInt32();
                if (tipo == DBT_DEVICEARRIVAL || tipo == DBT_DEVICEREMOVECOMPLETE)
                {
                    UnidadConectada?.Invoke(this, EventArgs.Empty);

                    if (tipo == DBT_DEVICEREMOVECOMPLETE)
                        UnidadDesconectada?.Invoke(this, EventArgs.Empty);
                }
            }
            return IntPtr.Zero;
        }
    }
}
