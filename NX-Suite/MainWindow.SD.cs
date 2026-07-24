using NX_Swite.Hardware;
using NX_Swite.Core.Configuracion;
using NX_Swite.Models;
using NX_Swite.UI;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NX_Swite
{
    /// <summary>
    /// MainWindow — Gestión de la SD (lista de unidades, panel de info, refresco
    /// al cambiar de unidad). Suscrito al combo de unidades y al evento
    /// plug &amp; play de <see cref="NotificadorDiscos"/>.
    /// </summary>
    public partial class MainWindow
    {
        private async Task ActualizarListaUnidadesAsync()
        {
            try
            {
                string? letraPrevia = (InfoSD.ComboDrives.SelectedItem as SDInfo)?.Letra;
                var unidades = await _cerebro.ObtenerUnidadesRemoviblesAsync();

                InfoSD.ComboDrives.ItemsSource       = unidades;
                InfoSD.ComboDrives.DisplayMemberPath = "FullName";

                if (unidades != null && unidades.Any())
                {
                    var unidadPrevia = unidades.FirstOrDefault(u => u.Letra == letraPrevia);
                    InfoSD.ComboDrives.SelectedItem = unidadPrevia ?? unidades.First();
                }
                else
                {
                    LimpiarInterfazSD();
                    // No hay SD: recalcular estados localmente sin llamar a la red
                    if (!_cargandoCatalogoInicial && _catalogoModulos != null)
                    {
                        await _cerebro.RefrescarEstadosSinRedAsync(_catalogoModulos, string.Empty);
                        RefrescarVistaActual();
                    }

                    if (VistaNews.Visibility == Visibility.Visible)
                        ActualizarDiagnosticoSD();
                }
            }
            catch (Exception ex)
            {
                Dialogos.Advertencia($"Error detectando la SD: {ex.Message}", "Diagnóstico");
            }
        }

        private void LimpiarInterfazSD()
        {
            InfoSD.TxtTotalSize.Text  = "0 GB";
            InfoSD.TxtFileSystem.Text = "--";
            InfoSD.TxtSDSerial.Text   = "Desconocido";
            InfoSD.TxtAtmosVer.Text   = "N/A";
            InfoSD.TxtSDModelo.Visibility    = System.Windows.Visibility.Collapsed;
            InfoSD.LblSDModelo.Visibility    = System.Windows.Visibility.Collapsed;
            InfoSD.TxtSDRegion.Visibility    = System.Windows.Visibility.Collapsed;
            InfoSD.LblSDRegion.Visibility    = System.Windows.Visibility.Collapsed;
            OcultarSeccionLlaves();
        }

        /// <summary>
        /// Refresca únicamente la versión de Atmosphere en el panel derecho
        /// sin necesidad de cambiar la unidad seleccionada. Útil tras instalar
        /// o desinstalar un módulo con etiquetas "atmosphere" / "atmosphere_mod".
        /// </summary>
        private void OcultarSeccionLlaves()
        {
            InfoSD.SepLlaves.Visibility        = System.Windows.Visibility.Collapsed;
            InfoSD.LblSeccionLlaves.Visibility  = System.Windows.Visibility.Collapsed;
            InfoSD.LblMasterKey.Visibility      = System.Windows.Visibility.Collapsed;
            InfoSD.TxtMasterKey.Visibility      = System.Windows.Visibility.Collapsed;
            InfoSD.LblFirmware.Visibility       = System.Windows.Visibility.Collapsed;
            InfoSD.TxtFirmware.Visibility       = System.Windows.Visibility.Collapsed;
            InfoSD.LblAtmosMinima.Visibility    = System.Windows.Visibility.Collapsed;
            InfoSD.TxtAtmosMinima.Visibility    = System.Windows.Visibility.Collapsed;
        }

        private void MostrarSeccionLlaves(NX_Swite.Models.InfoPanelDerecho info)
        {
            InfoSD.SepLlaves.Visibility        = System.Windows.Visibility.Visible;
            InfoSD.LblSeccionLlaves.Visibility  = System.Windows.Visibility.Visible;
            InfoSD.TxtMasterKey.Text            = info.MasterKeyMaxima;
            InfoSD.LblMasterKey.Visibility      = System.Windows.Visibility.Visible;
            InfoSD.TxtMasterKey.Visibility      = System.Windows.Visibility.Visible;
            InfoSD.TxtFirmware.Text             = info.FirmwareCompatible;
            InfoSD.LblFirmware.Visibility       = System.Windows.Visibility.Visible;
            InfoSD.TxtFirmware.Visibility       = System.Windows.Visibility.Visible;
            InfoSD.TxtAtmosMinima.Text          = info.AtmosphereDesde;
            InfoSD.LblAtmosMinima.Visibility    = System.Windows.Visibility.Visible;
            InfoSD.TxtAtmosMinima.Visibility    = System.Windows.Visibility.Visible;
        }

        internal void RefrescarVersionAtmos()
        {
            if (InfoSD.ComboDrives.SelectedItem is not SDInfo unidad) return;
            if (_catalogoModulos == null) return;

            var info = _cerebro.ObtenerInfoPanel(unidad, _catalogoModulos.ToList());
            InfoSD.TxtAtmosVer.Text = info.VersionAtmos;
        }



        private async void ComboDrives_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (InfoSD.ComboDrives.SelectedItem is not SDInfo unidad)
                return;

            if (_catalogoModulos == null || _datosGist == null)
                return;

            // Siempre actualizar el panel de info SD
            var info = _cerebro.ObtenerInfoPanel(unidad, _catalogoModulos.ToList());
            InfoSD.TxtTotalSize.Text  = info.Capacidad;
            InfoSD.TxtFileSystem.Text = info.Formato;
            InfoSD.TxtAtmosVer.Text   = info.VersionAtmos;
            InfoSD.TxtSDSerial.Text   = info.Serial;

            var entradaModelo = NX_Swite.Core.ModeloSwitchTable.ResolverSoloRemota(info.Serial);
            InfoSD.TxtSDModelo.Text       = entradaModelo?.Modelo ?? string.Empty;
            InfoSD.TxtSDModelo.Visibility = entradaModelo != null ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            InfoSD.LblSDModelo.Visibility = InfoSD.TxtSDModelo.Visibility;
            InfoSD.TxtSDRegion.Text       = entradaModelo?.Region ?? string.Empty;
            InfoSD.TxtSDRegion.Visibility = entradaModelo != null ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            InfoSD.LblSDRegion.Visibility = InfoSD.TxtSDRegion.Visibility;

            InfoSD.TxtFileSystem.Foreground = info.Formato == "FAT32"
                ? (SolidColorBrush)FindResource("AcentoCian")
                : (SolidColorBrush)FindResource("AcentoRojo");

            if (info.HayProdkeys)
                MostrarSeccionLlaves(info);
            else
                OcultarSeccionLlaves();

            // Solo re-sincronizar si la carga inicial ya termino
            if (_cargandoCatalogoInicial) return;

            try
            {
                _datosGist = await _cerebro.SincronizarTodoAsync(ConfiguracionLocal.UrlGistPrincipal, unidad.Letra);

                if (_datosGist == null) return;

                // Re-aplicar tablas remotas para que modelo/región/llaves usen datos frescos del Gist
                AplicarConfiguracionRemota(_datosGist);

                _catalogoModulos = new ObservableCollection<ModuloConfig>(_datosGist.Modulos ?? new System.Collections.Generic.List<ModuloConfig>());

                // Repoblar campos del panel que dependen de las tablas remotas
                var entradaModelo2 = NX_Swite.Core.ModeloSwitchTable.ResolverSoloRemota(InfoSD.TxtSDSerial.Text);
                InfoSD.TxtSDModelo.Text       = entradaModelo2?.Modelo ?? string.Empty;
                InfoSD.TxtSDModelo.Visibility = entradaModelo2 != null ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
                InfoSD.LblSDModelo.Visibility = InfoSD.TxtSDModelo.Visibility;
                InfoSD.TxtSDRegion.Text       = entradaModelo2?.Region ?? string.Empty;
                InfoSD.TxtSDRegion.Visibility = entradaModelo2 != null ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
                InfoSD.LblSDRegion.Visibility = InfoSD.TxtSDRegion.Visibility;

                if (InfoSD.TxtMasterKey.Visibility == System.Windows.Visibility.Visible &&
                    !string.IsNullOrEmpty(InfoSD.TxtMasterKey.Text))
                {
                    var mk = NX_Swite.Core.MasterKeyTable.BuscarSoloRemota(InfoSD.TxtMasterKey.Text);
                    InfoSD.TxtFirmware.Text    = mk?.RangoHosCompatible ?? "--";
                    InfoSD.TxtAtmosMinima.Text = mk?.AtmosphereDesde    ?? "--";
                }

                // Si el detalle está activo no tocamos los paneles del catálogo:
                // BtnInstalar/BtnBorrar ya restauran la vista correctamente.
                if (VistaDetalle.Visibility == Visibility.Visible) return;

                if (_mundoSeleccionado != null)
                    ActualizarFiltrosDelMundo(_mundoSeleccionado.Id);

                RefrescarVistaActual();

                if (VistaNews.Visibility == Visibility.Visible)
                    ActualizarDiagnosticoSD();
            }
            catch (Exception ex)
            {
                Dialogos.Advertencia($"Error al sincronizar con la SD: {ex.Message}", "Error");
            }
        }

        private async void BtnExpulsarSD_Click(object? sender, EventArgs e)
        {
            if (InfoSD.ComboDrives.SelectedItem is not SDInfo unidad)
            {
                Dialogos.Advertencia("No hay ninguna microSD seleccionada.", "Expulsar SD");
                return;
            }

            InfoSD.IsEnabled = false;
            try
            {
                bool ok = await Task.Run(() => EscanerDiscos.ExpulsarUnidad(unidad.Letra));

                if (ok)
                    Dialogos.Advertencia(
                        $"La microSD \"{unidad.Etiqueta}\" fue expulsada correctamente.\nYa puedes retirarla con seguridad.",
                        "Expulsar SD ✓");
                else
                    Dialogos.Advertencia(
                        "Windows no pudo expulsar la SD en este momento.\nCierra los programas que puedan estar accediendo a ella e inténtalo de nuevo.",
                        "Expulsar SD");
            }
            catch (Exception ex)
            {
                Dialogos.Advertencia($"Error al intentar expulsar la SD: {ex.Message}", "Expulsar SD");
            }
            finally
            {
                InfoSD.IsEnabled = true;
            }
        }
    }
}
