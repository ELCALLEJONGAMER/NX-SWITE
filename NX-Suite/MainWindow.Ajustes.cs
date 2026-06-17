using NX_Suite.Core;
using NX_Suite.Core.Configuracion;
using NX_Suite.Core;
using NX_Suite.Hardware;
using NX_Suite.Models.Cache;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
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
            if (PanelSonidoAjustes        != null) PanelSonidoAjustes.Visibility        = Visibility.Collapsed;
            if (PanelCacheAjustes         != null) PanelCacheAjustes.Visibility         = Visibility.Collapsed;
            if (PanelCarpetasProtegidas   != null) PanelCarpetasProtegidas.Visibility   = Visibility.Collapsed;
            if (PanelGitHub               != null) PanelGitHub.Visibility               = Visibility.Collapsed;

            if (TabSonido?.IsChecked == true && PanelSonidoAjustes != null)
                PanelSonidoAjustes.Visibility = Visibility.Visible;

            if (TabCache?.IsChecked == true && PanelCacheAjustes != null)
            {
                PanelCacheAjustes.Visibility = Visibility.Visible;
                RefrescarPanelCache();
            }

            if (TabCarpetasProtegidas?.IsChecked == true && PanelCarpetasProtegidas != null)
            {
                PanelCarpetasProtegidas.Visibility = Visibility.Visible;
                _ = RefrescarPanelCarpetasProtegidasAsync();
            }

            if (TabGitHub?.IsChecked == true && PanelGitHub != null)
            {
                PanelGitHub.Visibility = Visibility.Visible;
                RefrescarPanelGitHub();
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

        // ?? Carpetas Protegidas ???????????????????????????????????????????????

        /// <summary>
        /// Abre el overlay de Ajustes directamente en el tab Carpetas Protegidas.
        /// Si Ajustes ya está abierto solo cambia el tab activo.
        /// </summary>
        internal async Task AbrirAjustesEnTabCarpetasAsync()
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

            if (PanelAjustesOverlay.Visibility != Visibility.Visible)
            {
                AplicarBlurFondo(true);
                MostrarOverlayConAnimacion(PanelAjustesOverlay);
            }

            // Activar el tab Carpetas Protegidas
            if (TabCarpetasProtegidas != null)
                TabCarpetasProtegidas.IsChecked = true;
        }

        /// <summary>
        /// Refresca el panel de Carpetas Protegidas únicamente si el overlay de
        /// Ajustes está abierto y el tab Carpetas Protegidas está activo.
        /// Se llama al conectar/desconectar una SD o al cambiar la selección del combo.
        /// </summary>
        internal async Task RefrescarCarpetasProtegidasSiVisibleAsync()
        {
            if (PanelAjustesOverlay.Visibility == Visibility.Visible &&
                TabCarpetasProtegidas?.IsChecked == true)
            {
                await RefrescarPanelCarpetasProtegidasAsync();
            }
        }

        private async Task RefrescarPanelCarpetasProtegidasAsync()
        {
            var prefs = await Servicios.Preferencias.CargarAsync();

            // Explorador de la SD
            string? letraSD = (InfoSD.ComboDrives.SelectedItem as SDInfo)?.Letra;
            if (!string.IsNullOrEmpty(letraSD) && System.IO.Directory.Exists(letraSD))
            {
                var protegidosSet = new HashSet<string>(
                    prefs.LimpiezaSD.EntradasProtegidas, StringComparer.OrdinalIgnoreCase);

                var entradas = new List<EntradaSDVM>();
                var nombresEnSD = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var dir in System.IO.Directory.EnumerateDirectories(letraSD))
                {
                    string nombre = System.IO.Path.GetFileName(dir);
                    nombresEnSD.Add(nombre);
                    entradas.Add(new EntradaSDVM(nombre, EsTipoEntrada.Carpeta,
                        protegidosSet.Contains(nombre)));
                }

                foreach (var file in System.IO.Directory.EnumerateFiles(letraSD))
                {
                    string nombre = System.IO.Path.GetFileName(file);
                    string ext    = System.IO.Path.GetExtension(file);
                    var tipo      = NX_Suite.Core.ZipLogic.ExtensionesComprimidas.Contains(ext)
                                    ? EsTipoEntrada.Comprimido
                                    : EsTipoEntrada.Archivo;
                    nombresEnSD.Add(nombre);
                    entradas.Add(new EntradaSDVM(nombre, tipo,
                        protegidosSet.Contains(nombre)));
                }

                ListaExploradorSD.ItemsSource      = entradas.OrderBy(e => e.Tipo != EsTipoEntrada.Carpeta).ThenBy(e => e.Nombre).ToList();
                ListaExploradorSD.Visibility       = Visibility.Visible;
                TxtCabeceraExploradorSD.Visibility = Visibility.Visible;
                TxtSinSDEnAjustes.Visibility       = Visibility.Collapsed;

                // Entradas en prefs que NO existen físicamente en la SD (huérfanas / manuales)
                var huerfanas = prefs.LimpiezaSD.EntradasProtegidas
                    .Where(s => !nombresEnSD.Contains(s))
                    .OrderBy(s => s)
                    .ToList();

                if (huerfanas.Count > 0)
                {
                    TxtCabeceraEntradasGuardadas.Text       = "ENTRADAS SIN COINCIDENCIA EN LA SD";
                    TxtCabeceraEntradasGuardadas.Visibility = Visibility.Visible;
                    ListaEntradasProtegidas.ItemsSource     = huerfanas;
                    ListaEntradasProtegidas.Visibility      = Visibility.Visible;
                }
                else
                {
                    TxtCabeceraEntradasGuardadas.Visibility = Visibility.Collapsed;
                    ListaEntradasProtegidas.Visibility      = Visibility.Collapsed;
                }
            }
            else
            {
                ListaExploradorSD.Visibility       = Visibility.Collapsed;
                TxtCabeceraExploradorSD.Visibility = Visibility.Collapsed;
                TxtSinSDEnAjustes.Visibility       = Visibility.Visible;

                // Sin SD: mostrar todas las entradas protegidas guardadas
                var todas = prefs.LimpiezaSD.EntradasProtegidas.OrderBy(s => s).ToList();
                if (todas.Count > 0)
                {
                    TxtCabeceraEntradasGuardadas.Text       = "ENTRADAS PROTEGIDAS GUARDADAS";
                    TxtCabeceraEntradasGuardadas.Visibility = Visibility.Visible;
                    ListaEntradasProtegidas.ItemsSource     = todas;
                    ListaEntradasProtegidas.Visibility      = Visibility.Visible;
                }
                else
                {
                    TxtCabeceraEntradasGuardadas.Visibility = Visibility.Collapsed;
                    ListaEntradasProtegidas.Visibility      = Visibility.Collapsed;
                }
            }
        }

        private async void BtnQuitarEntradaProtegida_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as System.Windows.Controls.Button)?.Tag is not string nombre) return;

            var prefs = await Servicios.Preferencias.CargarAsync();
            prefs.LimpiezaSD.EntradasProtegidas.RemoveAll(
                s => string.Equals(s, nombre, StringComparison.OrdinalIgnoreCase));
            await Servicios.Preferencias.GuardarAsync(prefs);

            await RefrescarPanelCarpetasProtegidasAsync();
            MostrarEstado("?  Entrada eliminada.");
        }

        private async void BtnAnadirEntradaProtegida_Click(object sender, RoutedEventArgs e)
            => await AnadirEntradaProtegidaAsync();

        private async void TxtNuevaEntrada_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                await AnadirEntradaProtegidaAsync();
        }

        private async Task AnadirEntradaProtegidaAsync()
        {
            string nombre = TxtNuevaEntrada.Text.Trim();
            if (string.IsNullOrEmpty(nombre)) return;

            var prefs = await Servicios.Preferencias.CargarAsync();

            bool yaExiste = prefs.LimpiezaSD.EntradasProtegidas
                .Any(s => string.Equals(s, nombre, StringComparison.OrdinalIgnoreCase));

            if (!yaExiste)
            {
                prefs.LimpiezaSD.EntradasProtegidas.Add(nombre);
                await Servicios.Preferencias.GuardarAsync(prefs);
                MostrarEstado("?  Entrada añadida.");
            }
            else
            {
                MostrarEstado("?  Esa entrada ya está protegida.");
            }

            TxtNuevaEntrada.Text = string.Empty;
            await RefrescarPanelCarpetasProtegidasAsync();
        }

        private async void CheckEntradaSD_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as System.Windows.Controls.CheckBox)?.Tag is not string nombre) return;
            bool proteger = (sender as System.Windows.Controls.CheckBox)?.IsChecked == true;

            var prefs = await Servicios.Preferencias.CargarAsync();

            if (proteger)
            {
                bool yaExiste = prefs.LimpiezaSD.EntradasProtegidas
                    .Any(s => string.Equals(s, nombre, StringComparison.OrdinalIgnoreCase));
                if (!yaExiste)
                    prefs.LimpiezaSD.EntradasProtegidas.Add(nombre);
            }
            else
            {
                prefs.LimpiezaSD.EntradasProtegidas.RemoveAll(
                    s => string.Equals(s, nombre, StringComparison.OrdinalIgnoreCase));
            }

            await Servicios.Preferencias.GuardarAsync(prefs);
            await RefrescarPanelCarpetasProtegidasAsync();
            MostrarEstado(proteger ? "?  Entrada protegida." : "?  Entrada desprotegida.");
        }

        // ?? GitHub Token ???????????????????????????????????????????????????????

        private void RefrescarPanelGitHub()
        {
            bool hayToken = TokenGitHub.HayToken;
            if (TxtEstadoToken != null)
            {
                TxtEstadoToken.Text       = hayToken ? "? Token configurado" : "Sin token configurado";
                TxtEstadoToken.Foreground = hayToken
                    ? new System.Windows.Media.SolidColorBrush(
                          System.Windows.Media.Color.FromRgb(0x00, 0xD2, 0xFF))
                    : new System.Windows.Media.SolidColorBrush(
                          System.Windows.Media.Color.FromRgb(0x50, 0x50, 0x60));
            }
            // No pre-rellena el PasswordBox — el token nunca se muestra después de guardado.
            if (TxtTokenGitHub != null)
                TxtTokenGitHub.Clear();
        }

        private void BtnGuardarToken_Click(object sender, RoutedEventArgs e)
        {
            string valor = TxtTokenGitHub?.Password ?? string.Empty;
            if (string.IsNullOrWhiteSpace(valor))
            {
                MostrarEstado("?  El campo de token está vacío.");
                return;
            }

            TokenGitHub.Guardar(valor);
            RefrescarPanelGitHub();
            MostrarEstado("?  Token guardado de forma segura.");
        }

        private void BtnBorrarToken_Click(object sender, RoutedEventArgs e)
        {
            TokenGitHub.Borrar();
            RefrescarPanelGitHub();
            MostrarEstado("?  Token eliminado.");
        }
    }
}
