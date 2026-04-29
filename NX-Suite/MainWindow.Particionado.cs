using NX_Suite.Core.Configuracion;
using NX_Suite.Hardware;
using NX_Suite.Models;
using NX_Suite.UI;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Media.Effects;

namespace NX_Suite
{
    /// <summary>
    /// MainWindow — Handlers del overlay de Particionado y Formateo SD.
    /// Permite particionar una SD directamente (sin instalar módulos) usando
    /// <see cref="ParticionadorDiscos.ParticionarYFormatearAsync"/>.
    /// </summary>
    public partial class MainWindow
    {
        // ?? Estado ????????????????????????????????????????????????????????????

        private SDInfo?                 _sdSelParticionado;
        private int                     _gbEmuMMCParticionado = 12;
        private bool                    _particionandoEnProceso;
        private CancellationTokenSource? _ctsParticionado;

        private static readonly int[] _gbTicksParticionado = { 4, 8, 12, 16, 24, 32, 48, 64 };

        // ?? Blur del fondo ?????????????????????????????????????????????????

        private void AplicarBlurFondo(bool activar)
        {
            Effect efecto = activar ? new BlurEffect { Radius = 6, KernelType = KernelType.Gaussian } : null;
            BarraTopBar.Effect                = efecto;
            PanelLateralIzquierdo.Effect      = efecto;
            GridContenidoCentral.Effect       = efecto;
            GridPanelDerechoContenedor.Effect = efecto;
        }

        // ?? Apertura / cierre ?????????????????????????????????????????????????

        public void AbrirOverlayParticionado()
        {
            _particionandoEnProceso = false;
            _sdSelParticionado = InfoSD.ComboDrives.SelectedItem as SDInfo;
            ActualizarInfoSDEnOverlay();
            ActualizarSliderParticionado((int)SliderGbParticionado.Value);
            AplicarBlurFondo(true);
            PanelParticionadoOverlay.Visibility = Visibility.Visible;
        }

        private void BtnCerrarParticionado_Click(object sender, RoutedEventArgs e)
            => CerrarOverlayParticionado();

        private void PanelParticionado_BackdropClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_particionandoEnProceso) return;
            CerrarOverlayParticionado();
        }

        private void CerrarOverlayParticionado()
        {
            if (_particionandoEnProceso) return;
            _ctsParticionado?.Cancel();
            AplicarBlurFondo(false);
            PanelParticionadoOverlay.Visibility = Visibility.Collapsed;
        }

        // ?? Info SD (sin combo propio — lee desde el panel derecho) ??????????

        private void BtnRefrescarSDParticionado_Click(object sender, RoutedEventArgs e)
        {
            _sdSelParticionado = InfoSD.ComboDrives.SelectedItem as SDInfo;
            ActualizarInfoSDEnOverlay();
        }

        private void ActualizarInfoSDEnOverlay()
        {
            if (_sdSelParticionado == null)
            {
                TxtLetraSDParticionado.Text   = "—";
                TxtNombreSDParticionado.Text  = "Sin SD seleccionada";
                TxtInfoSDParticionado.Text    = "Selecciona una SD en el panel derecho y pulsa Refrescar";
                BtnParticionarAhora.IsEnabled = false;
                return;
            }

            string cap = string.IsNullOrEmpty(_sdSelParticionado.CapacidadTotal) || _sdSelParticionado.CapacidadTotal == "0"
                ? "Tamaño desconocido"
                : $"{_sdSelParticionado.CapacidadTotal} GB";

            TxtLetraSDParticionado.Text   = _sdSelParticionado.Letra.TrimEnd('\\', ':');
            TxtNombreSDParticionado.Text  = string.IsNullOrWhiteSpace(_sdSelParticionado.Etiqueta)
                ? "Sin etiqueta"
                : _sdSelParticionado.Etiqueta;
            TxtInfoSDParticionado.Text    = $"{cap}  •  Disco #{_sdSelParticionado.DiscoFisico}  •  {(string.IsNullOrEmpty(_sdSelParticionado.Formato) ? "RAW" : _sdSelParticionado.Formato)}";
            BtnParticionarAhora.IsEnabled = _sdSelParticionado.DiscoFisico >= 0;
        }

        // ?? Slider emuMMC ?????????????????????????????????????????????????????

        private void SliderGbParticionado_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
            => ActualizarSliderParticionado((int)e.NewValue);

        private void ActualizarSliderParticionado(int indice)
        {
            indice = Math.Clamp(indice, 0, _gbTicksParticionado.Length - 1);
            _gbEmuMMCParticionado = _gbTicksParticionado[indice];

            TxtGbValorParticionado.Text      = $"{_gbEmuMMCParticionado} GB";
            BadgeRecParticionado.Visibility  = (_gbEmuMMCParticionado == 12 || _gbEmuMMCParticionado == 24)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        // ?? Particionado ??????????????????????????????????????????????????????

        private async void BtnParticionarAhora_Click(object sender, RoutedEventArgs e)
        {
            if (_sdSelParticionado == null || _sdSelParticionado.DiscoFisico < 0)
            {
                TxtEstadoParticionado.Text = "? Selecciona una SD válida antes de continuar.";
                return;
            }

            _particionandoEnProceso        = true;
            BtnParticionarAhora.IsEnabled  = false;
            BtnRefrescarSDParticionado.IsEnabled = false;
            SliderGbParticionado.IsEnabled = false;

            ContenedorProgresoParticionado.Visibility = Visibility.Visible;
            _ctsParticionado = new CancellationTokenSource();

            try
            {
                int  disco = _sdSelParticionado.DiscoFisico;
                int  gb    = _gbEmuMMCParticionado;

                var progreso = new Progress<(int Pct, string Msg)>(p =>
                {
                    TxtEstadoParticionado.Text = $"[{p.Pct}%] {p.Msg}";

                    // Actualizar barra de progreso: el ancho máximo del contenedor
                    double maxW = ContenedorProgresoParticionado.ActualWidth;
                    BarraProgresoParticionado.Width = maxW * p.Pct / 100.0;
                });

                string urlFat32 = ConfiguracionRemota.Ui?.UrlFat32Format ?? "";

                var particionador = new ParticionadorDiscos();
                await particionador.ParticionarYFormatearAsync(disco, gb, urlFat32, progreso, _ctsParticionado.Token);

                // Éxito
                BarraProgresoParticionado.Width = ContenedorProgresoParticionado.ActualWidth;
                TxtEstadoParticionado.Text = $"? SD particionada — SWITCH SD (FAT32) + emuMMC ({gb} GB, tipo E0)";

                await System.Threading.Tasks.Task.Delay(800, _ctsParticionado.Token);
                await ActualizarListaUnidadesAsync();

                AplicarBlurFondo(false);
                PanelParticionadoOverlay.Visibility = Visibility.Collapsed;
                Dialogos.Info(
                    $"La SD ha sido particionada correctamente.\n\n" +
                    $"• SWITCH SD — FAT32, etiqueta \"SWITCH SD\"\n" +
                    $"• emuMMC    — {gb} GB, tipo E0 (invisible para Windows)",
                    "Particionado completado ?");
            }
            catch (OperationCanceledException)
            {
                TxtEstadoParticionado.Text = "Cancelado por el usuario.";
            }
            catch (Exception ex)
            {
                TxtEstadoParticionado.Text = $"? Error: {ex.Message}";
                Dialogos.Error($"Error durante el particionado:\n\n{ex.Message}");
            }
            finally
            {
                _particionandoEnProceso              = false;
                BtnParticionarAhora.IsEnabled        = _sdSelParticionado?.DiscoFisico >= 0;
                BtnRefrescarSDParticionado.IsEnabled = true;
                SliderGbParticionado.IsEnabled       = true;
            }
        }
    }
}
