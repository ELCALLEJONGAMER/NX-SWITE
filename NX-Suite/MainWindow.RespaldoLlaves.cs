using NX_Swite.Core;
using NX_Swite.Core.Configuracion;
using NX_Swite.Hardware;
using NX_Swite.Models;
using NX_Swite.Services;
using NX_Swite.UI;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NX_Swite
{
    public partial class MainWindow
    {
        private readonly RespaldoLlavesLogic _respaldoLlaves = new();
        private AnalisisRespaldoLlaves?      _analisisLlaves;
        private RespaldoLocal?               _respaldoSeleccionado;

        // ?? Apertura ??????????????????????????????????????????????????????

        private async void AbrirOverlayRespaldoLlaves()
        {
            try
            {
                // Reset completo: cada apertura es una sesion limpia
                ResetearEstadoOverlay();
                MostrarOverlayRespaldoLlaves();

                string? letraSD = (InfoSD.ComboDrives.SelectedItem as SDInfo)?.Letra;

                if (!string.IsNullOrEmpty(letraSD))
                    await RefrescarOverlayRespaldoLlaves(letraSD);
                else
                    MostrarEstadoAnalisisSD("Conecta una Micro SD para analizar.", "#707080");

                RefrescarListaRespaldosPC();
            }
            catch (Exception ex)
            {
                Dialogos.Error($"Error al abrir el panel de llaves:\n{ex.Message}");
            }
        }

        /// <summary>
        /// Limpia todos los controles del overlay antes de mostrar nueva sesion.
        /// Garantiza que cambiar de SD no deje datos residuales visibles.
        /// </summary>
        private void ResetearEstadoOverlay()
        {
            _analisisLlaves       = null;
            _respaldoSeleccionado = null;

            // Panel SD izquierdo
            PanelRespaldoDetalle.Visibility      = Visibility.Collapsed;
            TxtRespaldoEstadoAnalisis.Visibility = Visibility.Collapsed;
            TxtRespaldoEstadoAnalisis.Text       = string.Empty;
            TxtRespaldoFeedback.Visibility       = Visibility.Collapsed;
            TxtRespaldoFeedback.Text             = string.Empty;
            BtnConfirmarRespaldo.IsEnabled       = false;
            TxtRespaldoModelo.Visibility         = Visibility.Collapsed;
            TxtRespaldoModelo.Text               = string.Empty;

            // Panel PC derecho
            TxtRestaurarFeedback.Visibility    = Visibility.Collapsed;
            TxtRestaurarFeedback.Text          = string.Empty;
            BtnRestaurarSeleccionado.IsEnabled = false;
            BtnVerCertificado.Visibility       = Visibility.Collapsed;
            TxtRespaldoSeleccionadoInfo.Text   = "Selecciona un respaldo para restaurarlo a la SD.";
        }

        private void CerrarOverlayRespaldoLlaves()
        {
            var fade = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(180)));
            fade.Completed += (_, _) =>
            {
                PanelRespaldoLlavesOverlay.Visibility = Visibility.Collapsed;
                AplicarBlurFondo(false);
            };
            PanelRespaldoLlavesOverlay.BeginAnimation(UIElement.OpacityProperty, fade);
        }

        // ?? Analisis SD ???????????????????????????????????????????????????

        private async Task RefrescarOverlayRespaldoLlaves(string letraSD)
        {
            MostrarEstadoAnalisisSD("Analizando SD...", "#707080");
            PanelRespaldoDetalle.Visibility = Visibility.Collapsed;
            BtnConfirmarRespaldo.IsEnabled  = false;

            _analisisLlaves = await Task.Run(() => _respaldoLlaves.Analizar(letraSD));
            PoblarResultadoAnalisis(_analisisLlaves);
        }

        private void PoblarResultadoAnalisis(AnalisisRespaldoLlaves analisis)
        {
            if (!string.IsNullOrEmpty(analisis.ErrorAnalisis))
            {
                MostrarEstadoAnalisisSD($"Error: {analisis.ErrorAnalisis}", "#FF5555");
                PanelRespaldoDetalle.Visibility = Visibility.Collapsed;
                BtnConfirmarRespaldo.IsEnabled  = false;
                return;
            }

            if (!analisis.CarpetaAutomaticaExiste || !analisis.TieneArchivos)
            {
                MostrarEstadoAnalisisSD(
                    "No se encontro atmosphere/automatic_backups.\n" +
                    "Inicia Atmosphere al menos una vez para generar los respaldos.",
                    "#FFD54A");
                PanelRespaldoDetalle.Visibility = Visibility.Collapsed;
                BtnConfirmarRespaldo.IsEnabled  = false;
                return;
            }

            MostrarEstadoAnalisisSD(string.Empty, "#707080");
            PanelRespaldoDetalle.Visibility = Visibility.Visible;

            TxtRespaldoSerial.Text = analisis.Serial ?? "No detectado";

            // Modelo y región
            if (!string.IsNullOrEmpty(analisis.Modelo) || !string.IsNullOrEmpty(analisis.Region))
            {
                string modeloTexto = analisis.Modelo ?? string.Empty;
                if (!string.IsNullOrEmpty(analisis.Region))
                    modeloTexto += (modeloTexto.Length > 0 ? "  ·  " : "") + analisis.Region;
                TxtRespaldoModelo.Text       = modeloTexto;
                TxtRespaldoModelo.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                TxtRespaldoModelo.Visibility = System.Windows.Visibility.Collapsed;
            }

            TxtRespaldoBiskeys.Text       = analisis.HayBiskeys  ? "\u2713  OK" : "\u2717  No encontrado";
            TxtRespaldoBiskeys.Foreground = ArchivoColor(analisis.HayBiskeys);

            TxtRespaldoProdinfo.Text       = analisis.HayProdinfo ? "\u2713  OK" : "\u2717  No encontrado";
            TxtRespaldoProdinfo.Foreground = ArchivoColor(analisis.HayProdinfo);

            TxtRespaldoProdkeys.Text       = analisis.HayProdkeys ? "\u2713  OK" : "\u2717  No disponible";
            TxtRespaldoProdkeys.Foreground = ArchivoColor(analisis.HayProdkeys);

            TxtRespaldoDestino.Text = analisis.RutaDestino ?? "\u2014";

            ConfigurarBadgeVerificacion(analisis);

            BtnConfirmarRespaldo.IsEnabled = analisis.TieneArchivos;

            if (analisis.EstadoVerificacion == EstadoVerificacionLlaves.Verificado)
                Logger.RespaldoLlavesVerificado(analisis.Serial ?? "desconocido");
            else if (analisis.EstadoVerificacion == EstadoVerificacionLlaves.Discrepancia)
                Logger.RespaldoLlavesDiscrepancia(analisis.Serial ?? "desconocido");
        }

        private void ConfigurarBadgeVerificacion(AnalisisRespaldoLlaves analisis)
        {
            switch (analisis.EstadoVerificacion)
            {
                case EstadoVerificacionLlaves.Verificado:
                    TxtRespaldoVerificacion.Text        = "\u2713  VERIFICADO  \u2014  bis_key_00 coincide";
                    TxtRespaldoVerificacion.Foreground  = PincelVerde;
                    PanelRespaldoAdvertencia.Visibility = Visibility.Collapsed;
                    break;

                case EstadoVerificacionLlaves.Discrepancia:
                    TxtRespaldoVerificacion.Text        = "\u26a0  DISCREPANCIA  \u2014  consolas distintas";
                    TxtRespaldoVerificacion.Foreground  = PincelRojo;
                    TxtRespaldoMsgAdvertencia.Text      = analisis.DetalleVerificacion ?? string.Empty;
                    PanelRespaldoAdvertencia.Visibility = Visibility.Visible;
                    break;

                case EstadoVerificacionLlaves.SinProdkeys:
                    TxtRespaldoVerificacion.Text        = "\u26a0  Sin prod.keys  \u2014  verificacion parcial";
                    TxtRespaldoVerificacion.Foreground  = PincelAmbar;
                    TxtRespaldoMsgAdvertencia.Text      =
                        "No se encontro switch/prod.keys. Solo se respaldaran BISKEYS y PRODINFO.";
                    PanelRespaldoAdvertencia.Visibility = Visibility.Visible;
                    break;

                case EstadoVerificacionLlaves.SinBiskeys:
                    TxtRespaldoVerificacion.Text        = "\u26a0  Sin BISKEYS.bin";
                    TxtRespaldoVerificacion.Foreground  = PincelAmbar;
                    PanelRespaldoAdvertencia.Visibility = Visibility.Collapsed;
                    break;

                case EstadoVerificacionLlaves.ArchivoInvalido:
                case EstadoVerificacionLlaves.ErrorLectura:
                    TxtRespaldoVerificacion.Text        = $"\u2717  Error: {analisis.DetalleVerificacion}";
                    TxtRespaldoVerificacion.Foreground  = PincelRojo;
                    PanelRespaldoAdvertencia.Visibility = Visibility.Collapsed;
                    break;

                default:
                    TxtRespaldoVerificacion.Text        = "\u2014  Sin datos de verificacion";
                    TxtRespaldoVerificacion.Foreground  = PincelGris;
                    PanelRespaldoAdvertencia.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        // ?? Panel derecho: respaldos PC ???????????????????????????????????

        private void RefrescarListaRespaldosPC()
        {
            var respaldos = RespaldoLlavesLogic.ListarRespaldosLocales();
            ListaRespaldosPC.ItemsSource = respaldos;
            TxtSinRespaldosPC.Visibility = respaldos.Count == 0
                ? Visibility.Visible : Visibility.Collapsed;
            SeleccionarRespaldo(null);
        }

        private void SeleccionarRespaldo(RespaldoLocal? respaldo)
        {
            _respaldoSeleccionado = respaldo;
            ActualizarBordesTarjetas();

            bool haySD  = !string.IsNullOrEmpty((InfoSD.ComboDrives.SelectedItem as SDInfo)?.Letra);
            bool hayRec = respaldo != null;

            BtnRestaurarSeleccionado.IsEnabled = haySD && hayRec;
            BtnVerCertificado.Visibility = (hayRec && respaldo!.HayCertificado)
                ? Visibility.Visible : Visibility.Collapsed;

            TxtRespaldoSeleccionadoInfo.Text = hayRec
                ? $"Seleccionado: {respaldo!.Serial}   \u00b7   {respaldo.FechaFormateada}"
                : "Selecciona un respaldo para restaurarlo a la SD.";
        }

        private void ActualizarBordesTarjetas()
        {
            for (int i = 0; i < ListaRespaldosPC.Items.Count; i++)
            {
                var container = ListaRespaldosPC.ItemContainerGenerator
                    .ContainerFromIndex(i) as System.Windows.Controls.ContentPresenter;
                if (container == null) continue;

                var border = EncontrarBorderNombrado(container, "TarjetaRespaldo");
                if (border == null) continue;

                var item = ListaRespaldosPC.Items[i] as RespaldoLocal;
                bool sel = item != null && item == _respaldoSeleccionado;
                border.BorderBrush = new SolidColorBrush(sel
                    ? (Color)ColorConverter.ConvertFromString("#4CAF50")
                    : (Color)ColorConverter.ConvertFromString("#252535"));
                border.Background = new SolidColorBrush(sel
                    ? (Color)ColorConverter.ConvertFromString("#0A1A0E")
                    : (Color)ColorConverter.ConvertFromString("#0C0C18"));
            }
        }

        private static System.Windows.Controls.Border? EncontrarBorderNombrado(
            System.Windows.DependencyObject padre, string nombre)
        {
            int n = System.Windows.Media.VisualTreeHelper.GetChildrenCount(padre);
            for (int i = 0; i < n; i++)
            {
                var hijo = System.Windows.Media.VisualTreeHelper.GetChild(padre, i);
                if (hijo is System.Windows.Controls.Border b && b.Name == nombre) return b;
                var r = EncontrarBorderNombrado(hijo, nombre);
                if (r != null) return r;
            }
            return null;
        }

        // ?? Handlers ?????????????????????????????????????????????????????

        private void RespaldoLlaves_BackdropClick(object sender, MouseButtonEventArgs e)
            => CerrarOverlayRespaldoLlaves();

        private void BtnCerrarRespaldoLlaves_Click(object sender, RoutedEventArgs e)
            => CerrarOverlayRespaldoLlaves();

        private void TarjetaRespaldo_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.Border border &&
                border.DataContext is RespaldoLocal respaldo)
            {
                SeleccionarRespaldo(respaldo == _respaldoSeleccionado ? null : respaldo);
            }
        }

        private async void BtnConfirmarRespaldo_Click(object sender, RoutedEventArgs e)
        {
            if (_analisisLlaves == null) return;

            BtnConfirmarRespaldo.IsEnabled = false;
            MostrarFeedbackSD("Guardando respaldo...", "#00D2FF");

            var resultado = await _respaldoLlaves.RespaldarAsync(_analisisLlaves);

            if (resultado.Exito)
            {
                try { RespaldoLlavesLogic.GenerarCertificadoTxt(_analisisLlaves); } catch { }
                try { RespaldoLlavesLogic.GenerarCertificadoPng(_analisisLlaves); } catch { }

                MostrarFeedbackSD(
                    $"\u2713  Respaldo completado \u2014 {resultado.ArchivosCopiados.Count} archivo(s) guardados",
                    "#4CAF50");
                TxtRespaldoDestino.Text = resultado.RutaDestino ?? TxtRespaldoDestino.Text;
                RefrescarListaRespaldosPC();
            }
            else if (resultado.Bloqueado)
            {
                MostrarFeedbackSD($"\u26A0  {resultado.MotivoBloqueado}", "#FFD54A");
                BtnConfirmarRespaldo.IsEnabled = true;
            }
            else
            {
                string msg = resultado.Errores.Count > 0
                    ? string.Join("\n", resultado.Errores) : "Error desconocido";
                MostrarFeedbackSD($"\u2717  Error al respaldar: {msg}", "#FF5555");
                BtnConfirmarRespaldo.IsEnabled = true;
            }
        }

        private async void BtnRestaurarSeleccionado_Click(object sender, RoutedEventArgs e)
            => await EjecutarRestauracionAsync(forzar: false);

        private async Task EjecutarRestauracionAsync(bool forzar)
        {
            if (_respaldoSeleccionado == null) return;
            string? letraSD = (InfoSD.ComboDrives.SelectedItem as SDInfo)?.Letra;
            if (string.IsNullOrEmpty(letraSD))
            {
                MostrarFeedbackPC("\u26a0  Sin SD conectada.", "#FFD54A");
                return;
            }

            BtnRestaurarSeleccionado.IsEnabled = false;

            // ?? Paso 1: Si la SD tiene llaves verificadas y sin respaldo previo, respaldarlas
            //            antes de sobreescribir (minimiza la brecha de pérdida de datos).
            if (!forzar && _analisisLlaves != null && _analisisLlaves.TieneArchivos &&
                _analisisLlaves.EsSeguroRespaldar &&
                !_respaldoLlaves.RespaldoEstaAlDia(_analisisLlaves))
            {
                MostrarFeedbackPC("Guardando respaldo preventivo de la SD actual...", "#00D2FF");
                try
                {
                    var resGuard = await _respaldoLlaves.RespaldarAsync(_analisisLlaves);
                    if (resGuard.Exito)
                    {
                        try { RespaldoLlavesLogic.GenerarCertificadoTxt(_analisisLlaves); } catch { }
                        try { RespaldoLlavesLogic.GenerarCertificadoPng(_analisisLlaves); } catch { }
                        RefrescarListaRespaldosPC();
                        MostrarFeedbackPC(
                            $"\u2713  Respaldo preventivo guardado ({resGuard.ArchivosCopiados.Count} archivo(s)).",
                            "#4CAF50");
                        await Task.Delay(1200);
                    }
                }
                catch { /* no bloquear la restauración si el respaldo preventivo falla */ }
            }

            // ?? Paso 2: Restaurar
            MostrarFeedbackPC("Restaurando llaves a la SD...", "#00D2FF");

            var resultado = await _respaldoLlaves.RestaurarDesdeRespaldoLocalAsync(
                _respaldoSeleccionado, letraSD, forzar: forzar);

            if (resultado.DiscrepanciaSerial)
            {
                // La SD tiene llaves de otra consola: pedir confirmación explícita
                BtnRestaurarSeleccionado.IsEnabled = _respaldoSeleccionado != null;
                string serialSD  = resultado.SerialEnSD ?? "desconocido";
                string serialRec = _respaldoSeleccionado.Serial ?? "desconocido";
                bool confirmar = Dialogos.Confirmar(
                    $"??  La SD contiene llaves de otra consola:\n\n" +
                    $"   En la SD  :  {serialSD}\n" +
                    $"   Respaldo  :  {serialRec}\n\n" +
                    "Restaurar sobreescribirá las llaves actuales de la SD.\n" +
                    "Asegúrate de que es lo que deseas antes de continuar.\n\n" +
                    "¿Deseas restaurar de todas formas?",
                    "Consolas distintas — confirmar restauración",
                    System.Windows.MessageBoxImage.Warning);

                if (!confirmar)
                {
                    MostrarFeedbackPC("\u2014  Restauración cancelada por el usuario.", "#707080");
                    return;
                }

                // Usuario confirmó ? rellamar con forzar=true (sin volver a respaldar)
                await EjecutarRestauracionAsync(forzar: true);
                return;
            }

            if (resultado.Omitida)
            {
                MostrarFeedbackPC($"\u26a0  {resultado.MotivoOmision}", "#FFD54A");
            }
            else if (resultado.Exito)
            {
                MostrarFeedbackPC(
                    $"\u2713  Restaurados {resultado.ArchivosRestaurados.Count} archivo(s) en la SD.",
                    "#4CAF50");

                // Refrescar panel derecho sin expulsar/reinsertar la SD
                RefrescarPanelInfoSD();
                // Re-analizar la SD para que el overlay refleje los nuevos archivos
                _analisisLlaves = await Task.Run(() => _respaldoLlaves.Analizar(letraSD));
                PoblarResultadoAnalisis(_analisisLlaves);
                RefrescarListaRespaldosPC();
            }
            else
            {
                string err = resultado.Errores.Count > 0
                    ? string.Join("\n", resultado.Errores) : "Error desconocido";
                MostrarFeedbackPC($"\u2717  Error al restaurar: {err}", "#FF5555");
            }

            BtnRestaurarSeleccionado.IsEnabled = _respaldoSeleccionado != null;
        }

        private void BtnAbrirCarpetaRespaldo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string ruta = _respaldoSeleccionado?.RutaCarpeta
                           ?? _analisisLlaves?.RutaDestino
                           ?? ConfiguracionLocal.RutaRespaldosLlaves;
                if (!Directory.Exists(ruta))
                    ruta = ConfiguracionLocal.RutaRespaldosLlaves;
                if (Directory.Exists(ruta))
                    Process.Start("explorer.exe", ruta);
            }
            catch { }
        }

        private void BtnVerCertificado_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_respaldoSeleccionado == null) return;
                string png = Path.Combine(_respaldoSeleccionado.RutaCarpeta, "certificado.png");
                string txt = Path.Combine(_respaldoSeleccionado.RutaCarpeta, "certificado.txt");
                string abrir = File.Exists(png) ? png : txt;
                if (File.Exists(abrir))
                    Process.Start(new ProcessStartInfo(abrir) { UseShellExecute = true });
            }
            catch { }
        }

        // ?? Animacion de apertura ?????????????????????????????????????????

        private void MostrarOverlayRespaldoLlaves()
        {
            AplicarBlurFondo(true);
            PanelRespaldoLlavesOverlay.Visibility = Visibility.Visible;
            PanelRespaldoLlavesOverlay.Opacity    = 0;

            ContenidoRespaldoLlavesOverlay.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
            ContenidoRespaldoLlavesOverlay.RenderTransform       = new ScaleTransform(0.96, 0.96);

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
            var scX    = new DoubleAnimation(0.96, 1.0, TimeSpan.FromMilliseconds(200))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            var scY    = new DoubleAnimation(0.96, 1.0, TimeSpan.FromMilliseconds(200))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };

            PanelRespaldoLlavesOverlay.BeginAnimation(OpacityProperty, fadeIn);
            ContenidoRespaldoLlavesOverlay.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scX);
            ContenidoRespaldoLlavesOverlay.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scY);
        }

        // ?? Helpers visuales ??????????????????????????????????????????????

        private void MostrarEstadoAnalisisSD(string texto, string colorHex)
        {
            TxtRespaldoEstadoAnalisis.Text       = texto;
            TxtRespaldoEstadoAnalisis.Visibility = string.IsNullOrEmpty(texto)
                ? Visibility.Collapsed : Visibility.Visible;
            TxtRespaldoEstadoAnalisis.Foreground =
                new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
        }

        private void MostrarFeedbackSD(string texto, string colorHex)
        {
            TxtRespaldoFeedback.Text       = texto;
            TxtRespaldoFeedback.Foreground =
                new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
            TxtRespaldoFeedback.Visibility = Visibility.Visible;
            TxtRespaldoFeedback.BringIntoView();
        }

        private void MostrarFeedbackPC(string texto, string colorHex)
        {
            TxtRestaurarFeedback.Text       = texto;
            TxtRestaurarFeedback.Foreground =
                new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
            TxtRestaurarFeedback.Visibility = Visibility.Visible;
            TxtRestaurarFeedback.BringIntoView();
        }

        private static SolidColorBrush ArchivoColor(bool ok) =>
            new SolidColorBrush((Color)ColorConverter.ConvertFromString(ok ? "#4CAF50" : "#505060"));

        private static readonly SolidColorBrush PincelVerde = new((Color)ColorConverter.ConvertFromString("#4CAF50"));
        private static readonly SolidColorBrush PincelRojo  = new((Color)ColorConverter.ConvertFromString("#FF5555"));
        private static readonly SolidColorBrush PincelAmbar = new((Color)ColorConverter.ConvertFromString("#FFD54A"));
        private static readonly SolidColorBrush PincelGris  = new((Color)ColorConverter.ConvertFromString("#707080"));

        // ?? Proteccion automatica en operaciones destructivas ?????????????

        internal async Task PreservarYRestaurarLlaves(
            string letraSD,
            string nombreOperacion,
            Func<Task> operacionDestructiva,
            bool intentarActualizarRespaldoPostOp = false)
        {
            var analisis      = await Task.Run(() => _respaldoLlaves.Analizar(letraSD));
            bool hayLlaves    = analisis.TieneArchivos;
            bool esSeguro     = analisis.EsSeguroRespaldar;
            bool discrepancia = analisis.EstadoVerificacion == EstadoVerificacionLlaves.Discrepancia;

            if (hayLlaves && esSeguro)
            {
                bool alDia = await Task.Run(() => _respaldoLlaves.RespaldoEstaAlDia(analisis));
                if (!alDia)
                {
                    Logger.RespaldoLlavesAutoIniciado(analisis.Serial ?? "desconocido", nombreOperacion);
                    var bk = await _respaldoLlaves.RespaldarAsync(analisis);
                    if (bk.Exito)
                    {
                        try { RespaldoLlavesLogic.GenerarCertificadoTxt(analisis); } catch { }
                        try { RespaldoLlavesLogic.GenerarCertificadoPng(analisis); } catch { }
                    }
                }
            }
            else if (discrepancia)
            {
                Logger.RespaldoLlavesAutoOmitido(
                    analisis.Serial ?? "desconocido",
                    $"Discrepancia antes de '{nombreOperacion}'. Restauracion automatica deshabilitada.");
            }

            await operacionDestructiva();

            if (hayLlaves && esSeguro && !discrepancia && !string.IsNullOrEmpty(analisis.RutaDestino))
            {
                // Solo intentar leer de la SD post-operación si ésta no borra los archivos
                // (formateo/particionado reformatea la SD; limpieza simple los elimina).
                if (intentarActualizarRespaldoPostOp)
                    await _respaldoLlaves.ActualizarRespaldoSiSDTieneMasLlavesAsync(analisis);

                var rest = await _respaldoLlaves.RestaurarAsync(analisis, letraSD, timeoutMs: 20_000);
                if (!rest.Exito && !rest.Omitida && rest.Errores.Count > 0)
                    Logger.RestauracionLlavesFallida(rest.Serial, string.Join("; ", rest.Errores));
            }
        }
    }
}
