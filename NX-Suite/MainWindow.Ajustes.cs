using NX_Suite.Core;
using NX_Suite.Core.Configuracion;
using NX_Suite.Models.Cache;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media.Animation;

namespace NX_Suite
{
    /// <summary>
    /// MainWindow — lógica del overlay de Ajustes (tabs: Sonido, Caché).
    /// </summary>
    public partial class MainWindow
    {
        private bool _ajustesCargando;

        // ?? Apertura / cierre ????????????????????????????????????????????????

        private async void BtnAjustes_Click(object sender, RoutedEventArgs e)
        {
            _ajustesCargando = true;
            try
            {
                var prefs = await Servicios.Preferencias.CargarAsync();
                CargarEstadoAjustes(prefs);
            }
            finally
            {
                _ajustesCargando = false;
            }

            AplicarBlurFondo(true);
            MostrarOverlayConAnimacion(PanelAjustesOverlay);
        }

        private void BtnCerrarAjustes_Click(object sender, RoutedEventArgs e)
        {
            var fade = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(200)));
            fade.Completed += (_, _) =>
            {
                PanelAjustesOverlay.Visibility = Visibility.Collapsed;
                AplicarBlurFondo(false);
            };
            PanelAjustesOverlay.BeginAnimation(UIElement.OpacityProperty, fade);
        }

        // ?? Tabs ?????????????????????????????????????????????????????????????

        private void TabAjuste_Checked(object sender, RoutedEventArgs e)
        {
            // Ocultar todos los paneles primero
            if (PanelSonidoAjustes != null)  PanelSonidoAjustes.Visibility  = Visibility.Collapsed;
            if (PanelCacheAjustes  != null)  PanelCacheAjustes.Visibility   = Visibility.Collapsed;

            if (TabSonido?.IsChecked == true && PanelSonidoAjustes != null)
                PanelSonidoAjustes.Visibility = Visibility.Visible;

            if (TabCache?.IsChecked == true && PanelCacheAjustes != null)
            {
                PanelCacheAjustes.Visibility = Visibility.Visible;
                RefrescarPanelCache();
            }
        }

        // ?? Caché ?????????????????????????????????????????????????????????????

        private void RefrescarPanelCache()
        {
            // Recalcular estado de caché del catálogo actual
            if (_catalogoModulos != null)
                _cerebro.ActualizarEstadoCacheCatalogo(_catalogoModulos);

            // Pesos totales
            long bytesZips      = _cerebro.ObtenerPesoCacheZips();
            long bytesExtraccion = _cerebro.ObtenerPesoCacheExtraccion();
            TxtPesoZips.Text      = FormatearBytes(bytesZips);
            TxtPesoExtraccion.Text = FormatearBytes(bytesExtraccion);

            // Lista de módulos con caché activa (al menos un ZIP o carpeta)
            var items = new List<ItemCacheModuloVM>();

            if (_catalogoModulos != null)
            {
                foreach (var modulo in _catalogoModulos.Where(m => m.EstaEnCache))
                {
                    // Construir línea de detalle con versiones que tienen caché
                    var detalles = modulo.Versiones?
                        .Where(v => v.TieneZipCache || v.TieneCarpetaCache)
                        .Select(v =>
                        {
                            string tag = v.TieneCarpetaCache ? "Extraído" : "ZIP";
                            return $"v{v.Version} · {tag}";
                        })
                        .ToList() ?? new List<string>();

                    items.Add(new ItemCacheModuloVM
                    {
                        Nombre  = modulo.Nombre,
                        Detalle = detalles.Count > 0
                                  ? string.Join("   ", detalles)
                                  : "En caché",
                        Modulo  = modulo,
                    });
                }
            }

            ListaCacheModulos.ItemsSource = items.OrderBy(i => i.Nombre).ToList();
        }

        private void BtnEliminarCacheModulo_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as System.Windows.Controls.Button)?.Tag is not ItemCacheModuloVM item)
                return;

            try   { _cerebro.LimpiarCacheModulo(item.Modulo); }
            catch { /* silencioso: archivo en uso */ }

            RefrescarPanelCache();
            MostrarEstado("?  Caché eliminada.");
        }

        private void BtnLimpiarTodoCache_Click(object sender, RoutedEventArgs e)
        {
            try { _cerebro.LimpiarTodaLaBoveda(); }
            catch { /* silencioso */ }

            RefrescarPanelCache();
            MostrarEstado("?  Todo el caché eliminado.");
        }

        // ?? Sonido ????????????????????????????????????????????????????????????

        private void CargarEstadoAjustes(PreferenciasUsuario prefs)
        {
            var s = prefs.Sonido;
            SwSonidoActivo.IsChecked = s.Activo;
            SwIntro.IsChecked        = s.Intro;
            SwHover.IsChecked        = s.Hover;
            SwClick.IsChecked        = s.Click;
            SwNavegacion.IsChecked   = s.Navegacion;
            SwInstalar.IsChecked     = s.Instalar;
            SwExito.IsChecked        = s.Exito;
            SwError.IsChecked        = s.Error;
            SwCerrar.IsChecked       = s.Cerrar;
        }

        private async void SwitchAjuste_Click(object sender, RoutedEventArgs e)
        {
            if (_ajustesCargando) return;

            var prefs = new PreferenciasUsuario
            {
                Sonido = new SeccionSonido
                {
                    Activo     = SwSonidoActivo.IsChecked == true,
                    Intro      = SwIntro.IsChecked        == true,
                    Hover      = SwHover.IsChecked        == true,
                    Click      = SwClick.IsChecked        == true,
                    Navegacion = SwNavegacion.IsChecked   == true,
                    Instalar   = SwInstalar.IsChecked     == true,
                    Exito      = SwExito.IsChecked        == true,
                    Error      = SwError.IsChecked        == true,
                    Cerrar     = SwCerrar.IsChecked       == true,
                    Volumen    = ConfiguracionSonidos.Volumen,
                }
            };

            GestorPreferencias.AplicarSonido(prefs.Sonido);
            await Servicios.Preferencias.GuardarAsync(prefs);
            MostrarEstado("?  Preferencias guardadas.");
        }

        // ?? Helpers ??????????????????????????????????????????????????????????

        private async void MostrarEstado(string mensaje)
        {
            TxtEstadoAjustes.Text = mensaje;
            await System.Threading.Tasks.Task.Delay(2000);
            TxtEstadoAjustes.Text = "Los cambios se guardan automaticamente.";
        }

        private static string FormatearBytes(long bytes)
        {
            if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} GB";
            if (bytes >= 1_048_576)     return $"{bytes / 1_048_576.0:F1} MB";
            if (bytes >= 1_024)         return $"{bytes / 1_024.0:F0} KB";
            return $"{bytes} B";
        }
    }
}
