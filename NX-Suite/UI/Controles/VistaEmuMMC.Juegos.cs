using NX_Swite.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NX_Swite.UI.Controles
{
    /// <summary>Pestaña Juegos: juegos con partida guardada en la emuMMC, agrupando todos los perfiles.</summary>
    public partial class VistaEmuMMC
    {
        public sealed class ItemJuego : INotifyPropertyChanged
        {
            private ImageSource? _icono;
            public string TitleId { get; init; } = string.Empty;
            public string Nombre { get; init; } = string.Empty;
            public List<SaveEmuMmc> Saves { get; init; } = new();
            public string? IconoUrl { get; init; }
            public ImageSource? Icono
            {
                get => _icono;
                set { _icono = value; PropertyChanged?.Invoke(this, new(nameof(Icono))); }
            }
            public string Inicial => string.IsNullOrEmpty(Nombre) ? "?" : Nombre[..1].ToUpperInvariant();
            public string Perfiles => string.Join(" · ", Saves.Select(s => s.Perfil).Distinct());
            public string Tamano => EmuMmcMigracionLogic.FormatearTamano(Saves.Sum(s => s.Tamano));
            public string Descripcion => $"{Nombre}\nTitle ID: {TitleId}\nPerfiles: {Perfiles}";
            public event PropertyChangedEventHandler? PropertyChanged;
        }

        private List<ItemJuego> _juegos = new();

        private void LimpiarJuegos()
        {
            _juegos = new();
            ListaJuegos.ItemsSource = null;
            TabJuegos.Tag = null;
            TxtJuegosInfo.Text = string.Empty;
            TxtBtnCargarJuegos.Text = "Cargar juegos";
            TxtJuegosVacio.Text = "Tus juegos y partidas guardadas aparecerán aquí.";
            PanelJuegosVacio.Visibility = Visibility.Visible;
            BtnRespaldarJuegos.IsEnabled = false;
        }

        private async void BtnCargarJuegos_Click(object sender, RoutedEventArgs e) => await CargarJuegosAsync();

        private async Task CargarJuegosAsync()
        {
            string? letra = ObtenerLetraSD?.Invoke();
            int? disco = ObtenerDiscoFisico?.Invoke();
            if (string.IsNullOrWhiteSpace(letra) || disco == null) return;

            BtnCargarJuegos.IsEnabled = false;
            BarraJuegos.Visibility = Visibility.Visible;
            PanelJuegosVacio.Visibility = Visibility.Visible;
            TxtJuegosVacio.Text = "Leyendo tus juegos de la emuMMC…";
            ListaJuegos.ItemsSource = null;
            try
            {
                var prep = _titleDb.PrepararAsync();
                var (saves, perfiles) = await Task.Run(() =>
                {
                    using var l = EmuMmcSavesLogic.Abrir(letra, disco.Value);
                    return (l.ListarSavesDeJuegos(), l.LeerPerfiles());
                });
                await prep;
                foreach (var s in saves)
                {
                    s.Nombre   = _titleDb.ResolverNombre(s.TitleId);
                    s.IconoUrl = _titleDb.ResolverIconoUrl(s.TitleId);
                    s.Perfil   = perfiles.TryGetValue(s.UsuarioId, out var apodo) ? apodo : s.UsuarioCorto;
                }
                _juegos = saves.GroupBy(s => s.TitleId)
                    .Select(g => new ItemJuego
                    {
                        TitleId = g.Key,
                        Nombre = g.First().Nombre,
                        IconoUrl = g.First().IconoUrl,
                        Saves = g.OrderBy(s => s.Perfil).ToList(),
                    })
                    .OrderBy(j => j.Nombre, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
                FiltrarJuegos();
                TabJuegos.Tag = _juegos.Count > 0 ? _juegos.Count.ToString() : null;
                TxtJuegosInfo.Text = $"{_juegos.Count} juegos · {saves.Count} partidas";
                TxtBtnCargarJuegos.Text = "Recargar";
                if (_juegos.Count == 0) TxtJuegosVacio.Text = "No hay partidas guardadas en esta emuMMC.";
                PanelJuegosVacio.Visibility = _juegos.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                CargarIconosJuegos();
            }
            catch (Exception ex)
            {
                Services.Logger.Error("[emuMMC] No se pudieron leer los juegos", ex);
                TxtJuegosVacio.Text = $"No se pudieron leer los juegos: {ex.Message}";
            }
            finally
            {
                BtnCargarJuegos.IsEnabled = true;
                BarraJuegos.Visibility = Visibility.Collapsed;
            }
        }

        private void CargarIconosJuegos()
        {
            foreach (var j in _juegos.Where(j => j.Icono == null && !string.IsNullOrEmpty(j.IconoUrl)))
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(j.IconoUrl!);
                    bmp.DecodePixelWidth = 264;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    j.Icono = bmp;
                }
                catch { }
            }
        }

        private void FiltrarJuegos()
        {
            string q = TxtBuscarJuego.Text.Trim();
            ListaJuegos.ItemsSource = q.Length == 0
                ? _juegos
                : _juegos.Where(j => j.Nombre.Contains(q, StringComparison.CurrentCultureIgnoreCase)
                                  || j.TitleId.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        private void TxtBuscarJuego_TextChanged(object sender, TextChangedEventArgs e)
        {
            TxtBuscarMarca.Visibility = TxtBuscarJuego.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            FiltrarJuegos();
        }

        private void ListaJuegos_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => BtnRespaldarJuegos.IsEnabled = ListaJuegos.SelectedItems.Count > 0;

        private void MenuJuegosTodo_Click(object sender, RoutedEventArgs e) => ListaJuegos.SelectAll();

        private void MenuCopiarTitleId_Click(object sender, RoutedEventArgs e)
            => CopiarJuegos(j => j.TitleId);

        private void MenuCopiarNombre_Click(object sender, RoutedEventArgs e)
            => CopiarJuegos(j => j.Nombre);

        private void CopiarJuegos(Func<ItemJuego, string> campo)
        {
            var sel = ListaJuegos.SelectedItems.Cast<ItemJuego>().ToList();
            if (sel.Count == 0) return;
            try
            {
                Clipboard.SetText(string.Join(Environment.NewLine, sel.Select(campo)));
                TxtJuegosInfo.Text = sel.Count == 1 ? $"Copiado: {campo(sel[0])}" : $"{sel.Count} elementos copiados";
            }
            catch (Exception ex) { Services.Logger.Warning($"[emuMMC] Portapapeles: {ex.Message}"); }
        }

        private async void BtnRespaldarJuegos_Click(object sender, RoutedEventArgs e)
        {
            string? letra = ObtenerLetraSD?.Invoke();
            int? disco = ObtenerDiscoFisico?.Invoke();
            var saves = ListaJuegos.SelectedItems.Cast<ItemJuego>().SelectMany(j => j.Saves).ToList();
            if (string.IsNullOrWhiteSpace(letra) || disco == null || saves.Count == 0) return;

            string descargas = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            var dlg = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Carpeta donde guardar los saves",
                InitialDirectory = Directory.Exists(descargas) ? descargas : null,
            };
            if (dlg.ShowDialog() != true) return;

            string carpetaBase = Path.Combine(dlg.FolderName, "NX-Swite Saves", $"emuMMC_{DateTime.Now:yyyyMMdd_HHmmss}");
            BtnRespaldarJuegos.IsEnabled = false;
            BarraJuegos.Visibility = Visibility.Visible;
            TxtJuegosInfo.Text = "Respaldando partidas…";
            int ok = 0, errores = 0;
            try
            {
                await Task.Run(() =>
                {
                    using var l = EmuMmcSavesLogic.Abrir(letra, disco.Value);
                    foreach (var s in saves)
                    {
                        try
                        {
                            string nombre = string.Concat(s.Nombre.Split(Path.GetInvalidFileNameChars())).Trim();
                            string perfil = string.Concat(s.Perfil.Split(Path.GetInvalidFileNameChars())).Trim();
                            l.ExportarSave(s, Path.Combine(carpetaBase, $"{nombre} [{s.TitleId}]", perfil));
                            ok++;
                        }
                        catch (Exception ex)
                        {
                            Services.Logger.Error($"[emuMMC] Respaldo fallido {s.TitleId}", ex);
                            errores++;
                        }
                    }
                });
                TxtJuegosInfo.Text = errores == 0
                    ? $"{ok} partida(s) respaldadas. Restáuralas con DBI, JKSV o Checkpoint."
                    : $"{ok} respaldadas, {errores} con error. Ver log.";
                if (ok > 0) System.Diagnostics.Process.Start("explorer.exe", carpetaBase);
            }
            catch (Exception ex)
            {
                Services.Logger.Error("[emuMMC] Respaldo de saves fallido", ex);
                TxtJuegosInfo.Text = $"No se pudo respaldar: {ex.Message}";
            }
            finally
            {
                BarraJuegos.Visibility = Visibility.Collapsed;
                BtnRespaldarJuegos.IsEnabled = ListaJuegos.SelectedItems.Count > 0;
            }
        }
    }
}
