using NX_Swite.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

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

        private async void BtnLeerSaves_Click(object sender, RoutedEventArgs e)
        {
            string? letra = ObtenerLetraSD?.Invoke();
            int? disco = ObtenerDiscoFisico?.Invoke();
            if (string.IsNullOrWhiteSpace(letra) || disco == null) return;

            BtnLeerSaves.IsEnabled = false;
            TxtSaves.Text = "Leyendo saves…";
            ListaSaves.ItemsSource = null;
            try
            {
                var prep = _titleDb.PrepararAsync();
                var saves = await Task.Run(() =>
                {
                    using var logica = EmuMmcSavesLogic.Abrir(letra, disco.Value);
                    return logica.ListarSavesDeJuegos();
                });
                await prep;
                foreach (var s in saves) s.Nombre = _titleDb.ResolverNombre(s.TitleId);
                ListaSaves.ItemsSource = saves.OrderBy(s => s.Nombre).ToList();
                TxtSaves.Text = $"Saves de juegos ({saves.Count})";
            }
            catch (Exception ex)
            {
                Services.Logger.Error("[emuMMC] No se pudieron leer los saves", ex);
                TxtSaves.Text = $"No se pudieron leer los saves: {ex.Message}";
            }
            finally
            {
                BtnLeerSaves.IsEnabled = true;
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
                            logica.ExportarSave(s, Path.Combine(carpetaBase, $"{nombre} [{s.TitleId}]", s.UsuarioCorto));
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
                    ? $"{ok} save(s) respaldados en {carpetaBase}"
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

        public async Task RefrescarAsync()
        {
            string? letra = ObtenerLetraSD?.Invoke();
            if (string.IsNullOrWhiteSpace(letra))
            {
                TxtEstado.Text = "Selecciona una microSD para ver su emuMMC.";
                ListaDatos.ItemsSource = null;
                return;
            }

            TxtEstado.Text = "Leyendo…";
            InfoEmuMmc info;
            try
            {
                info = await Task.Run(() => EmuMmcInfoLogic.Leer(letra));
            }
            catch (Exception ex)
            {
                TxtEstado.Text = $"No se pudo leer la emuMMC: {ex.Message}";
                ListaDatos.ItemsSource = null;
                return;
            }

            if (!info.ExisteIni)
            {
                TxtEstado.Text = "Esta microSD no tiene emuMMC configurada (falta emuMMC/emummc.ini).";
                ListaDatos.ItemsSource = null;
                return;
            }

            TxtEstado.Text = info.Habilitada ? "emuMMC habilitada" : "emuMMC configurada pero deshabilitada";

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
