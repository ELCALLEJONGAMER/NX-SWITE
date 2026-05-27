using NX_Suite.UI;
using NX_Suite.Core;
using NX_Suite.Core.Configuracion;
using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace NX_Suite
{
    /// <summary>
    /// MainWindow — lógica del overlay de Ajustes.
    /// </summary>
    public partial class MainWindow
    {
        private bool _ajustesCargando;

        // ?? Apertura / cierre ????????????????????????????????????????????????

        private async void BtnAjustes_Click(object sender, RoutedEventArgs e)
        {
            // Cargar preferencias antes de mostrar para que los switches
            // estén en su estado correcto desde el primer frame visible.
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

            // Blur solo sobre el contenido de fondo (no sobre el overlay)
            AplicarBlurFondo(true);

            // Fade-in + scale-up idéntico al resto de overlays
            MostrarOverlayConAnimacion(PanelAjustesOverlay);
        }

        private void BtnCerrarAjustes_Click(object sender, RoutedEventArgs e)
        {
            // Fade-out
            var fade = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(200)));
            fade.Completed += (_, _) =>
            {
                PanelAjustesOverlay.Visibility = Visibility.Collapsed;
                AplicarBlurFondo(false);
            };
            PanelAjustesOverlay.BeginAnimation(UIElement.OpacityProperty, fade);
        }

        // ?? Carga de estado ??????????????????????????????????????????????????

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

        // ?? Switches ?????????????????????????????????????????????????????????

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
            await MostrarConfirmacionAjustes();
        }

        private async System.Threading.Tasks.Task MostrarConfirmacionAjustes()
        {
            TxtEstadoAjustes.Text = "?  Preferencias guardadas.";
            await System.Threading.Tasks.Task.Delay(2000);
            TxtEstadoAjustes.Text = "Los cambios se guardan automaticamente.";
        }

        // ?? Tabs ?????????????????????????????????????????????????????????????

        private void TabAjuste_Checked(object sender, RoutedEventArgs e)
        {
            if (PanelSonidoAjustes != null)
                PanelSonidoAjustes.Visibility = Visibility.Visible;
        }
    }
}

