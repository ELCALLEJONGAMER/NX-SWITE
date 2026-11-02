using NX_Swite.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NX_Swite.UI.Controles
{
    /// <summary>
    /// Mundo emuMMC: información de la emuMMC y respaldo manual de saves
    /// de juegos (solo lectura sobre la emuMMC).
    /// </summary>
    public partial class VistaEmuMMC : UserControl
    {
        /// <summary>Devuelve la letra de la SD seleccionada (p.ej. "E:\") o null.</summary>
        public Func<string?>? ObtenerLetraSD { get; set; }

        /// <summary>Devuelve el número de disco físico de la SD seleccionada o null.</summary>
        public Func<int?>? ObtenerDiscoFisico { get; set; }

        private static readonly Services.TitleDbService _titleDb = new();

        public VistaEmuMMC()
        {
            InitializeComponent();
        }

        private void ListaSaves_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => BtnRespaldar.IsEnabled = ListaSaves.SelectedItems.Count > 0;

        private const string TodosLosPerfiles = "Todos los perfiles";
        private List<SaveEmuMmc> _saves = new();

        private void ComboPerfil_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string? perfil = ComboPerfil.SelectedItem as string;
            ListaSaves.ItemsSource = perfil == null || perfil == TodosLosPerfiles
                ? _saves
                : _saves.Where(s => s.Perfil == perfil).ToList();
        }

        private async void BtnLeerSaves_Click(object sender, RoutedEventArgs e)
        {
            string? letra = ObtenerLetraSD?.Invoke();
            int? disco = ObtenerDiscoFisico?.Invoke();
            if (string.IsNullOrWhiteSpace(letra) || disco == null) return;

            BtnLeerSaves.IsEnabled = false;
            PanelEscanear.Visibility = Visibility.Collapsed;
            PanelEscaneando.Visibility = Visibility.Visible;
            TxtSaves.Text = "Leyendo saves…";
            ListaSaves.ItemsSource = null;
            try
            {
                var prep = _titleDb.PrepararAsync();
                var (saves, perfiles) = await Task.Run(() =>
                {
                    using var logica = EmuMmcSavesLogic.Abrir(letra, disco.Value);
                    return (logica.ListarSavesDeJuegos(), logica.LeerPerfiles());
                });
                await prep;
                foreach (var s in saves)
                {
                    s.Nombre   = _titleDb.ResolverNombre(s.TitleId);
                    s.IconoUrl = _titleDb.ResolverIconoUrl(s.TitleId);
                    s.Perfil   = perfiles.TryGetValue(s.UsuarioId, out var apodo) ? apodo : s.UsuarioCorto;
                }
                _saves = saves.OrderBy(s => s.Perfil).ThenBy(s => s.Nombre).ToList();
                var opciones = new List<string> { TodosLosPerfiles };
                opciones.AddRange(_saves.Select(s => s.Perfil).Distinct());
                ComboPerfil.ItemsSource = opciones;
                ComboPerfil.SelectedIndex = 0;
                TxtSaves.Text = $"Saves de juegos ({saves.Count}) · {opciones.Count - 1} perfil(es)";
            }
            catch (Exception ex)
            {
                Services.Logger.Error("[emuMMC] No se pudieron leer los saves", ex);
                _saves = new();
                TxtSaves.Text = $"No se pudieron leer los saves: {ex.Message}";
            }
            finally
            {
                BtnLeerSaves.IsEnabled = true;
                PanelEscaneando.Visibility = Visibility.Collapsed;
                PanelEscanear.Visibility = _saves.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private async void BtnRespaldar_Click(object sender, RoutedEventArgs e)
        {
            string? letra = ObtenerLetraSD?.Invoke();
            int? disco = ObtenerDiscoFisico?.Invoke();
            var seleccion = ListaSaves.SelectedItems.Cast<SaveEmuMmc>().ToList();
            if (string.IsNullOrWhiteSpace(letra) || disco == null || seleccion.Count == 0) return;

            string descargas = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            var dlg = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Carpeta donde guardar los saves",
                InitialDirectory = Directory.Exists(descargas) ? descargas : null,
            };
            if (dlg.ShowDialog() != true) return;

            string carpetaBase = Path.Combine(dlg.FolderName, "NX-Swite Saves", $"emuMMC_{DateTime.Now:yyyyMMdd_HHmmss}");
            BtnRespaldar.IsEnabled = false;
            TxtSaves.Text = "Respaldando saves…";
            int ok = 0;
            var errores = new List<string>();
            try
            {
                await Task.Run(() =>
                {
                    using var logica = EmuMmcSavesLogic.Abrir(letra, disco.Value);
                    foreach (var s in seleccion)
                    {
                        try
                        {
                            string nombre = string.Concat(s.Nombre.Split(Path.GetInvalidFileNameChars())).Trim();
                            string perfil = string.Concat(s.Perfil.Split(Path.GetInvalidFileNameChars())).Trim();
                            logica.ExportarSave(s, Path.Combine(carpetaBase, $"{nombre} [{s.TitleId}]", perfil));
                            ok++;
                        }
                        catch (Exception ex)
                        {
                            Services.Logger.Error($"[emuMMC] Respaldo fallido {s.TitleId}", ex);
                            errores.Add($"{s.Nombre}: {ex.Message}");
                        }
                    }
                });
                TxtSaves.Text = errores.Count == 0
                    ? $"{ok} save(s) rescatados. Recupéralos en la consola con DBI, JKSV o Checkpoint."
                    : $"{ok} respaldados, {errores.Count} con error. Ver log.";
                if (ok > 0)
                    System.Diagnostics.Process.Start("explorer.exe", carpetaBase);
            }
            catch (Exception ex)
            {
                Services.Logger.Error("[emuMMC] Respaldo de saves fallido", ex);
                TxtSaves.Text = $"No se pudo respaldar: {ex.Message}";
            }
            finally
            {
                BtnRespaldar.IsEnabled = ListaSaves.SelectedItems.Count > 0;
            }
        }

        private void BtnActualizar_Click(object sender, RoutedEventArgs e) => _ = RefrescarAsync();

        public void MostrarDashboard()
        {
            PanelSaves.Visibility = Visibility.Collapsed;
            PanelDashboard.Visibility = Visibility.Visible;
        }

        private void TarjetaSaves_Click(object sender, MouseButtonEventArgs e)
        {
            PanelDashboard.Visibility = Visibility.Collapsed;
            PanelSaves.Visibility = Visibility.Visible;
        }

        private void BtnVolver_Click(object sender, RoutedEventArgs e) => MostrarDashboard();

        private void Tarjeta_MouseEnter(object sender, MouseEventArgs e) => AnimarTarjeta(sender, 1.03, 1);

        private void Tarjeta_MouseLeave(object sender, MouseEventArgs e) => AnimarTarjeta(sender, 1.0, 0);

        private static void AnimarTarjeta(object sender, double escala, double halo)
        {
            if (sender is not Border tarjeta) return;
            var duracion = TimeSpan.FromMilliseconds(180);
            if (tarjeta.RenderTransform is ScaleTransform st)
            {
                st.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(escala, duracion));
                st.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(escala, duracion));
            }
            if (tarjeta.Parent is Grid g && g.Children.Count > 0 && g.Children[0] is Border borde && borde != tarjeta)
                borde.BeginAnimation(OpacityProperty, new DoubleAnimation(halo, duracion));
        }

        private void PonerEstado(string texto, string color, string icono)
        {
            TxtEstado.Text = texto;
            var pincel = (SolidColorBrush)new BrushConverter().ConvertFrom(color)!;
            TxtEstado.Foreground = pincel;
            IconoEstado.Stroke = pincel;
            IconoEstado.Data = Geometry.Parse(icono);
            BadgeEstado.Background = new SolidColorBrush(Color.FromArgb(0x1A, pincel.Color.R, pincel.Color.G, pincel.Color.B));
        }

        private const string IconoOk = "M20 6L9 17l-5-5";
        private const string IconoAviso = "M12 3l10 18H2z M12 10v5 M12 18v.5";
        private const string IconoNo = "M6 6l12 12 M18 6L6 18";

        private void LimpiarResumen()
        {
            TxtTipo.Text = "—";
            TxtContenidos.Text = "—";
            ListaDatos.ItemsSource = null;
        }

        public async Task RefrescarAsync()
        {
            string? letra = ObtenerLetraSD?.Invoke();
            LimpiarResumen();
            TxtSd.Text = string.IsNullOrWhiteSpace(letra) ? "—" : letra.TrimEnd('\\');
            if (string.IsNullOrWhiteSpace(letra))
            {
                PonerEstado("Sin microSD", "#FFB300", IconoAviso);
                TxtSubtitulo.Text = "Selecciona una microSD para ver su emuMMC.";
                return;
            }

            PonerEstado("Leyendo…", "#00D2FF", IconoOk);
            InfoEmuMmc info;
            try
            {
                info = await Task.Run(() => EmuMmcInfoLogic.Leer(letra));
            }
            catch (Exception ex)
            {
                PonerEstado("Error", "#FF5252", IconoNo);
                TxtSubtitulo.Text = $"No se pudo leer la emuMMC: {ex.Message}";
                return;
            }

            if (!info.ExisteIni)
            {
                PonerEstado("No configurada", "#FF5252", IconoNo);
                TxtSubtitulo.Text = "Esta microSD no tiene emuMMC configurada (falta emuMMC/emummc.ini).";
                return;
            }

            if (info.Habilitada)
                PonerEstado("Habilitada", "#00E676", IconoOk);
            else
                PonerEstado("Deshabilitada", "#FFB300", IconoAviso);
            TxtSubtitulo.Text = "Gestión y mantenimiento de tu emuMMC";
            TxtTipo.Text = info.Tipo switch
            {
                TipoEmuMmc.Particion => "Partición",
                TipoEmuMmc.Archivos  => "Archivos",
                _                    => "Desconocido",
            };
            TxtContenidos.Text = info.ExisteNintendo ? info.ContenidosNintendo.ToString() : "—";

            var datos = new List<KeyValuePair<string, string>>
            {
                new("Tipo", info.Tipo switch
                {
                    TipoEmuMmc.Particion => "Partición (RAW)",
                    TipoEmuMmc.Archivos  => "Archivos en la SD",
                    _                    => "Desconocido",
                }),
            };
            if (info.Sector != null)       datos.Add(new("Sector de inicio", $"0x{info.Sector:X}"));
            if (info.RutaArchivos != null) datos.Add(new("Ruta de archivos", info.RutaArchivos));
            datos.Add(new("Carpeta Nintendo", info.RutaNintendo == null
                ? "No indicada"
                : $"{info.RutaNintendo} ({(info.ExisteNintendo ? "existe" : "no encontrada")})"));
            if (info.ExisteNintendo)
                datos.Add(new("Contenidos instalados en SD", info.ContenidosNintendo.ToString()));

            ListaDatos.ItemsSource = datos;
        }
    }
}
