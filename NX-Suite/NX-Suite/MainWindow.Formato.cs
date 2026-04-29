using NX_Suite.Core.Configuracion;
using NX_Suite.Hardware;
using NX_Suite.Models;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace NX_Suite
{
    /// <summary>
    /// Handlers del overlay de Formateo FAT32. Independiente del Asistido
    /// Completo: solo formatea (no particiona) la unidad seleccionada usando
    /// <see cref="ParticionadorDiscos.FormatearSoloFAT32Async"/>.
    /// </summary>
    public partial class MainWindow
    {
        private readonly ObservableCollection<UnidadFormatoItem> _unidadesFormato = new();
        private CancellationTokenSource? _ctsFormato;

        // ?? Apertura del overlay (llamado desde RetractilDer) ????????????????

        public void AbrirOverlayFormatoFAT32()
        {
            ListaUnidadesFormato.ItemsSource = _unidadesFormato;
            RefrescarUnidadesFormato();
            PanelFormatoFAT32Overlay.Visibility = Visibility.Visible;
        }

        private void BtnCerrarFormato_Click(object sender, RoutedEventArgs e)
        {
            CerrarOverlayFormato();
        }

        private void PanelFormato_BackdropClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Si está formateando, ignorar click fuera para no perder el progreso
            if (_unidadesFormato.Any(u => u.EnProceso)) return;
            CerrarOverlayFormato();
        }

        private void CerrarOverlayFormato()
        {
            _ctsFormato?.Cancel();
            PanelFormatoFAT32Overlay.Visibility = Visibility.Collapsed;
        }

        // ?? Refresco de unidades ?????????????????????????????????????????????

        private void BtnRefrescarFormato_Click(object sender, RoutedEventArgs e)
            => RefrescarUnidadesFormato();

        private void RefrescarUnidadesFormato()
        {
            _unidadesFormato.Clear();

            var escaner = new EscanerDiscos();
            var unidades = escaner.ObtenerUnidadesRemovibles();

            foreach (var sd in unidades)
            {
                long capBytes = 0;
                try { capBytes = long.Parse(sd.CapacidadTotal) * 1024L * 1024L * 1024L; } catch { }

                _unidadesFormato.Add(new UnidadFormatoItem
                {
                    Letra         = sd.Letra,
                    Etiqueta      = string.IsNullOrWhiteSpace(sd.Etiqueta) ? "Sin etiqueta" : sd.Etiqueta,
                    Capacidad     = string.IsNullOrEmpty(sd.CapacidadTotal) || sd.CapacidadTotal == "0"
                                        ? "Tamaño desconocido"
                                        : $"{sd.CapacidadTotal} GB",
                    FormatoActual = string.IsNullOrWhiteSpace(sd.Formato) ? "RAW" : sd.Formato,
                    DiscoFisico   = sd.DiscoFisico,
                });
            }

            TxtSinUnidades.Visibility = _unidadesFormato.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            TxtEstadoFormato.Text     = _unidadesFormato.Count == 0
                ? "No hay unidades extraíbles."
                : "Selecciona una unidad para formatear";
        }

        // ?? Selección de tarjeta ?????????????????????????????????????????????

        private void TarjetaUnidad_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is not Border b || b.Tag is not UnidadFormatoItem item) return;

            // Toggle exclusivo
            foreach (var u in _unidadesFormato)
                u.Seleccionada = ReferenceEquals(u, item) && !u.Seleccionada;

            var sel = _unidadesFormato.FirstOrDefault(u => u.Seleccionada);
            TxtEstadoFormato.Text = sel != null
                ? $"Lista para formatear: {sel.Letra} ({sel.Capacidad})"
                : "Selecciona una unidad para formatear";
        }

        // ?? Ejecutar el formateo ?????????????????????????????????????????????

        private async void BtnFormatearAhora_Click(object sender, RoutedEventArgs e)
        {
            var item = _unidadesFormato.FirstOrDefault(u => u.Seleccionada);
            if (item == null)
            {
                TxtEstadoFormato.Text = "? Selecciona primero una unidad";
                return;
            }

            string etiqueta = TxtEtiquetaFormato.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(etiqueta)) etiqueta = "SWITCH SD";
            if (etiqueta.Length > 11) etiqueta = etiqueta.Substring(0, 11);

            // Confirmación visual: deshabilitar refresco y cierre
            BtnRefrescarFormato.IsEnabled  = false;
            BtnFormatearAhora.IsEnabled    = false;
            item.EnProceso                 = true;
            item.Estado                    = "Iniciando…";
            _ctsFormato                    = new CancellationTokenSource();

            try
            {
                var particionador = new ParticionadorDiscos();
                var progreso = new Progress<(int Pct, string Msg)>(p =>
                {
                    item.Progreso = p.Pct * 1.72; // 1.72 ? ancho ~172 px máximo de la barra
                    item.Estado   = p.Msg;
                    TxtEstadoFormato.Text = $"{item.Letra} ? {p.Msg} ({p.Pct}%)";
                });

                string urlZip = ConfiguracionRemota.Ui?.UrlFat32Format ?? "";
                await particionador.FormatearSoloFAT32Async(
                    letraRaiz:        item.Letra,
                    urlFat32FormatZip: urlZip,
                    etiqueta:         etiqueta,
                    progreso:         progreso,
                    ct:               _ctsFormato.Token);

                item.Estado          = "? Formateado";
                item.Progreso        = 172;
                TxtEstadoFormato.Text = $"? {item.Letra} formateada como FAT32 con etiqueta \"{etiqueta}\"";
            }
            catch (OperationCanceledException)
            {
                item.Estado = "Cancelado";
                TxtEstadoFormato.Text = "Operación cancelada por el usuario.";
            }
            catch (Exception ex)
            {
                item.Estado = "? Error";
                TxtEstadoFormato.Text = $"? Error: {ex.Message}";
                MessageBox.Show(this, ex.Message, "Error al formatear",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                item.EnProceso              = false;
                BtnRefrescarFormato.IsEnabled = true;
                BtnFormatearAhora.IsEnabled   = true;
                _ctsFormato?.Dispose();
                _ctsFormato = null;
                // Refrescar lectura de etiqueta/formato real tras formatear
                RefrescarUnidadesFormato();
            }
        }
    }
}
