using NX_Suite.Hardware;
using NX_Suite.Core.Configuracion;
using NX_Suite.Hardware;
using NX_Suite.Models;
using NX_Suite.UI;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NX_Suite
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
        }

        /// <summary>
        /// Refresca únicamente la versión de Atmosphere en el panel derecho
        /// sin necesidad de cambiar la unidad seleccionada. Útil tras instalar
        /// o desinstalar un módulo con etiquetas "atmosphere" / "atmosphere_mod".
        /// </summary>
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
            InfoSD.TxtFileSystem.Foreground = info.Formato == "FAT32"
                ? (SolidColorBrush)FindResource("AcentoCian")
                : (SolidColorBrush)FindResource("AcentoRojo");

            // Solo re-sincronizar si la carga inicial ya termino
            if (_cargandoCatalogoInicial) return;

            try
            {
                _datosGist = await _cerebro.SincronizarTodoAsync(ConfiguracionLocal.UrlGistPrincipal, unidad.Letra);

                if (_datosGist == null) return;

                _catalogoModulos = new ObservableCollection<ModuloConfig>(_datosGist.Modulos ?? new System.Collections.Generic.List<ModuloConfig>());

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
    }
}
