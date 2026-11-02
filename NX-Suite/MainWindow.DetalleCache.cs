using NX_Swite.Core;
using NX_Swite.Hardware;
using NX_Swite.Models;
using NX_Swite.Services;
using NX_Swite.UI;
using NX_Swite.UI.Controles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace NX_Swite
{
    /// <summary>
    /// MainWindow — Detalle de módulo: sección de caché local (tamaños y borrado de ZIP/carpeta extraída).
    /// </summary>
    public partial class MainWindow
    {
        private void RefrescarSeccionCache(ModuloConfig modulo)
        {
            if (modulo == null) return;

            // Versión de referencia: seleccionada > instalada > latest
            ModuloVersion? verRef = _versionSeleccionadaDetalle;
            if (verRef == null && !string.IsNullOrEmpty(modulo.VersionInstalada) &&
                modulo.VersionInstalada is not ("No detectado" or "No instalado"))
            {
                verRef = modulo.Versiones?.FirstOrDefault(v =>
                    string.Equals(v.Version, modulo.VersionInstalada, StringComparison.OrdinalIgnoreCase));
            }
            verRef ??= modulo.Versiones?.FirstOrDefault();

            // Actualizar rutas del módulo para que cualquier referencia externa funcione
            modulo.RutaCacheZip     = verRef?.RutaCacheZipVer     ?? string.Empty;
            modulo.RutaCacheCarpeta = verRef?.RutaCacheCarpetaVer ?? string.Empty;

            // Las filas de ZIP/Carpeta del panel ya no se usan — el cache está embebido en los chips
            FilaCacheZip.Visibility     = Visibility.Collapsed;
            FilaCacheCarpeta.Visibility = Visibility.Collapsed;

            // El panel se muestra solo si hay versiones (para los chips)
            bool tieneVersiones = modulo.Versiones?.Count > 0;
            PanelCacheDetalle.Visibility = tieneVersiones ? Visibility.Visible : Visibility.Collapsed;
        }

        private async Task ComputarTamanosCacheAsync(ModuloConfig modulo, bool zipExiste, bool carpetaExiste)
        {
            string tamZip = string.Empty, tamCarpeta = string.Empty;
            await Task.Run(() =>
            {
                if (zipExiste && System.IO.File.Exists(modulo.RutaCacheZip))
                    tamZip = FormatBytes(new System.IO.FileInfo(modulo.RutaCacheZip).Length);
                if (carpetaExiste)
                {
                    if (System.IO.Directory.Exists(modulo.RutaCacheCarpeta))
                    {
                        long total = new System.IO.DirectoryInfo(modulo.RutaCacheCarpeta)
                            .GetFiles("*", System.IO.SearchOption.AllDirectories)
                            .Sum(f => f.Length);
                        tamCarpeta = FormatBytes(total);
                    }
                    else if (System.IO.File.Exists(modulo.RutaCacheCarpeta))
                        tamCarpeta = FormatBytes(new System.IO.FileInfo(modulo.RutaCacheCarpeta).Length);
                }
            });
            TxtTamanoZip.Text     = string.IsNullOrEmpty(tamZip)     ? string.Empty : $"({tamZip})";
            TxtTamanoCarpeta.Text = string.IsNullOrEmpty(tamCarpeta) ? string.Empty : $"({tamCarpeta})";
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1_073_741_824L) return $"{bytes / 1_073_741_824.0:F1} GB";
            if (bytes >= 1_048_576L)     return $"{bytes / 1_048_576.0:F1} MB";
            if (bytes >= 1024L)          return $"{bytes / 1024.0:F0} KB";
            return $"{bytes} B";
        }

        private void BtnBorrarCacheZip_Click(object sender, RoutedEventArgs e)
        {
            if (_moduloActual == null || string.IsNullOrEmpty(_moduloActual.RutaCacheZip)) return;
            try
            {
                if (System.IO.File.Exists(_moduloActual.RutaCacheZip))
                    System.IO.File.Delete(_moduloActual.RutaCacheZip);
                if (_catalogoModulos != null) _cerebro.ActualizarEstadoCacheCatalogo(_catalogoModulos);
                RefrescarSeccionCache(_moduloActual);
                RellenarChipsVersiones(_moduloActual);
            }
            catch (Exception ex)
            {
                Dialogos.Error($"Error al borrar ZIP: {ex.Message}");
            }
        }

        private void BtnBorrarCacheCarpeta_Click(object sender, RoutedEventArgs e)
        {
            if (_moduloActual == null || string.IsNullOrEmpty(_moduloActual.RutaCacheCarpeta)) return;
            try
            {
                if (System.IO.Directory.Exists(_moduloActual.RutaCacheCarpeta))
                    System.IO.Directory.Delete(_moduloActual.RutaCacheCarpeta, true);
                else if (System.IO.File.Exists(_moduloActual.RutaCacheCarpeta))
                    System.IO.File.Delete(_moduloActual.RutaCacheCarpeta);
                if (_catalogoModulos != null) _cerebro.ActualizarEstadoCacheCatalogo(_catalogoModulos);
                RefrescarSeccionCache(_moduloActual);
                RellenarChipsVersiones(_moduloActual);
            }
            catch (Exception ex)
            {
                Dialogos.Error($"Error al borrar caché extraído: {ex.Message}");
            }
        }
    }
}
