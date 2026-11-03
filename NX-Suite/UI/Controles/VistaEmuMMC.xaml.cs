using NX_Swite.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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

        /// <summary>Devuelve la unidad seleccionada completa (serial, capacidad…).</summary>
        public Func<Hardware.SDInfo?>? ObtenerSD { get; set; }

        /// <summary>Confirmación de borrado de la unidad (letra, disco).</summary>
        public Func<string, int, bool>? ConfirmarBorrado { get; set; }

        /// <summary>URL del zip de fat32format (catálogo).</summary>
        public Func<string>? ObtenerUrlFat32 { get; set; }

        /// <summary>Re-escanea las unidades tras particionar.</summary>
        public Func<Task>? RefrescarUnidades { get; set; }

        /// <summary>Firmware de la emuMMC detectado en el panel derecho (o null).</summary>
        public Func<string?>? ObtenerFirmware { get; set; }

        /// <summary>Serial de la consola detectado en el panel derecho (para el fondo Lite/OLED/Normal).</summary>
        public Func<string?>? ObtenerSerialConsola { get; set; }

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

        private void BtnActualizar_Click(object sender, RoutedEventArgs e) => _ = RefrescarAsync(forzar: true);

        /// <summary>Alto mínimo del dashboard para que Álbum/Juegos tengan espacio útil; por debajo se desplaza.</summary>
        private const double AltoMinimoDashboard = 860;

        private void ScrollDashboard_SizeChanged(object sender, SizeChangedEventArgs e)
            => PanelDashboard.Height = Math.Max(AltoMinimoDashboard, e.NewSize.Height);

        public void MostrarDashboard()
        {
            if (_ctsMigracion != null) return;
            MostrarPanel(PanelDashboard);
        }

        private void MostrarPanel(FrameworkElement panel)
        {
            foreach (var p in new FrameworkElement[] { PanelDashboard, PanelSaves, PanelMigrar, PanelRespaldar, PanelRestaurar })
                p.Visibility = p == panel ? Visibility.Visible : Visibility.Collapsed;
            bool conProgreso = panel == PanelMigrar || panel == PanelRespaldar || panel == PanelRestaurar;
            PanelProgreso.Visibility = conProgreso ? Visibility.Visible : Visibility.Collapsed;
            TxtEtiquetaCopia.Text = panel == PanelMigrar ? "COPIA EN EL PC" : "ESTADO";
            if (panel != PanelMigrar)
            {
                TxtCopiaMigracion.Text = panel == PanelRespaldar ? "Listo para respaldar" : "Listo para restaurar";
                BarraMigracion.Value = 0;
                TxtPorcentaje.Text = string.Empty;
                TxtMigracion.Text = "La microSD no se modifica hasta que confirmes.";
            }
            else if (_carpetaCopia == null)
                TxtCopiaMigracion.Text = "Ninguna";
        }

        // ── Migración (un lector) ─────────────────────────────────────────────

        private string? _carpetaCopia;
        private CancellationTokenSource? _ctsMigracion;

        private void TarjetaMigrar_Click(object sender, MouseButtonEventArgs e) => MostrarPanel(PanelMigrar);

        private void PonerCopia(string carpeta, ManifiestoMigracion m)
        {
            _carpetaCopia = carpeta;
            string tipo = m.Tipo == TipoEmuMmc.Particion ? $"partición {m.GbEmuMmcNecesarios} GB" : "archivos";
            TxtCopiaMigracion.Text = $"{carpeta}  ·  emuMMC {tipo}  ·  {m.NumArchivos} archivos";
        }

        private Button[] BotonesBloqueables => new[]
        {
            BtnPaso1, BtnPaso3, BtnElegirCopia, BtnVolverMigrar,
            BtnCrearRespaldo, BtnCarpetaRespaldos, BtnVolverRespaldar,
            BtnRestaurar, BtnVolverRestaurar, BtnOtraCarpetaRestaurar,
        };

        private async Task EjecutarMigracionAsync(Func<IProgress<(int Pct, string Msg)>, CancellationToken, Task> accion,
            string titulo = "Migrar microSD")
        {
            _ctsMigracion = new CancellationTokenSource();
            var estados = BotonesBloqueables.ToDictionary(b => b, b => b.IsEnabled);
            foreach (var b in estados.Keys) b.IsEnabled = false;
            ListaRespaldosRest.IsEnabled = ChkRespJuegos.IsEnabled = ChkRestJuegos.IsEnabled = false;
            BtnCancelarMigracion.Visibility = Visibility.Visible;
            var progreso = new Progress<(int Pct, string Msg)>(p =>
            {
                BarraMigracion.Value = p.Pct;
                TxtPorcentaje.Text = $"{p.Pct}%";
                TxtMigracion.Text = p.Msg;
            });
            try
            {
                await accion(progreso, _ctsMigracion.Token);
            }
            catch (OperationCanceledException)
            {
                TxtMigracion.Text = "Operación cancelada.";
            }
            catch (Exception ex)
            {
                Services.Logger.Error($"[emuMMC] {titulo} fallido", ex);
                TxtMigracion.Text = $"Error: {ex.Message}";
                Dialogos.Error(ex.Message, titulo);
            }
            finally
            {
                _ctsMigracion.Dispose();
                _ctsMigracion = null;
                foreach (var kv in estados) kv.Key.IsEnabled = kv.Value;
                ListaRespaldosRest.IsEnabled = ChkRespJuegos.IsEnabled = true;
                ChkRestJuegos.IsEnabled = (ListaRespaldosRest.SelectedItem as ItemRespaldo)?.Manifiesto.IncluyeJuegos == true;
                BtnCancelarMigracion.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnCancelarMigracion_Click(object sender, RoutedEventArgs e) => _ctsMigracion?.Cancel();

        private async void BtnPaso1_Click(object sender, RoutedEventArgs e)
        {
            var sd = ObtenerSD?.Invoke();
            if (sd == null || sd.DiscoFisico < 0)
            {
                Dialogos.Advertencia("Selecciona primero la microSD antigua.", "Migrar microSD");
                return;
            }
            var info = await Task.Run(() => EmuMmcInfoLogic.Leer(sd.Letra));
            if (!info.ExisteIni || info.Tipo == TipoEmuMmc.NoConfigurada)
            {
                Dialogos.Advertencia("La microSD seleccionada no tiene emuMMC configurada.", "Migrar microSD");
                return;
            }

            string descargas = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            var dlg = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Carpeta del PC donde guardar la copia de la microSD",
                InitialDirectory = Directory.Exists(descargas) ? descargas : null,
            };
            if (dlg.ShowDialog() != true) return;
            string carpeta = Path.Combine(dlg.FolderName, "NX-Swite Migracion", $"SD_{DateTime.Now:yyyyMMdd_HHmmss}");

            await EjecutarMigracionAsync(async (p, ct) =>
            {
                var m = await Task.Run(() => EmuMmcMigracionLogic.CopiarOrigenAPcAsync(
                    sd.Letra, sd.DiscoFisico, sd.Serial, carpeta, p, ct), ct);
                PonerCopia(carpeta, m);
                Dialogos.Info("Copia completada.\n\nExtrae la microSD antigua, inserta la nueva, selecciónala y pulsa el paso 3.",
                    "Migrar microSD");
            });
        }

        private void BtnElegirCopia_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Carpeta de una copia de migración (contiene manifiesto.json)" };
            if (dlg.ShowDialog() != true) return;
            try { PonerCopia(dlg.FolderName, EmuMmcMigracionLogic.LeerManifiesto(dlg.FolderName)); }
            catch (Exception ex) { Dialogos.Error(ex.Message, "Migrar microSD"); }
        }

        private async void BtnPaso3_Click(object sender, RoutedEventArgs e)
        {
            if (_carpetaCopia == null)
            {
                Dialogos.Advertencia("Primero copia la microSD antigua al PC (paso 1) o elige una copia existente.", "Migrar microSD");
                return;
            }
            var sd = ObtenerSD?.Invoke();
            if (sd == null || sd.DiscoFisico < 0)
            {
                Dialogos.Advertencia("Selecciona la microSD nueva.", "Migrar microSD");
                return;
            }

            ManifiestoMigracion m;
            try { m = EmuMmcMigracionLogic.LeerManifiesto(_carpetaCopia); }
            catch (Exception ex) { Dialogos.Error(ex.Message, "Migrar microSD"); return; }

            if (!string.IsNullOrWhiteSpace(m.SerialOrigen) && m.SerialOrigen == sd.Serial &&
                !Dialogos.Confirmar("La microSD seleccionada parece ser la ORIGINAL (mismo número de serie).\n\n¿Seguro que quieres borrarla?",
                    "Misma microSD", MessageBoxImage.Warning))
                return;
            if (ConfirmarBorrado != null && !ConfirmarBorrado(sd.Letra, sd.DiscoFisico)) return;

            int disco = sd.DiscoFisico;
            string url = ObtenerUrlFat32?.Invoke() ?? string.Empty;
            await EjecutarMigracionAsync(async (p, ct) =>
            {
                var particionador = new Hardware.ParticionadorDiscos();
                if (m.Tipo == TipoEmuMmc.Particion)
                    await particionador.ParticionarYFormatearAsync(disco, m.GbEmuMmcNecesarios, url, p, ct);
                else
                    await particionador.ParticionarSimpleYFormatearAsync(disco, url,
                        Core.Configuracion.ConfiguracionLocal.EtiquetaSwitchSd, p, ct);

                await Task.Delay(2000, ct);
                string letra = DriveInfo.GetDrives().Select(d => d.Name)
                    .FirstOrDefault(l => { try { return particionador.ObtenerIndiceDiscoFisico(l) == disco; } catch { return false; } })
                    ?? throw new InvalidOperationException("No se encontró la letra de la microSD nueva tras formatearla.");

                await Task.Run(() => EmuMmcMigracionLogic.RestaurarEnDestinoAsync(_carpetaCopia, letra, disco, p, ct), ct);
                if (RefrescarUnidades != null) await RefrescarUnidades();
                Dialogos.Info("Migración completada y verificada.\n\nPrueba la consola con la nueva microSD antes de borrar la copia del PC.",
                    "Migrar microSD");
            });
        }

        private void TarjetaSaves_Click(object sender, MouseButtonEventArgs e) => MostrarPanel(PanelSaves);

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

        private string? _sdCargada;

        private static readonly string[] MensajesEscaneo =
        {
            "Destripando tu emuMMC…",
            "Encontrando juegos HENTAI en tus archivos borrados…",
            "Buscando páginas para adultos en el modo incógnito de tu consola…",
            "Asando la carne en el asador…",
            "Despertando a Link, que lleva 100 años durmiendo…",
            "Pidiéndole a Bowser que devuelva a la princesa…",
            "Contando las horas que le has echado a Animal Crossing…",
            "Soplando el cartucho por si acaso…",
            "Cazando Pokémon shiny entre tus saves…",
            "Convenciendo a Tom Nook de que perdone tu hipoteca…",
            "Buscando la tercera parte de Half-Life…",
            "Revisando si sigues atascado en el Templo del Agua…",
        };

        private void Bloquear(bool bloqueado)
        {
            BloqueoContenido.Visibility = bloqueado ? Visibility.Visible : Visibility.Collapsed;
            ZonaContenido.Visibility    = bloqueado ? Visibility.Collapsed : Visibility.Visible;
            BarraNavegacion.Visibility  = bloqueado ? Visibility.Collapsed : Visibility.Visible;
        }

        private async void BtnEscanear_Click(object sender, RoutedEventArgs e)
        {
            string? letra = ObtenerLetraSD?.Invoke();
            if (string.IsNullOrWhiteSpace(letra))
            {
                TxtSubtitulo.Text = "Selecciona una microSD para escanear su emuMMC.";
                return;
            }

            var rnd = new Random();
            var mensajes = MensajesEscaneo.OrderBy(_ => rnd.Next()).ToArray();
            int idx = 0;
            TxtMensajeEscaneo.Text = mensajes[0];
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2.2) };
            timer.Tick += (_, _) =>
            {
                idx = (idx + 1) % mensajes.Length;
                var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(350));
                TxtMensajeEscaneo.Text = mensajes[idx];
                TxtMensajeEscaneo.BeginAnimation(OpacityProperty, fade);
            };

            OverlayEscaneo.Opacity = 0;
            OverlayEscaneo.Visibility = Visibility.Visible;
            OverlayEscaneo.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250)));
            GiroEscaneo.BeginAnimation(RotateTransform.AngleProperty,
                new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1.4)) { RepeatBehavior = RepeatBehavior.Forever });
            GiroEscaneo2.BeginAnimation(RotateTransform.AngleProperty,
                new DoubleAnimation(360, 0, TimeSpan.FromSeconds(3)) { RepeatBehavior = RepeatBehavior.Forever });
            timer.Start();

            try
            {
                Paso(0, "Leyendo perfiles y almacenamiento…");
                await CargarConsolaAsync();
                Paso(1, "Leyendo juegos y partidas guardadas…");
                await CargarJuegosAsync();
                Paso(2, "Leyendo capturas y vídeos…");
                await CargarAlbumAsync();
                Paso(3, "¡Listo!");
                await Task.Delay(400);
                Bloquear(false);
            }
            catch (Exception ex)
            {
                Services.Logger.Error("[emuMMC] Error en el escaneo", ex);
            }
            finally
            {
                timer.Stop();
                GiroEscaneo.BeginAnimation(RotateTransform.AngleProperty, null);
                GiroEscaneo2.BeginAnimation(RotateTransform.AngleProperty, null);
                OverlayEscaneo.Visibility = Visibility.Collapsed;
            }
        }

        private void Paso(int completados, string texto)
        {
            double p = completados / 3.0;
            TxtPasoEscaneo.Text = texto;
            TxtPorcentajeEscaneo.Text = $"{(int)(p * 100)}%";
            BarraEscaneo.BeginAnimation(WidthProperty, new DoubleAnimation(360 * p, TimeSpan.FromMilliseconds(400)));
        }

        public async Task RefrescarAsync(bool forzar = false)
        {
            string? letra = ObtenerLetraSD?.Invoke();
            string claveSd = $"{letra}|{ObtenerSD?.Invoke()?.Serial}";
            bool cambioSd = forzar || claveSd != _sdCargada;
            _sdCargada = claveSd;
            AplicarModeloConsola(ObtenerSerialConsola?.Invoke());
            LimpiarResumen();
            if (cambioSd)
            {
                LimpiarConsola();
                LimpiarAlbum();
                LimpiarJuegos();
                Bloquear(true);
            }
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
