using NX_Suite.Hardware;
using NX_Suite.Core;
using NX_Suite.Models;
using NX_Suite.UI;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace NX_Suite
{
    /// <summary>
    /// MainWindow — Handlers del overlay de Particionado y Formateo SD
    /// (rediseñado). Lee la SD del panel derecho, no muestra ? ni Refrescar
    /// y delega el progreso al <c>OverlayCarga</c> global. El SafeButton
    /// "FORMATEAR Y PARTICIONAR" requiere mantener pulsado 2 segundos para
    /// confirmar la operación destructiva.
    /// </summary>
    public partial class MainWindow
    {
        // ?? Estado ???????????????????????????????????????????????????????????

        private SDInfo?                 _sdSelParticionado;
        private int                     _gbEmuMMCParticionado = 12;
        private bool                    _particionandoEnProceso;
        private CancellationTokenSource? _ctsParticionado;

        private static readonly int[] _gbTicksParticionado = { 4, 8, 12, 16, 24, 32, 48, 64 };

        // ?? Apertura / cierre ????????????????????????????????????????????????

        public void AbrirOverlayParticionado()
        {
            _particionandoEnProceso = false;
            _sdSelParticionado = InfoSD.ComboDrives.SelectedItem as SDInfo;
            ActualizarInfoSDParticionado();
            ActualizarSliderParticionado((int)SliderGbParticionado.Value);
            MostrarOverlayConAnimacion(PanelParticionadoOverlay);
        }

        private void PanelParticionado_BackdropClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_particionandoEnProceso) return;
            CerrarOverlayParticionado();
        }

        internal void CerrarOverlayParticionado()
        {
            if (_particionandoEnProceso) return;
            _ctsParticionado?.Cancel();
            AplicarBlurFondo(false);
            PanelParticionadoOverlay.Visibility = Visibility.Collapsed;
        }

        // ?? Pintado de la tarjeta SD desde el panel derecho ??????????????????

        private void ActualizarInfoSDParticionado()
        {
            if (_sdSelParticionado == null || _sdSelParticionado.DiscoFisico < 0)
            {
                TxtLetraSDParticionado.Text  = "—";
                TxtNombreSDParticionado.Text = "Sin SD seleccionada";
                TxtInfoSDParticionado.Text   = "Selecciona una SD en el panel derecho";
                AvisoSinSDParticionado.Visibility = Visibility.Visible;
                BtnParticionarAhora.IsEnabled = false;
                TxtEstadoParticionado.Text = "Conecta o selecciona una microSD para continuar";
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

            AvisoSinSDParticionado.Visibility = Visibility.Collapsed;
            BtnParticionarAhora.IsEnabled = true;
            TxtEstadoParticionado.Text = "Mantén pulsado FORMATEAR Y PARTICIONAR para confirmar";
        }

        // ?? Slider emuMMC ????????????????????????????????????????????????????

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

        // ?? Acción principal: particionar ????????????????????????????????????

        private async void BtnParticionarAhora_Click(object sender, RoutedEventArgs e)
        {
            // Releer la SD por si el usuario cambió la selección en el panel derecho
            _sdSelParticionado = InfoSD.ComboDrives.SelectedItem as SDInfo;
            if (_sdSelParticionado == null || _sdSelParticionado.DiscoFisico < 0)
            {
                ActualizarInfoSDParticionado();
                return;
            }

            _particionandoEnProceso = true;
            _ctsParticionado = new CancellationTokenSource();

            int  disco = _sdSelParticionado.DiscoFisico;
            int  gb    = _gbEmuMMCParticionado;

            // Cierra el overlay y da paso al OverlayCarga global (bloqueante).
            // El blur del fondo lo gestiona internamente _pantallaCarga.Mostrar().
            AplicarBlurFondo(false);
            PanelParticionadoOverlay.Visibility = Visibility.Collapsed;
            _pantallaCarga.Mostrar($"Particionando disco #{disco} — emuMMC: {gb} GB");

            try
            {
                var reportador = _pantallaCarga.ObtenerReportador();
                var progreso = new Progress<(int Pct, string Msg)>(p =>
                    reportador.Report(new EstadoProgreso
                    {
                        Porcentaje  = p.Pct,
                        TareaActual = p.Msg,
                        PasoActual  = 0
                    }));

                string urlFat32 = NX_Suite.Core.Configuracion.ConfiguracionRemota.Ui?.UrlFat32Format ?? string.Empty;

                var particionador = new ParticionadorDiscos();
                await particionador.ParticionarYFormatearAsync(disco, gb, urlFat32, progreso, _ctsParticionado.Token);

                await Task.Delay(800, _ctsParticionado.Token);
                await ActualizarListaUnidadesAsync();

                _pantallaCarga.Ocultar();
                Servicios.Sonidos.Reproducir(EventoSonido.Exito);
                Dialogos.Info(
                    $"La SD ha sido particionada correctamente.\n\n" +
                    $"• SWITCH SD — FAT32, etiqueta \"SWITCH SD\"\n" +
                    $"• emuMMC    — {gb} GB, tipo E0 (invisible para Windows)",
                    "Particionado completado");
            }
            catch (OperationCanceledException)
            {
                _pantallaCarga.Ocultar();
            }
            catch (Exception ex)
            {
                _pantallaCarga.Ocultar();
                Dialogos.Error($"Error durante el particionado:\n\n{ex.Message}", "Fallo");
            }
            finally
            {
                _particionandoEnProceso = false;
                _ctsParticionado?.Dispose();
                _ctsParticionado = null;
            }
        }
    }
}
