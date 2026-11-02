using NX_Swite.Core;
using NX_Swite.Hardware;
using NX_Swite.Core.Configuracion;
using NX_Swite.Models;
using NX_Swite.Services;
using NX_Swite.UI;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NX_Swite
{
    /// <summary>
    /// MainWindow — Detección del firmware de la emuMMC (automática en segundo plano
    /// y verificación alterna bajo demanda) mostrada en el panel derecho.
    /// </summary>
    public partial class MainWindow
    {
        // ── Control de la detección asíncrona de firmware de emuMMC ─────────
        // Se cancela y recrea cada vez que cambia la unidad seleccionada, se
        // desconecta la SD o se cierra la ventana, para evitar que un resultado
        // obsoleto (de una unidad ya no seleccionada) actualice la UI.
        private CancellationTokenSource? _ctsFirmwareEmummc;
        private int _idOperacionFirmwareEmummc;

        /// <summary>Cancela cualquier detección de firmware de emuMMC en curso.</summary>
        internal void CancelarDeteccionFirmwareEmummc()
        {
            _ctsFirmwareEmummc?.Cancel();
            _ctsFirmwareEmummc?.Dispose();
            _ctsFirmwareEmummc = null;
        }

        /// <summary>
        /// Traduce un <see cref="EstadoFirmwareEmummc"/> al texto que se muestra
        /// en el campo «FIRMWARE EMUMMC» del panel derecho.
        /// </summary>
        private static string TextoEstadoFirmwareEmummc(ResultadoFirmwareEmummc resultado) => resultado.Estado switch
        {
            EstadoFirmwareEmummc.Detected             => resultado.Version ?? "--",
            EstadoFirmwareEmummc.FirmwareNotDetected   => "emuMMC detectada, pero no fue posible identificar su firmware.",
            EstadoFirmwareEmummc.EmuMmcNotFound         => "No se detectó una emuMMC en esta microSD.",
            EstadoFirmwareEmummc.KeysMissing            => "Sin prod.keys",
            EstadoFirmwareEmummc.KeysInvalid            => "prod.keys inválido",
            EstadoFirmwareEmummc.ToolValidationFailed   => "Herramienta no disponible",
            EstadoFirmwareEmummc.AccessDenied           => "Requiere permisos de administrador",
            EstadoFirmwareEmummc.TimedOut               => "La detección tardó demasiado y fue cancelada.",
            _                                            => "No se pudo leer la NAND",
        };

        /// <summary>
        /// Inicia (sin bloquear) la detección de firmware de la emuMMC RAW para
        /// la unidad representada por <paramref name="info"/>. Cancela cualquier
        /// detección anterior y descarta resultados obsoletos comparando el
        /// identificador incremental de operación.
        /// </summary>
        private void IniciarDeteccionFirmwareEmummcAsync(InfoPanelDerecho info)
        {
            CancelarDeteccionFirmwareEmummc();

            if (!info.HayProdkeys)
            {
                InfoSD.TxtFirmwareEmummc.Text = "Sin prod.keys";
                return;
            }

            InfoSD.TxtFirmwareEmummc.Text = "Detectando firmware de emuMMC...";
            InfoSD.BtnVerificarFirmwareAlterno.Visibility = Visibility.Collapsed;

            _ctsFirmwareEmummc = new CancellationTokenSource();
            var ct = _ctsFirmwareEmummc.Token;
            int idOperacion = ++_idOperacionFirmwareEmummc;

            _ = EjecutarDeteccionFirmwareEmummcAsync(info, idOperacion, ct);
        }

        private async Task EjecutarDeteccionFirmwareEmummcAsync(InfoPanelDerecho info, int idOperacion, System.Threading.CancellationToken ct)
        {
            ResultadoFirmwareEmummc resultado;
            try
            {
                resultado = await _cerebro.ObtenerFirmwareEmummcAsync(info, ct);
            }
            catch (OperationCanceledException)
            {
                // Cancelación por cambio de unidad o cierre de ventana: no es un error, no se toca la UI.
                return;
            }
            catch (Exception ex)
            {
                resultado = ResultadoFirmwareEmummc.De(EstadoFirmwareEmummc.Failed, ex.Message);
            }

            // Si la lectura normal falló, usar la versión verificada antes si la
            // huella de la emuMMC no ha cambiado (lee ~1 MB, menos de un segundo).
            bool desdeRecuerdo = false;
            if (resultado.Estado is EstadoFirmwareEmummc.FirmwareNotDetected
                    or EstadoFirmwareEmummc.ToolValidationFailed
                    or EstadoFirmwareEmummc.TimedOut
                    or EstadoFirmwareEmummc.Failed
                && !string.IsNullOrEmpty(info.RutaProdKeys))
            {
                string rutaKeys = info.RutaProdKeys;
                string? huella = await Task.Run(() => NxNandManagerLogic.CalcularHuellaEmummc(info.DiscoFisico, rutaKeys));
                string? recordada = huella != null ? NxNandManagerLogic.ObtenerFirmwareRecordado(huella) : null;
                if (recordada != null)
                {
                    resultado = ResultadoFirmwareEmummc.Ok(recordada);
                    desdeRecuerdo = true;
                }
            }

            // Descartar resultados obsoletos: la unidad pudo cambiar mientras se detectaba.
            if (idOperacion != _idOperacionFirmwareEmummc) return;
            if (InfoSD.ComboDrives.SelectedItem is not SDInfo unidadActual) return;
            if (unidadActual.DiscoFisico != info.DiscoFisico) return;

            InfoSD.TxtFirmwareEmummc.Text = TextoEstadoFirmwareEmummc(resultado);
            FirmwareEmummcRealDetectado = resultado.Estado == EstadoFirmwareEmummc.Detected
                ? resultado.Version
                : null;
            _infoFirmwareAlterno = info;
            InfoSD.TxtFirmwareEmummc.ToolTip = desdeRecuerdo
                ? "Versión verificada anteriormente. La emuMMC no ha cambiado desde entonces."
                : null;
            InfoSD.BtnVerificarFirmwareAlterno.Visibility =
                resultado.Estado is EstadoFirmwareEmummc.FirmwareNotDetected
                    or EstadoFirmwareEmummc.ToolValidationFailed
                    or EstadoFirmwareEmummc.TimedOut
                    or EstadoFirmwareEmummc.Failed
                    ? Visibility.Visible : Visibility.Collapsed;
        }

        private InfoPanelDerecho? _infoFirmwareAlterno;

        /// <summary>
        /// Verificación alterna del firmware de la emuMMC, solo a petición del usuario,
        /// con pantalla de progreso que bloquea la app mientras se lee la microSD.
        /// </summary>
        private async Task VerificarFirmwareAlternoAsync()
        {
            var info = _infoFirmwareAlterno;
            if (info == null || string.IsNullOrEmpty(info.RutaProdKeys)) return;

            if (!Dialogos.Confirmar(
                    "Se leerá una copia de la emuMMC (unos 2,6 GB) para identificar su firmware.\n\n" +
                    "Tarda alrededor de 1 minuto. No se modifica nada en la microSD, pero no la retires " +
                    "ni la uses mientras dure.\n\n¿Continuar?",
                    "Verificar firmware de forma alterna", MessageBoxImage.Information))
                return;

            CancelarDeteccionFirmwareEmummc();
            _pantallaCarga.MostrarFase("Verificando firmware emuMMC", "Preparando…");
            ResultadoFirmwareEmummc resultado;
            try
            {
                var progreso = new Progress<(int Pct, string Msg)>(p => _pantallaCarga.ActualizarFase(p.Pct, p.Msg));
                resultado = await NxNandManagerLogic.ObtenerFirmwareAlternoAsync(
                    info.DiscoFisico, info.RutaProdKeys, progreso, CancellationToken.None);
            }
            catch (Exception ex)
            {
                resultado = ResultadoFirmwareEmummc.De(EstadoFirmwareEmummc.Failed, ex.Message);
            }
            finally
            {
                _pantallaCarga.Ocultar();
            }

            if (InfoSD.ComboDrives.SelectedItem is not SDInfo actual || actual.DiscoFisico != info.DiscoFisico)
                return;

            InfoSD.TxtFirmwareEmummc.Text = TextoEstadoFirmwareEmummc(resultado);
            if (resultado.Estado == EstadoFirmwareEmummc.Detected)
            {
                FirmwareEmummcRealDetectado = resultado.Version;
                InfoSD.BtnVerificarFirmwareAlterno.Visibility = Visibility.Collapsed;
            }
            else
            {
                Dialogos.Advertencia(
                    "Tampoco fue posible identificar el firmware con la verificación alterna.",
                    "Firmware emuMMC");
            }
        }
    }
}
