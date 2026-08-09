using NX_Swite.Core;
using NX_Swite.Core;
using NX_Swite.Hardware;
using NX_Swite.Models;
using NX_Swite.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace NX_Swite
{
    /// <summary>
    /// MainWindow — Panel de Diagnóstico Rápido SD.
    /// Tres escaneos sobre el catálogo instalado:
    ///   1. Configuración  — módulos con HallazgosConfig activos.
    ///   2. Dependencias   — módulos instalados con deps no satisfechas.
    ///   3. Compatibilidad — conflictos entre versiones (IncompatibleCon
    ///                       y VersionDependencia de VersionCompatibleSeleccionada).
    /// </summary>
    public partial class MainWindow
    {
        internal void ActualizarDiagnosticoSD()
        {
            string? letraSD = (InfoSD.ComboDrives.SelectedItem as SDInfo)?.Letra;

            if (_catalogoModulos == null || string.IsNullOrEmpty(letraSD))
            {
                MostrarDiagnosticoSinSD();
                return;
            }

            var instalados = _catalogoModulos
                .Where(m => m.EstadoSd != EstadoSdModulo.NoInstalado)
                .ToList();

            // ── 1. Módulos de configuración con hallazgos activos ──
            var conProblemas = instalados
                .Where(m => m.HallazgosConfig?.Count > 0)
                .OrderByDescending(m => m.HallazgosConfig.Any(h => h.EsCritico))
                .ToList();

            // ── 2. Módulos instalados con dependencias insatisfechas ──
            // Se agrupa por CAUSA PRINCIPAL: el módulo que origina el problema
            // (ej. Hekate desactualizado) en lugar de por cada módulo afectado.
            var conDepsRotas = DiagnosticoCompatibilidadLogic.AgruparDependenciasRotas(instalados, _catalogoModulos);

            // ── 3. Incompatibilidades de versión cruzada (+ Fuente D: firmware/Atmos reales) ──
            var todosIncompat = DiagnosticoCompatibilidadLogic.EscanearIncompatibilidades(instalados, FirmwareEmummcRealDetectado, AtmosRealDetectado);
            var conIncompat = todosIncompat.Where(h => h.TipoConflicto != "firmware_real").ToList();
            var conFirmwareRota = todosIncompat.Where(h => h.TipoConflicto == "firmware_real" && h.Origen == "Firmware").ToList();
            var conAtmosRota = todosIncompat.Where(h => h.TipoConflicto == "firmware_real" && h.Origen == "Atmosphere").ToList();

            if (conProblemas.Count == 0 && conDepsRotas.Count == 0 && conIncompat.Count == 0 &&
                conFirmwareRota.Count == 0 && conAtmosRota.Count == 0)
            {
                MostrarDiagnosticoOK();
                return;
            }

            var partes = new List<string>();
            if (conProblemas.Count > 0)
            {
                int criticos = conProblemas.Count(m => m.HallazgosConfig.Any(h => h.EsCritico));
                partes.Add(criticos > 0
                    ? $"{criticos} configuración(es) crítica(s)"
                    : $"{conProblemas.Count} configuración(es) con avisos");
            }
            if (conDepsRotas.Count > 0)
                partes.Add($"{conDepsRotas.Count} dependencia(s) rota(s)");
            if (conFirmwareRota.Count > 0)
                partes.Add($"{conFirmwareRota.Count} incompatibilidad(es) de firmware");
            if (conAtmosRota.Count > 0)
                partes.Add($"{conAtmosRota.Count} incompatibilidad(es) de Atmosphere");
            if (conIncompat.Count > 0)
                partes.Add($"{conIncompat.Count} conflicto(s) de versión");

            TxtDiagSubtitulo.Text = string.Join(" · ", partes) + ".";
            PanelDiagSinSD.Visibility = Visibility.Collapsed;
            PanelDiagOK.Visibility = Visibility.Collapsed;
            ScrollDiag.Visibility = Visibility.Visible;

            ListaDiagnostico.ItemsSource = new ObservableCollection<ModuloConfig>(conProblemas);
            ListaDiagDeps.ItemsSource = new ObservableCollection<HallazgoDependencia>(conDepsRotas);
            ListaDiagIncompat.ItemsSource = new ObservableCollection<HallazgoIncompatibilidad>(conIncompat);
            ListaDiagCompatRota.ItemsSource = new ObservableCollection<HallazgoIncompatibilidad>(conFirmwareRota);
            ListaDiagCompatAtmos.ItemsSource = new ObservableCollection<HallazgoIncompatibilidad>(conAtmosRota);

            SeccionDiagConfig.Visibility = conProblemas.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            SeccionDiagDeps.Visibility = conDepsRotas.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            SeccionDiagCompatRota.Visibility = conFirmwareRota.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            SeccionDiagCompatAtmos.Visibility = conAtmosRota.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            SeccionDiagIncompat.Visibility = conIncompat.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // ── Estados del panel ──────────────────────────────────────────────────

        private void MostrarDiagnosticoSinSD()
        {
            TxtDiagSubtitulo.Text = "Conecta una SD para analizar.";
            PanelDiagSinSD.Visibility = Visibility.Visible;
            PanelDiagOK.Visibility = Visibility.Collapsed;
            ScrollDiag.Visibility = Visibility.Collapsed;
            ListaDiagnostico.ItemsSource = null;
            ListaDiagDeps.ItemsSource = null;
            ListaDiagIncompat.ItemsSource = null;
            ListaDiagCompatRota.ItemsSource = null;
            ListaDiagCompatAtmos.ItemsSource = null;
        }

        private void MostrarDiagnosticoOK()
        {
            TxtDiagSubtitulo.Text = "Sin problemas detectados.";
            PanelDiagSinSD.Visibility = Visibility.Collapsed;
            PanelDiagOK.Visibility = Visibility.Visible;
            ScrollDiag.Visibility = Visibility.Collapsed;
            ListaDiagnostico.ItemsSource = null;
            ListaDiagDeps.ItemsSource = null;
            ListaDiagIncompat.ItemsSource = null;
            ListaDiagCompatRota.ItemsSource = null;
            ListaDiagCompatAtmos.ItemsSource = null;
        }

        // ── Handlers ──────────────────────────────────────────────────────────

        private async void Diagnostico_ClickReparar(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not Button btn || btn.Tag is not ModuloConfig modulo)
                return;
            string? letraSD = (InfoSD.ComboDrives.SelectedItem as SDInfo)?.Letra;
            if (string.IsNullOrEmpty(letraSD)) { Dialogos.Advertencia("No hay ninguna SD seleccionada."); return; }
            Servicios.Sonidos.Reproducir(EventoSonido.Click);
            await EjecutarInstalacionRapidaAsync(modulo, letraSD);
            ActualizarDiagnosticoSD();
        }

        private async void Diagnostico_ClickInstalarDep(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not Button btn || btn.Tag is not ModuloConfig depModulo)
                return;
            string? letraSD = (InfoSD.ComboDrives.SelectedItem as SDInfo)?.Letra;
            if (string.IsNullOrEmpty(letraSD)) { Dialogos.Advertencia("No hay ninguna SD seleccionada."); return; }
            Servicios.Sonidos.Reproducir(EventoSonido.Click);
            await EjecutarInstalacionRapidaAsync(depModulo, letraSD);
            ActualizarDiagnosticoSD();
        }

        private async void Diagnostico_ClickResolverIncompat(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not Button btn || btn.Tag is not HallazgoIncompatibilidad hallazgo)
                return;
            string? letraSD = (InfoSD.ComboDrives.SelectedItem as SDInfo)?.Letra;
            if (string.IsNullOrEmpty(letraSD)) { Dialogos.Advertencia("No hay ninguna SD seleccionada."); return; }
            Servicios.Sonidos.Reproducir(EventoSonido.Click);
            if (hallazgo.EsIncompatibleTotal)
                await EjecutarEliminacionRapidaAsync(hallazgo.ModuloAAccionar, letraSD);
            else
                await EjecutarInstalacionRapidaAsync(hallazgo.ModuloAAccionar, letraSD);
            ActualizarDiagnosticoSD();
        }
    }
}