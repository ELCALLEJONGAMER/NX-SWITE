using Microsoft.Win32;
using NX_Suite.Core;
using NX_Suite.Core.Configuracion;
using NX_Suite.Models;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NX_Suite
{
    /// <summary>
    /// MainWindow — Overlay de actualización de firmware RP2040 (Picofly).
    ///
    /// Se abre automáticamente al detectar el chip vía <see cref="NotificadorDiscos"/>
    /// y también de forma manual desde el botón <c>BtnRp2040</c> de la TopBar.
    /// </summary>
    public partial class MainWindow
    {
        private readonly Rp2040Logic _rp2040 = new();
        private string? _letraRp2040Actual;
        private CancellationTokenSource? _ctRp2040;

        // ?? API nativa para cerrar la ventana del Explorador que abre AutoPlay ??

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const uint WM_CLOSE = 0x0010;

        /// <summary>
        /// Cierra la ventana del Explorador de Windows que AutoPlay abre al
        /// conectar el RP2040 (título "RPI-RP2 (X:)"). Espera hasta 2 s para
        /// darle tiempo a que aparezca antes de intentarlo.
        /// </summary>
        private static async Task CerrarVentanaExploradorRp2040Async(string letra)
        {
            // Windows puede tardar un momento en abrir la ventana del Explorador
            for (int intento = 0; intento < 8; intento++)
            {
                await Task.Delay(250);
                string titulo = $"RPI-RP2 ({letra}:)";
                IntPtr hWnd = FindWindow("CabinetWClass", titulo);
                if (hWnd != IntPtr.Zero)
                {
                    SendMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                    return;
                }
            }
        }

        // ?? Mantener NX-Suite al frente temporalmente ?????????????????????

        /// <summary>
        /// Pone la ventana en <c>Topmost</c> durante <paramref name="ms"/> ms y
        /// luego la restaura. Garantiza que el overlay del RP2040 quede por
        /// delante del Explorador de Windows que AutoPlay abre al conectar el chip.
        /// </summary>
        private async Task PermanecerAlFrenteAsync(int ms = 4000)
        {
            Topmost = true;
            Activate();
            await Task.Delay(ms);
            Topmost = false;
        }

        // ?? Detección automática ??????????????????????????????????????????

        /// <summary>
        /// Llamado desde el handler de <see cref="NotificadorDiscos.UnidadConectada"/>
        /// cada vez que se conecta una unidad.
        /// </summary>
        internal void ComprobarRp2040Conectado()
        {
            string? letra = _rp2040.DetectarLetraRp2040();
            if (letra == null) return;

            _letraRp2040Actual = letra;

            Dispatcher.InvokeAsync(() =>
            {
                AbrirOverlayRp2040();
                // Quedarse al frente mientras AutoPlay intenta abrir el Explorador,
                // y también intentar cerrar esa ventana en paralelo.
                _ = PermanecerAlFrenteAsync(4000);
                _ = CerrarVentanaExploradorRp2040Async(letra);
            });
        }

        // ?? Apertura / cierre ?????????????????????????????????????????????

        private void BtnRp2040_Click(object sender, RoutedEventArgs e)
        {
            _letraRp2040Actual = _rp2040.DetectarLetraRp2040();
            AbrirOverlayRp2040();
        }

        private void BtnCerrarRp2040_Click(object sender, RoutedEventArgs e)
            => CerrarOverlayRp2040();

        private void PanelRp2040_BackdropClick(object sender, MouseButtonEventArgs e)
            => CerrarOverlayRp2040();

        internal void AbrirOverlayRp2040()
        {
            RefrescarEstadoRp2040();
            MostrarOverlayRp2040();
        }

        internal void CerrarOverlayRp2040()
        {
            _ctRp2040?.Cancel();
            OcultarOverlayRp2040();
        }

        // ?? Animación — mismo patrón que MostrarOverlayLog / OcultarOverlayLog ??

        private void MostrarOverlayRp2040()
        {
            AplicarBlurFondo(true);
            PanelRp2040Overlay.Visibility = Visibility.Visible;
            PanelRp2040Overlay.Opacity    = 0;

            ContenidoRp2040Overlay.RenderTransformOrigin = new Point(0.5, 0.5);
            ContenidoRp2040Overlay.RenderTransform       = new ScaleTransform(0.96, 0.96);

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
            var scaleX = new DoubleAnimation(0.96, 1.0, TimeSpan.FromMilliseconds(200))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            var scaleY = new DoubleAnimation(0.96, 1.0, TimeSpan.FromMilliseconds(200))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };

            PanelRp2040Overlay.BeginAnimation(OpacityProperty, fadeIn);
            ContenidoRp2040Overlay.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
            ContenidoRp2040Overlay.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
        }

        private void OcultarOverlayRp2040()
        {
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(160));
            fadeOut.Completed += (_, _) =>
            {
                PanelRp2040Overlay.Visibility = Visibility.Collapsed;
                AplicarBlurFondo(false);
            };
            PanelRp2040Overlay.BeginAnimation(OpacityProperty, fadeOut);
        }

        // ?? Estado del chip ???????????????????????????????????????????????

        private void RefrescarEstadoRp2040()
        {
            string urlFirmware   = ConfiguracionRemota.Ui.UrlFirmwareRp2040;
            string versionRemota = ConfiguracionRemota.Ui.VersionFirmwareRp2040;

            bool hayChip = _letraRp2040Actual != null;

            // Badge ON / OFF
            TxtRp2040EstadoBadge.Text = hayChip ? "ON" : "OFF";
            BadgeEstadoChip.Background = hayChip
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1400FFCC"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A1A28"));
            TxtRp2040EstadoBadge.Foreground = hayChip
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00FFCC"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#505060"));

            // Título de estado
            TxtRp2040Estado.Text = hayChip ? "Chip conectado" : "Chip no conectado";
            TxtRp2040Estado.Foreground = hayChip
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E0F0"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#505060"));

            // Modelo del chip (solo si está conectado)
            if (hayChip)
            {
                string? modeloChip = _rp2040.LeerVersionFirmware(_letraRp2040Actual!);
                TxtRp2040VersionActual.Text       = modeloChip != null
                    ? $"Modelo: {modeloChip}"
                    : "Modelo: no detectado";
                TxtRp2040VersionActual.Visibility = Visibility.Visible;
            }
            else
            {
                TxtRp2040VersionActual.Visibility = Visibility.Collapsed;
            }

            // Versión disponible
            TxtRp2040VersionRemota.Text = string.IsNullOrEmpty(versionRemota)
                ? "—"
                : versionRemota;

            // Icono caché visible si el firmware está descargado y es válido
            bool enCache = Rp2040Logic.FirmwareDisponibleEnCache(versionRemota);
            PanelIconoCacheRp2040.Visibility = enCache ? Visibility.Visible : Visibility.Collapsed;

            // Ocultar panel de progreso al abrir
            PanelProgresoRp2040.Visibility = Visibility.Collapsed;

            // Estado de los botones
            BtnFlashearRp2040.IsEnabled = hayChip && !string.IsNullOrEmpty(urlFirmware);
            BtnGuardarRp2040.IsEnabled  = !string.IsNullOrEmpty(urlFirmware);
        }

        // ?? Acciones ??????????????????????????????????????????????????????

        private async void BtnFlashearRp2040_Click(object sender, RoutedEventArgs e)
        {
            string urlFirmware = ConfiguracionRemota.Ui.UrlFirmwareRp2040;
            if (string.IsNullOrEmpty(urlFirmware) || _letraRp2040Actual == null) return;

            _ctRp2040 = new CancellationTokenSource();
            SetRp2040Operando(true, "Descargando firmware…");

            var progreso = new Progress<EstadoProgreso>(p =>
                Dispatcher.InvokeAsync(() =>
                {
                    TxtRp2040Progreso.Text = p.TareaActual;
                    AnimarBarraProgreso(p.Porcentaje);
                }));

            var resultado = await _rp2040.FlashearAsync(
                _letraRp2040Actual, urlFirmware, ConfiguracionRemota.Ui.VersionFirmwareRp2040,
                progreso, _ctRp2040.Token);

            SetRp2040Operando(false, string.Empty);

            if (resultado.Exito)
            {
                TxtRp2040Progreso.Text = "Firmware enviado. El chip se reiniciará automáticamente.";
                AnimarBarraProgreso(100);
                // Actualizar icono caché tras descarga exitosa
                PanelIconoCacheRp2040.Visibility = Visibility.Visible;
                Servicios.Sonidos.Reproducir(EventoSonido.Exito);
                await Task.Delay(2500);
                CerrarOverlayRp2040();
            }
            else
            {
                TxtRp2040Progreso.Text = $"Error: {resultado.MensajeError}";
                Servicios.Sonidos.Reproducir(EventoSonido.Error);
            }
        }

        private async void BtnGuardarRp2040_Click(object sender, RoutedEventArgs e)
        {
            string urlFirmware = ConfiguracionRemota.Ui.UrlFirmwareRp2040;
            if (string.IsNullOrEmpty(urlFirmware)) return;

            var dlg = new SaveFileDialog
            {
                Title            = "Guardar firmware Picofly",
                Filter           = "Firmware UF2 (*.uf2)|*.uf2",
                FileName         = "picofly_firmware.uf2",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                                   + "\\Downloads"
            };

            if (dlg.ShowDialog() != true) return;

            _ctRp2040 = new CancellationTokenSource();
            SetRp2040Operando(true, "Descargando firmware…");

            var progreso = new Progress<EstadoProgreso>(p =>
                Dispatcher.InvokeAsync(() =>
                {
                    TxtRp2040Progreso.Text = p.TareaActual;
                    AnimarBarraProgreso(p.Porcentaje);
                }));

            var resultado = await _rp2040.GuardarEnPcAsync(
                urlFirmware, ConfiguracionRemota.Ui.VersionFirmwareRp2040,
                dlg.FileName, progreso, _ctRp2040.Token);

            SetRp2040Operando(false, string.Empty);
            TxtRp2040Progreso.Text = resultado.Exito
                ? "Firmware guardado correctamente."
                : $"Error: {resultado.MensajeError}";
            if (resultado.Exito)
            {
                AnimarBarraProgreso(100);
                PanelIconoCacheRp2040.Visibility = Visibility.Visible;
            }
        }

        private void SetRp2040Operando(bool operando, string mensaje)
        {
            BtnFlashearRp2040.IsEnabled    = !operando;
            BtnGuardarRp2040.IsEnabled     = !operando;
            PanelProgresoRp2040.Visibility = operando ? Visibility.Visible : Visibility.Visible;
            TxtRp2040Progreso.Text         = mensaje;
            if (operando)
                AnimarBarraProgreso(0);
        }

        private void AnimarBarraProgreso(double porcentaje)
        {
            // Calcula el ancho proporcional al contenedor (ancho del overlay - márgenes)
            double anchoContenedor = ContenidoRp2040Overlay.ActualWidth - 48 - 28; // paddings
            if (anchoContenedor <= 0) anchoContenedor = 400;
            double anchoObjetivo = anchoContenedor * Math.Max(0, Math.Min(100, porcentaje)) / 100.0;

            var anim = new DoubleAnimation(anchoObjetivo, TimeSpan.FromMilliseconds(200))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            BarraProgresoRp2040.BeginAnimation(WidthProperty, anim);
        }
    }
}
