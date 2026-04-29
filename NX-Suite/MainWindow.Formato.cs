using NX_Suite.Core.Configuracion;
using NX_Suite.Hardware;
using NX_Suite.Models;
using NX_Suite.UI;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace NX_Suite
{
    /// <summary>
    /// Handlers del overlay de Formateo FAT32 (rediseñado). Lee la SD activa
    /// del panel derecho (no tiene combo ni lista propia), no muestra ? ni
    /// botón Refrescar. Al pulsar el SafeButton "FORMATEAR" cierra el overlay
    /// y delega el progreso al <c>OverlayCarga</c> global, que bloquea la UI
    /// hasta que la operación termina.
    /// </summary>
    public partial class MainWindow
    {
        private SDInfo? _sdSelFormato;
        private bool _formateandoEnProceso;
        private CancellationTokenSource? _ctsFormato;

        // ?? Apertura / cierre ????????????????????????????????????????????????

        public void AbrirOverlayFormatoFAT32()
        {
            _formateandoEnProceso = false;
            _sdSelFormato = InfoSD.ComboDrives.SelectedItem as SDInfo;
            ActualizarInfoSDFormato();
            AplicarBlurFondo(true);
            PanelFormatoFAT32Overlay.Visibility = Visibility.Visible;
        }

        private void PanelFormato_BackdropClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_formateandoEnProceso) return;
            CerrarOverlayFormato();
        }

        internal void CerrarOverlayFormato()
        {
            if (_formateandoEnProceso) return;
            AplicarBlurFondo(false);
            PanelFormatoFAT32Overlay.Visibility = Visibility.Collapsed;
        }

        // ?? Pintado de la tarjeta SD desde el panel derecho ??????????????????

        private void ActualizarInfoSDFormato()
        {
            if (_sdSelFormato == null || string.IsNullOrEmpty(_sdSelFormato.Letra))
            {
                TxtLetraSDFormato.Text = "—";
                TxtNombreSDFormato.Text = "Sin SD seleccionada";
                TxtInfoSDFormato.Text = "Selecciona una SD en el panel derecho";
                AvisoSinSDFormato.Visibility = Visibility.Visible;
                BtnFormatearAhora.IsEnabled = false;
                TxtEstadoFormato.Text = "Conecta o selecciona una microSD para continuar";
                return;
            }

            string cap = string.IsNullOrEmpty(_sdSelFormato.CapacidadTotal) || _sdSelFormato.CapacidadTotal == "0"
                ? "Tamaño desconocido"
                : $"{_sdSelFormato.CapacidadTotal} GB";

            TxtLetraSDFormato.Text = _sdSelFormato.Letra.TrimEnd('\\', ':');
            TxtNombreSDFormato.Text = string.IsNullOrWhiteSpace(_sdSelFormato.Etiqueta)
                ? "Sin etiqueta"
                : _sdSelFormato.Etiqueta;
            TxtInfoSDFormato.Text = $"{cap}  •  Disco #{_sdSelFormato.DiscoFisico}  •  {(string.IsNullOrEmpty(_sdSelFormato.Formato) ? "RAW" : _sdSelFormato.Formato)}";

            AvisoSinSDFormato.Visibility = Visibility.Collapsed;
            BtnFormatearAhora.IsEnabled = true;
            TxtEstadoFormato.Text = "Mantén pulsado FORMATEAR para confirmar";
        }

        // ?? Acción principal: formatear ??????????????????????????????????????

        private async void BtnFormatearAhora_Click(object sender, RoutedEventArgs e)
        {
            // Releer la SD por si el usuario cambió la selección en el panel derecho
            _sdSelFormato = InfoSD.ComboDrives.SelectedItem as SDInfo;
            if (_sdSelFormato == null || string.IsNullOrEmpty(_sdSelFormato.Letra))
            {
                ActualizarInfoSDFormato();
                return;
            }

            _formateandoEnProceso = true;
            _ctsFormato = new CancellationTokenSource();

            // Cierra el overlay y da paso al OverlayCarga global (bloqueante).
            // El blur del fondo lo gestiona internamente _pantallaCarga.Mostrar().
            AplicarBlurFondo(false);
            PanelFormatoFAT32Overlay.Visibility = Visibility.Collapsed;
            _pantallaCarga.Mostrar($"Formateando {_sdSelFormato.Letra} en FAT32");

            try
            {
                var particionador = new ParticionadorDiscos();
                var reportador = _pantallaCarga.ObtenerReportador();
                var progreso = new Progress<(int Pct, string Msg)>(p =>
                    reportador.Report(new EstadoProgreso
                    {
                        Porcentaje = p.Pct,
                        TareaActual = p.Msg,
                        PasoActual = 0
                    }));

                string urlZip = ConfiguracionRemota.Ui?.UrlFat32Format ?? string.Empty;
                await particionador.FormatearSoloFAT32Async(
                    letraRaiz: _sdSelFormato.Letra,
                    urlFat32FormatZip: urlZip,
                    etiqueta: "SWITCH SD",
                    progreso: progreso,
                    ct: _ctsFormato.Token);

                await Task.Delay(500);
                await ActualizarListaUnidadesAsync();

                _pantallaCarga.Ocultar();
                Dialogos.Info(
                    $"La unidad {_sdSelFormato.Letra} se ha formateado como FAT32 con etiqueta \"SWITCH SD\".",
                    "Formateado completado");
            }
            catch (OperationCanceledException)
            {
                _pantallaCarga.Ocultar();
            }
            catch (Exception ex)
            {
                _pantallaCarga.Ocultar();
                Dialogos.Error($"Error al formatear:\n\n{ex.Message}", "Fallo");
            }
            finally
            {
                _formateandoEnProceso = false;
                _ctsFormato?.Dispose();
                _ctsFormato = null;
            }
        }
    }
}
