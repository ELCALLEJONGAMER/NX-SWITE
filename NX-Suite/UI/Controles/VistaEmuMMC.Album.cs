using NX_Swite.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NX_Swite.UI.Controles
{
    /// <summary>Pestaña Álbum: capturas y vídeos de la consola (microSD + memoria interna).</summary>
    public partial class VistaEmuMMC
    {
        public static IValueConverter VisibleSiTexto { get; } = new ConvVisibleSiTexto();

        private sealed class ConvVisibleSiTexto : IValueConverter
        {
            public object Convert(object v, Type t, object p, CultureInfo c)
                => string.IsNullOrEmpty(v?.ToString()) ? Visibility.Collapsed : Visibility.Visible;
            public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
        }

        public sealed class ItemAlbum : INotifyPropertyChanged
        {
            private ImageSource? _miniatura;
            public ArchivoAlbum Archivo { get; init; } = new();
            public ImageSource? Miniatura
            {
                get => _miniatura;
                set { _miniatura = value; PropertyChanged?.Invoke(this, new(nameof(Miniatura))); }
            }
            public string Dia => Archivo.Fecha.ToString("dddd, d 'de' MMMM 'de' yyyy", CultureInfo.CurrentCulture) is var s
                ? char.ToUpper(s[0]) + s[1..] : string.Empty;
            public string Hora => Archivo.Fecha.ToString("HH:mm");
            public string Etiqueta => Archivo.Origen == OrigenAlbum.Consola ? "CONSOLA" : "MICROSD";
            public Brush  ColorEtiqueta => Archivo.Origen == OrigenAlbum.Consola ? PincelConsola : PincelSd;
            public Visibility VisibilidadVideo => Archivo.EsVideo ? Visibility.Visible : Visibility.Collapsed;
            public string Descripcion =>
                $"{(Archivo.EsVideo ? "Vídeo" : "Captura")} · {Archivo.Fecha:g} · {EmuMmcMigracionLogic.FormatearTamano(Archivo.Tamano)}";
            public event PropertyChangedEventHandler? PropertyChanged;
        }

        private static readonly Brush PincelConsola = Pincel("#B388FF");
        private static readonly Brush PincelSd      = Pincel("#00E676");

        private List<ItemAlbum> _album = new();
        private string _filtroAlbum = "todo";
        private CancellationTokenSource? _ctsMiniaturas;
        private int _indiceVisor = -1;

        // ── Pestañas ─────────────────────────────────────────────────────────

        private void Pestana_Checked(object sender, RoutedEventArgs e)
        {
            if (PanelAlbum == null || PanelHerramientas == null || PanelJuegos == null) return;
            PanelAlbum.Visibility        = TabAlbum.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PanelJuegos.Visibility       = TabJuegos.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            if (TabJuegos.IsChecked == true && _juegos.Count == 0 && BtnCargarJuegos.IsEnabled)
                _ = CargarJuegosAsync();
            PanelHerramientas.Visibility = TabHerramientas.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void LimpiarAlbum()
        {
            _ctsMiniaturas?.Cancel();
            _album = new();
            ListaAlbum.ItemsSource = null;
            TabAlbum.Tag = null;
            TxtAlbumInfo.Text = string.Empty;
            TxtBtnCargarAlbum.Text = "Cargar álbum";
            TxtAlbumVacio.Text = "Tus capturas y vídeos de la consola aparecerán aquí.";
            PanelAlbumVacio.Visibility = Visibility.Visible;
            BtnExportarAlbum.IsEnabled = false;
        }

        // ── Carga ────────────────────────────────────────────────────────────

        private async void BtnCargarAlbum_Click(object sender, RoutedEventArgs e) => await CargarAlbumAsync();

        private async Task CargarAlbumAsync()
        {
            string? letra = ObtenerLetraSD?.Invoke();
            int? disco = ObtenerDiscoFisico?.Invoke();
            if (string.IsNullOrWhiteSpace(letra)) return;

            BtnCargarAlbum.IsEnabled = false;
            BarraAlbum.Visibility = Visibility.Visible;
            TxtAlbumVacio.Text = "Leyendo el álbum…";
            PanelAlbumVacio.Visibility = Visibility.Visible;
            ListaAlbum.ItemsSource = null;
            string? aviso = null;
            try
            {
                var info = await Task.Run(() => EmuMmcInfoLogic.Leer(letra));
                var archivos = await Task.Run(() => EmuMmcAlbumLogic.ListarSd(letra, info.RutaNintendo));

                if (disco != null)
                {
                    TxtAlbumVacio.Text = "Leyendo la memoria de la consola (emuMMC)…";
                    try
                    {
                        string cache = EmuMmcAlbumLogic.CarpetaCache(ObtenerSD?.Invoke()?.Serial);
                        archivos.AddRange(await Task.Run(() =>
                        {
                            using var l = EmuMmcSavesLogic.Abrir(letra, disco.Value);
                            return l.ExtraerAlbumConsola(cache);
                        }));
                    }
                    catch (Exception ex)
                    {
                        Services.Logger.Warning($"[emuMMC] Álbum interno no disponible: {ex.Message}");
                        aviso = "No se pudo leer la memoria de la consola; se muestra solo el álbum de la microSD.";
                    }
                }

                _album = archivos.OrderByDescending(a => a.Fecha).Select(a => new ItemAlbum { Archivo = a }).ToList();
                AplicarFiltroAlbum();
                int fotos = _album.Count(a => !a.Archivo.EsVideo);
                int videos = _album.Count - fotos;
                TabAlbum.Tag = _album.Count > 0 ? _album.Count.ToString() : null;
                TxtAlbumInfo.Text = $"{fotos} capturas · {videos} vídeos";
                TxtBtnCargarAlbum.Text = "Recargar";
                if (_album.Count == 0)
                    TxtAlbumVacio.Text = aviso ?? "No hay capturas ni vídeos en esta consola.";
                else if (aviso != null)
                    TxtAlbumInfo.Text += "  ·  " + aviso;
                PanelAlbumVacio.Visibility = _album.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                _ = CargarMiniaturasAsync();
            }
            catch (Exception ex)
            {
                Services.Logger.Error("[emuMMC] No se pudo leer el álbum", ex);
                TxtAlbumVacio.Text = $"No se pudo leer el álbum: {ex.Message}";
            }
            finally
            {
                BtnCargarAlbum.IsEnabled = true;
                BarraAlbum.Visibility = Visibility.Collapsed;
            }
        }

        private void AplicarFiltroAlbum()
        {
            var datos = _album.Where(a => _filtroAlbum switch
            {
                "img" => !a.Archivo.EsVideo,
                "vid" => a.Archivo.EsVideo,
                _     => true,
            }).ToList();
            var vista = new ListCollectionView(datos);
            vista.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ItemAlbum.Dia)));
            ListaAlbum.ItemsSource = vista;
        }

        private void FiltroAlbum_Checked(object sender, RoutedEventArgs e)
        {
            _filtroAlbum = (sender as FrameworkElement)?.Tag as string ?? "todo";
            if (ListaAlbum != null && _album.Count > 0) AplicarFiltroAlbum();
        }

        /// <summary>Genera las miniaturas en segundo plano (los vídeos usan su miniatura de shell).</summary>
        private async Task CargarMiniaturasAsync()
        {
            _ctsMiniaturas?.Cancel();
            var cts = _ctsMiniaturas = new CancellationTokenSource();
            foreach (var item in _album.ToList())
            {
                if (cts.IsCancellationRequested) return;
                var img = await Task.Run(() => item.Archivo.EsVideo ? MiniaturaShell(item.Archivo.Ruta) : CargarBitmap(item.Archivo.Ruta, 384));
                if (cts.IsCancellationRequested) return;
                item.Miniatura = img;
            }
        }

        private static ImageSource? CargarBitmap(string ruta, int ancho = 0)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                if (ancho > 0) bmp.DecodePixelWidth = ancho;
                bmp.UriSource = new Uri(ruta);
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch { return null; }
        }

        private static ImageSource? MiniaturaShell(string ruta)
        {
            IntPtr hbmp = IntPtr.Zero;
            try
            {
                var iid = typeof(IShellItemImageFactory).GUID;
                SHCreateItemFromParsingName(ruta, IntPtr.Zero, ref iid, out var fab);
                fab.GetImage(new TamanoNativo { cx = 384, cy = 216 }, 0, out hbmp);
                var bmp = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hbmp, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                bmp.Freeze();
                return bmp;
            }
            catch { return null; }
            finally { if (hbmp != IntPtr.Zero) DeleteObject(hbmp); }
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct TamanoNativo { public int cx; public int cy; }

        [System.Runtime.InteropServices.ComImport]
        [System.Runtime.InteropServices.Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
        [System.Runtime.InteropServices.InterfaceType(System.Runtime.InteropServices.ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItemImageFactory
        {
            void GetImage(TamanoNativo size, int flags, out IntPtr phbm);
        }

        [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, PreserveSig = false)]
        private static extern void SHCreateItemFromParsingName(string path, IntPtr pbc, ref Guid riid, out IShellItemImageFactory item);

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        // ── Selección y exportación ──────────────────────────────────────────

        private void ListaAlbum_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int n = ListaAlbum.SelectedItems.Count;
            BtnExportarAlbum.IsEnabled = n > 0;
            TxtBtnExportar.Text = n > 1 ? $"Extraer ({n})" : "Extraer";
        }

        private void MenuAlbumTodo_Click(object sender, RoutedEventArgs e) => ListaAlbum.SelectAll();

        private void MenuAlbumVer_Click(object sender, RoutedEventArgs e)
        {
            if (ListaAlbum.SelectedItem is ItemAlbum i) AbrirVisor(i);
        }

        private void ListaAlbum_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if ((e.OriginalSource as DependencyObject) is { } d &&
                ItemsControl.ContainerFromElement(ListaAlbum, d) is ListBoxItem { DataContext: ItemAlbum i })
                AbrirVisor(i);
        }

        private void ListaAlbum_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && ListaAlbum.SelectedItem is ItemAlbum i) { AbrirVisor(i); e.Handled = true; }
        }

        private void BtnExportarAlbum_Click(object sender, RoutedEventArgs e)
            => Exportar(ListaAlbum.SelectedItems.Cast<ItemAlbum>().ToList());

        private void Exportar(List<ItemAlbum> items)
        {
            if (items.Count == 0) return;
            string imagenes = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            var dlg = new Microsoft.Win32.OpenFolderDialog
            {
                Title = items.Count == 1 ? "Carpeta donde extraer el archivo" : $"Carpeta donde extraer {items.Count} archivos",
                InitialDirectory = Directory.Exists(imagenes) ? imagenes : null,
            };
            if (dlg.ShowDialog() != true) return;

            string destino = items.Count == 1 ? dlg.FolderName : Path.Combine(dlg.FolderName, $"Álbum Switch {DateTime.Now:yyyy-MM-dd HH.mm}");
            Directory.CreateDirectory(destino);
            int ok = 0;
            foreach (var i in items)
            {
                try
                {
                    string ruta; int n = 0;
                    do ruta = Path.Combine(destino, EmuMmcAlbumLogic.NombreExportacion(i.Archivo, n++));
                    while (File.Exists(ruta));
                    File.Copy(i.Archivo.Ruta, ruta);
                    File.SetLastWriteTime(ruta, i.Archivo.Fecha);
                    ok++;
                }
                catch (Exception ex)
                {
                    Services.Logger.Warning($"[emuMMC] No se pudo extraer {i.Archivo.Ruta}: {ex.Message}");
                }
            }
            Services.Logger.Info($"[emuMMC] Álbum: {ok}/{items.Count} archivos extraídos a {destino}");
            Process.Start("explorer.exe", destino);
        }

        // ── Visor ────────────────────────────────────────────────────────────

        private List<ItemAlbum> ItemsVisibles => (ListaAlbum.ItemsSource as ListCollectionView)?.Cast<ItemAlbum>().ToList() ?? new();

        private void AbrirVisor(ItemAlbum item)
        {
            var lista = ItemsVisibles;
            _indiceVisor = lista.IndexOf(item);
            if (_indiceVisor < 0) return;
            Visor.Visibility = Visibility.Visible;
            MostrarEnVisor(lista);
            Visor.Focus();
        }

        private void MostrarEnVisor(List<ItemAlbum> lista)
        {
            var i = lista[_indiceVisor];
            VisorVideo.Stop();
            VisorVideo.Source = null;
            if (i.Archivo.EsVideo)
            {
                VisorImagen.Source = null;
                VisorImagen.Visibility = Visibility.Collapsed;
                VisorVideo.Visibility = Visibility.Visible;
                VisorVideo.Source = new Uri(i.Archivo.Ruta);
                VisorVideo.Play();
            }
            else
            {
                VisorVideo.Visibility = Visibility.Collapsed;
                VisorImagen.Visibility = Visibility.Visible;
                VisorImagen.Source = CargarBitmap(i.Archivo.Ruta);
            }
            TxtVisorTitulo.Text = $"{(i.Archivo.EsVideo ? "Vídeo" : "Captura")} · {i.Dia}";
            TxtVisorDetalle.Text = $"{i.Hora}  ·  {(i.Archivo.Origen == OrigenAlbum.Consola ? "Memoria de la consola" : "microSD")}  ·  {EmuMmcMigracionLogic.FormatearTamano(i.Archivo.Tamano)}";
            TxtVisorPosicion.Text = $"{_indiceVisor + 1} / {lista.Count}";
        }

        private void Navegar(int delta)
        {
            var lista = ItemsVisibles;
            if (lista.Count == 0) return;
            _indiceVisor = (_indiceVisor + delta + lista.Count) % lista.Count;
            MostrarEnVisor(lista);
        }

        private void CerrarVisor()
        {
            VisorVideo.Stop();
            VisorVideo.Source = null;
            VisorImagen.Source = null;
            Visor.Visibility = Visibility.Collapsed;
            ListaAlbum.Focus();
        }

        private void VisorAnterior_Click(object sender, RoutedEventArgs e)  => Navegar(-1);
        private void VisorSiguiente_Click(object sender, RoutedEventArgs e) => Navegar(1);
        private void VisorCerrar_Click(object sender, RoutedEventArgs e)    => CerrarVisor();

        private void VisorExtraer_Click(object sender, RoutedEventArgs e)
        {
            var lista = ItemsVisibles;
            if (_indiceVisor >= 0 && _indiceVisor < lista.Count) Exportar(new() { lista[_indiceVisor] });
        }

        private void Visor_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape: CerrarVisor(); break;
                case Key.Left:   Navegar(-1); break;
                case Key.Right:  Navegar(1); break;
                case Key.Space when VisorVideo.Visibility == Visibility.Visible: AlternarVideo(); break;
                default: return;
            }
            e.Handled = true;
        }

        private void Visor_FondoClick(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource == Visor) CerrarVisor();
        }

        private bool _videoPausado;
        private void AlternarVideo()
        {
            if (_videoPausado) VisorVideo.Play(); else VisorVideo.Pause();
            _videoPausado = !_videoPausado;
        }

        private void VisorVideo_Click(object sender, MouseButtonEventArgs e) { AlternarVideo(); e.Handled = true; }

        private void VisorVideo_Ended(object sender, RoutedEventArgs e)
        {
            VisorVideo.Position = TimeSpan.Zero;
            VisorVideo.Play();
        }
    }
}
