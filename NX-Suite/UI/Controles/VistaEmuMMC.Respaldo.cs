using NX_Swite.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NX_Swite.UI.Controles
{
    /// <summary>Respaldo y restauración de la emuMMC (sin formatear la microSD).</summary>
    public partial class VistaEmuMMC
    {
        private const string TituloRespaldo   = "Respaldar emuMMC";
        private const string TituloRestaurar  = "Restaurar emuMMC";

        private EstimacionCopia? _estimacion;

        private static string CarpetaRespaldosPorDefecto =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "NX-Swite Respaldos emuMMC");

        private string _carpetaRespaldos = CarpetaRespaldosPorDefecto;

        // ── Modelos de presentación ──────────────────────────────────────────

        public sealed class Chip
        {
            public string Texto { get; init; } = string.Empty;
            public Brush  Color { get; init; } = Brushes.White;
            public Brush  Fondo => Atenuar(Color);
        }

        public sealed class ItemRespaldo
        {
            public string              Carpeta    { get; init; } = string.Empty;
            public ManifiestoMigracion Manifiesto { get; init; } = new();
            public string Titulo => Manifiesto.Fecha.ToString("dd MMM yyyy · HH:mm");
            public string Tamano => EmuMmcMigracionLogic.FormatearTamano(Manifiesto.BytesTotales);
            public List<Chip> Chips
            {
                get
                {
                    var r = new List<Chip>
                    {
                        string.IsNullOrEmpty(Manifiesto.Firmware)
                            ? new Chip { Texto = "FW DESCONOCIDO", Color = Pincel("#9AA0AA") }
                            : new Chip { Texto = $"FW {Manifiesto.Firmware}", Color = Pincel("#00D2FF") },
                        new Chip
                        {
                            Texto = Manifiesto.Tipo == TipoEmuMmc.Particion ? "PARTICIÓN" : "ARCHIVOS",
                            Color = Pincel("#B388FF"),
                        },
                    };
                    if (Manifiesto.Alcance == AlcanceCopia.SdCompleta)
                        r.Add(new Chip { Texto = "SD COMPLETA", Color = Pincel("#FFB300") });
                    else if (Manifiesto.IncluyeJuegos)
                        r.Add(new Chip { Texto = "CON JUEGOS", Color = Pincel("#00E676") });
                    return r;
                }
            }
        }

        public sealed class Comprobacion
        {
            public string Titulo  { get; init; } = string.Empty;
            public string Detalle { get; init; } = string.Empty;
            public Brush  Color   { get; init; } = Brushes.White;
            public Geometry Icono { get; init; } = Geometry.Empty;
            public Brush  Fondo => Atenuar(Color);
        }

        private enum Nivel { Ok, Aviso, Bloqueo }

        private static Comprobacion Fila(Nivel n, string titulo, string detalle) => new()
        {
            Titulo  = titulo,
            Detalle = detalle,
            Color   = Pincel(n switch { Nivel.Ok => "#00E676", Nivel.Aviso => "#FFB300", _ => "#FF5252" }),
            Icono   = Geometry.Parse(n switch { Nivel.Ok => IconoOk, Nivel.Aviso => IconoAviso, _ => IconoNo }),
        };

        private static SolidColorBrush Pincel(string hex)
        {
            var b = (SolidColorBrush)new BrushConverter().ConvertFrom(hex)!;
            b.Freeze();
            return b;
        }

        private static Brush Atenuar(Brush b)
        {
            if (b is not SolidColorBrush s) return Brushes.Transparent;
            var r = new SolidColorBrush(Color.FromArgb(0x1F, s.Color.R, s.Color.G, s.Color.B));
            r.Freeze();
            return r;
        }

        // ── Navegación ───────────────────────────────────────────────────────

        private async void TarjetaRespaldar_Click(object sender, MouseButtonEventArgs e)
        {
            MostrarPanel(PanelRespaldar);
            CargarListaRespaldos();
            await AnalizarOrigenRespaldoAsync();
        }

        private void TarjetaRestaurar_Click(object sender, MouseButtonEventArgs e)
        {
            MostrarPanel(PanelRestaurar);
            CargarListaRespaldos();
        }

        private void CargarListaRespaldos(string? seleccionar = null)
        {
            var items = EmuMmcMigracionLogic.ListarRespaldos(_carpetaRespaldos)
                .Select(x => new ItemRespaldo { Carpeta = x.Carpeta, Manifiesto = x.Manifiesto })
                .ToList();
            ListaRespaldosResp.ItemsSource = items;
            ListaRespaldosRest.ItemsSource = items;
            TxtSinRespaldosResp.Visibility = TxtSinRespaldosRest.Visibility =
                items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            TxtDestinoRespaldo.Text = _carpetaRespaldos;
            if (seleccionar != null)
                ListaRespaldosRest.SelectedItem = items.FirstOrDefault(i => i.Carpeta == seleccionar);
            else
                LimpiarComprobaciones();
        }

        // ── Respaldar ────────────────────────────────────────────────────────

        private async Task AnalizarOrigenRespaldoAsync()
        {
            _estimacion = null;
            var sd = ObtenerSD?.Invoke();
            TxtRespSd.Text = sd == null ? "—" : sd.Letra.TrimEnd('\\');
            TxtRespTipo.Text = TxtRespFirmware.Text = TxtTamEmu.Text = TxtTamJuegos.Text = "—";
            BtnCrearRespaldo.IsEnabled = false;
            if (sd == null || sd.DiscoFisico < 0)
            {
                TxtMigracion.Text = "Selecciona la microSD con la emuMMC a respaldar.";
                ActualizarTotalRespaldo();
                return;
            }

            TxtMigracion.Text = "Calculando tamaño…";
            try
            {
                _estimacion = await Task.Run(() => EmuMmcMigracionLogic.Estimar(sd.Letra, sd.DiscoFisico));
                TxtRespTipo.Text = _estimacion.Tipo == TipoEmuMmc.Particion ? "Partición" : "Archivos";
                TxtRespFirmware.Text = ObtenerFirmware?.Invoke() ?? "Desconocido";
                TxtTamEmu.Text = EmuMmcMigracionLogic.FormatearTamano(_estimacion.BytesEmuMmc + _estimacion.BytesRaw);
                TxtTamJuegos.Text = _estimacion.BytesJuegos > 0 ? EmuMmcMigracionLogic.FormatearTamano(_estimacion.BytesJuegos) : "Sin juegos";
                ChkRespJuegos.IsEnabled = _estimacion.BytesJuegos > 0;
                if (_estimacion.BytesJuegos == 0) ChkRespJuegos.IsChecked = false;
                BtnCrearRespaldo.IsEnabled = true;
                TxtMigracion.Text = "La microSD solo se lee; no se modifica nada.";
            }
            catch (Exception ex)
            {
                TxtMigracion.Text = ex.Message;
            }
            ActualizarTotalRespaldo();
        }

        private void ChkRespJuegos_Cambiado(object sender, RoutedEventArgs e) => ActualizarTotalRespaldo();

        private void ActualizarTotalRespaldo()
        {
            if (_estimacion == null) { TxtTotalRespaldo.Text = "—"; return; }
            long total = _estimacion.BytesEmuMmc + _estimacion.BytesRaw + (ChkRespJuegos.IsChecked == true ? _estimacion.BytesJuegos : 0);
            TxtTotalRespaldo.Text = EmuMmcMigracionLogic.FormatearTamano(total);
        }

        private void BtnCarpetaRespaldos_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Carpeta donde guardar los respaldos de emuMMC" };
            if (dlg.ShowDialog() != true) return;
            _carpetaRespaldos = dlg.FolderName;
            CargarListaRespaldos();
        }

        private void BtnAbrirCarpetaRespaldos_Click(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(_carpetaRespaldos);
            Process.Start("explorer.exe", _carpetaRespaldos);
        }

        private async void BtnCrearRespaldo_Click(object sender, RoutedEventArgs e)
        {
            var sd = ObtenerSD?.Invoke();
            if (sd == null || sd.DiscoFisico < 0 || _estimacion == null) return;

            bool juegos = ChkRespJuegos.IsChecked == true;
            string? firmware = ObtenerFirmware?.Invoke();
            if (firmware == null &&
                !Dialogos.Confirmar("No se ha podido identificar el firmware de la emuMMC.\n\n" +
                                    "El respaldo quedará marcado como «FW desconocido» y no se podrá avisar de un " +
                                    "posible downgrade al restaurarlo.\n\n¿Continuar?", TituloRespaldo, MessageBoxImage.Warning))
                return;

            string nombre = $"emuMMC_{DateTime.Now:yyyyMMdd_HHmmss}" + (firmware != null ? $"_FW{firmware}" : string.Empty);
            string carpeta = Path.Combine(_carpetaRespaldos, nombre);
            var alcance = juegos ? AlcanceCopia.EmuMmcConJuegos : AlcanceCopia.EmuMmc;

            bool ok = false;
            await EjecutarMigracionAsync(async (p, ct) =>
            {
                try
                {
                    await Task.Run(() => EmuMmcMigracionLogic.CopiarAPcAsync(
                        sd.Letra, sd.DiscoFisico, sd.Serial, carpeta, alcance, firmware, p, ct), ct);
                    ok = true;
                }
                catch
                {
                    // Un respaldo a medias no debe aparecer como válido.
                    try { if (Directory.Exists(carpeta)) Directory.Delete(carpeta, true); } catch { }
                    throw;
                }
            }, TituloRespaldo);

            if (!ok) return;
            TxtCopiaMigracion.Text = carpeta;
            TxtMigracion.Text = "Respaldo completado. Guárdalo hasta comprobar que no lo necesitas.";
            CargarListaRespaldos();
        }

        // ── Restaurar ────────────────────────────────────────────────────────

        private void BtnOtraCarpetaRestaurar_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Carpeta que contiene respaldos de emuMMC" };
            if (dlg.ShowDialog() != true) return;
            // Admite tanto la carpeta de un respaldo como la carpeta que los contiene.
            bool esRespaldo = File.Exists(Path.Combine(dlg.FolderName, EmuMmcMigracionLogic.NombreManifiesto));
            _carpetaRespaldos = esRespaldo ? Path.GetDirectoryName(dlg.FolderName)! : dlg.FolderName;
            CargarListaRespaldos(esRespaldo ? dlg.FolderName : null);
        }

        private void LimpiarComprobaciones()
        {
            ListaComprobaciones.ItemsSource = null;
            TxtSinSeleccion.Visibility = Visibility.Visible;
            BtnRestaurar.IsEnabled = false;
            ChkRestJuegos.IsEnabled = false;
            ChkRestJuegos.IsChecked = false;
            TxtRestTamJuegos.Text = "—";
            TxtRestJuegosDesc.Text = "Este respaldo no incluye juegos.";
        }

        private void ListaRespaldosRest_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListaRespaldosRest.SelectedItem is not ItemRespaldo item) { LimpiarComprobaciones(); return; }
            var m = item.Manifiesto;

            ChkRestJuegos.IsEnabled = m.IncluyeJuegos;
            ChkRestJuegos.IsChecked = m.IncluyeJuegos;
            TxtRestTamJuegos.Text = m.IncluyeJuegos ? EmuMmcMigracionLogic.FormatearTamano(m.BytesJuegos) : "—";
            TxtRestJuegosDesc.Text = m.IncluyeJuegos
                ? "Sobrescribe los juegos de la microSD con los del respaldo."
                : "Este respaldo no incluye juegos; los instalados en la microSD se conservan.";

            var filas = Evaluar(m, out bool bloqueado);
            ListaComprobaciones.ItemsSource = filas;
            TxtSinSeleccion.Visibility = Visibility.Collapsed;
            BtnRestaurar.IsEnabled = !bloqueado;
        }

        /// <summary>Comprueba el respaldo contra la microSD seleccionada antes de escribir nada.</summary>
        private List<Comprobacion> Evaluar(ManifiestoMigracion m, out bool bloqueado)
        {
            var r = new List<Comprobacion>();
            bloqueado = false;
            var sd = ObtenerSD?.Invoke();
            if (sd == null || sd.DiscoFisico < 0)
            {
                bloqueado = true;
                r.Add(Fila(Nivel.Bloqueo, "No hay microSD seleccionada", "Inserta la microSD y selecciónala en el panel derecho."));
                return r;
            }

            // Misma microSD
            if (string.IsNullOrWhiteSpace(m.SerialOrigen) || string.IsNullOrWhiteSpace(sd.Serial))
                r.Add(Fila(Nivel.Aviso, "No se puede confirmar la microSD", "No hay número de serie para compararla con la del respaldo."));
            else if (m.SerialOrigen == sd.Serial)
                r.Add(Fila(Nivel.Ok, "Misma microSD del respaldo", $"{sd.Letra.TrimEnd('\\')} · {sd.CapacidadTotal} GB"));
            else
                r.Add(Fila(Nivel.Aviso, "Es una microSD distinta",
                    "Restaurar en otra tarjeta funciona, pero asegúrate de que la emuMMC pertenece a esta consola."));

            // Firmware
            string? fwActual = ObtenerFirmware?.Invoke();
            int? cmp = EmuMmcMigracionLogic.CompararFirmware(m.Firmware, fwActual);
            if (cmp < 0)
                r.Add(Fila(Nivel.Aviso, $"Firmware anterior: {m.Firmware} → actual {fwActual}",
                    "Volver a un firmware más antiguo puede impedir el arranque si Atmosphère, los sigpatches o los fusibles " +
                    "ya están en una versión superior. Revísalo antes de iniciar la consola."));
            else if (cmp == 0)
                r.Add(Fila(Nivel.Ok, $"Mismo firmware ({m.Firmware})", "Sin riesgo de incompatibilidad por versión."));
            else if (cmp > 0)
                r.Add(Fila(Nivel.Aviso, $"Firmware más reciente: {m.Firmware} (actual {fwActual})",
                    "Comprueba que Atmosphère y los sigpatches de la microSD soportan esa versión."));
            else
                r.Add(Fila(Nivel.Aviso, "Firmware sin comparar",
                    $"Respaldo: {m.Firmware ?? "desconocido"} · microSD: {fwActual ?? "desconocido"}."));

            // Encaje
            if (m.Tipo == TipoEmuMmc.Particion)
            {
                EncajeParticion encaje;
                try { encaje = EmuMmcMigracionLogic.EvaluarEncaje(m, EmuMmcMigracionLogic.LeerParticiones(sd.DiscoFisico)); }
                catch (Exception ex)
                {
                    bloqueado = true;
                    r.Add(Fila(Nivel.Bloqueo, "No se pudo leer la tabla de particiones", ex.Message));
                    return r;
                }
                switch (encaje)
                {
                    case EncajeParticion.Exacta:
                        r.Add(Fila(Nivel.Ok, "Partición emuMMC compatible", "Mismo tamaño. No hace falta formatear."));
                        break;
                    case EncajeParticion.Mayor:
                        r.Add(Fila(Nivel.Aviso, "Partición emuMMC más grande",
                            "Se restaura sin formatear; el espacio sobrante de la partición quedará sin usar."));
                        break;
                    case EncajeParticion.Insuficiente:
                        bloqueado = true;
                        r.Add(Fila(Nivel.Bloqueo, "Partición emuMMC demasiado pequeña",
                            $"Se necesita una de al menos {m.GbEmuMmcNecesarios} GB. Usa «Migrar» para preparar la microSD."));
                        break;
                    default:
                        bloqueado = true;
                        r.Add(Fila(Nivel.Bloqueo, "La microSD no tiene partición emuMMC",
                            "Este respaldo es de tipo partición. Usa «Migrar» para preparar la microSD."));
                        break;
                }
            }
            else
                r.Add(Fila(Nivel.Ok, "emuMMC por archivos", "Se sustituyen los archivos. No hace falta formatear."));

            if (m.Alcance == AlcanceCopia.SdCompleta)
                r.Add(Fila(Nivel.Aviso, "Copia de SD completa",
                    "Es una copia de migración: también se sobrescriben Atmosphère, Hekate y el resto de archivos."));

            return r;
        }

        private async void BtnRestaurar_Click(object sender, RoutedEventArgs e)
        {
            if (ListaRespaldosRest.SelectedItem is not ItemRespaldo item) return;
            var sd = ObtenerSD?.Invoke();
            if (sd == null || sd.DiscoFisico < 0) return;

            // Re-evaluar justo antes (la SD pudo cambiar).
            var filas = Evaluar(item.Manifiesto, out bool bloqueado);
            ListaComprobaciones.ItemsSource = filas;
            if (bloqueado) { BtnRestaurar.IsEnabled = false; return; }

            var avisos = filas.Where(f => f.Color is SolidColorBrush s && s.Color == ((SolidColorBrush)Pincel("#FFB300")).Color)
                              .Select(f => "• " + f.Titulo).ToList();
            string texto =
                $"Se restaurará el respaldo del {item.Titulo} en {sd.Letra.TrimEnd('\\')} ({sd.Etiqueta}, {sd.CapacidadTotal} GB).\n\n" +
                "La emuMMC actual de esa microSD se SOBRESCRIBIRÁ: se perderán las partidas y cambios posteriores al respaldo.\n" +
                (avisos.Count > 0 ? "\nAtención:\n" + string.Join("\n", avisos) + "\n" : string.Empty) +
                "\n¿Continuar?";
            if (!Dialogos.Confirmar(texto, TituloRestaurar, MessageBoxImage.Warning)) return;

            bool juegos = ChkRestJuegos.IsChecked == true;
            int disco = sd.DiscoFisico;
            string letra = sd.Letra;
            bool ok = false;
            await EjecutarMigracionAsync(async (p, ct) =>
            {
                await Task.Run(() => EmuMmcMigracionLogic.RestaurarAsync(item.Carpeta, letra, disco, juegos, p, ct), ct);
                ok = true;
            }, TituloRestaurar);

            if (!ok) return;
            TxtCopiaMigracion.Text = $"Restaurado: {item.Titulo}";
            if (RefrescarUnidades != null) await RefrescarUnidades();
            Dialogos.Info("emuMMC restaurada y verificada.\n\nArranca la consola para comprobar que todo funciona.", TituloRestaurar);
        }
    }
}
