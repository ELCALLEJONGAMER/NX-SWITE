using NX_Suite.Core;
using NX_Suite.Hardware;
using NX_Suite.Models;
using NX_Suite.UI;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace NX_Suite
{
    /// <summary>
    /// MainWindow — Panel de Diagnóstico Rápido SD.
    /// Muestra los módulos de configuración con hallazgos activos
    /// y ofrece un botón "Reparar" que ejecuta su pipeline de instalación.
    /// No requiere JSON adicional: reutiliza HallazgosConfig generados
    /// por ValidadorConfiguracion durante la detección normal de la SD.
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>
        /// Actualiza el panel de diagnóstico rápido con el estado actual del catálogo.
        /// Llamar tras cualquier cambio de SD o re-sincronización.
        /// </summary>
        internal void ActualizarDiagnosticoSD()
        {
            string? letraSD = (InfoSD.ComboDrives.SelectedItem as SDInfo)?.Letra;

            if (_catalogoModulos == null || string.IsNullOrEmpty(letraSD))
            {
                MostrarDiagnosticoSinSD();
                return;
            }

            // ?? 1. Módulos de configuración con hallazgos activos ??
            var conProblemas = _catalogoModulos
                .Where(m => m.HallazgosConfig?.Count > 0
                         && m.EstadoSd != EstadoSdModulo.NoInstalado)
                .OrderByDescending(m => m.HallazgosConfig.Any(h => h.EsCritico))
                .ToList();

            // ?? 2. Módulos instalados con dependencias insatisfechas ??
            var conDepsRotas = new List<HallazgoDependencia>();
            foreach (var modulo in _catalogoModulos
                .Where(m => m.EstadoSd != EstadoSdModulo.NoInstalado
                         && m.Dependencias?.Count > 0))
            {
                var deps = AnalizadorDependencias.Analizar(modulo, _catalogoModulos);
                var pendientes = deps.Where(d => d.Estado != EstadoDependencia.OK).ToList();
                if (pendientes.Count > 0)
                    conDepsRotas.Add(new HallazgoDependencia
                    {
                        Modulo               = modulo,
                        DependenciasPendientes = pendientes
                    });
            }

            if (conProblemas.Count == 0 && conDepsRotas.Count == 0)
            {
                MostrarDiagnosticoOK();
                return;
            }

            // ?? Resumen en subtítulo ??
            var partes = new List<string>();
            if (conProblemas.Count > 0)
            {
                int criticos = conProblemas.Count(m => m.HallazgosConfig.Any(h => h.EsCritico));
                partes.Add(criticos > 0
                    ? $"{criticos} configuración(es) crítica(s)"
                    : $"{conProblemas.Count} configuración(es) con avisos");
            }
            if (conDepsRotas.Count > 0)
                partes.Add($"{conDepsRotas.Count} módulo(s) con dependencias rotas");

            TxtDiagSubtitulo.Text        = string.Join(" · ", partes) + ".";
            PanelDiagSinSD.Visibility    = Visibility.Collapsed;
            PanelDiagOK.Visibility       = Visibility.Collapsed;
            ScrollDiag.Visibility        = Visibility.Visible;

            ListaDiagnostico.ItemsSource    = new ObservableCollection<ModuloConfig>(conProblemas);
            ListaDiagDeps.ItemsSource       = new ObservableCollection<HallazgoDependencia>(conDepsRotas);
            SeccionDiagConfig.Visibility    = conProblemas.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            SeccionDiagDeps.Visibility      = conDepsRotas.Count  > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void MostrarDiagnosticoSinSD()
        {
            TxtDiagSubtitulo.Text     = "Conecta una SD para analizar.";
            PanelDiagSinSD.Visibility = Visibility.Visible;
            PanelDiagOK.Visibility    = Visibility.Collapsed;
            ScrollDiag.Visibility     = Visibility.Collapsed;
            ListaDiagnostico.ItemsSource = null;
            ListaDiagDeps.ItemsSource    = null;
        }

        private void MostrarDiagnosticoOK()
        {
            TxtDiagSubtitulo.Text     = "Sin problemas detectados.";
            PanelDiagSinSD.Visibility = Visibility.Collapsed;
            PanelDiagOK.Visibility    = Visibility.Visible;
            ScrollDiag.Visibility     = Visibility.Collapsed;
            ListaDiagnostico.ItemsSource = null;
            ListaDiagDeps.ItemsSource    = null;
        }

        private async void Diagnostico_ClickReparar(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not Button btn || btn.Tag is not ModuloConfig modulo)
                return;

            string? letraSD = (InfoSD.ComboDrives.SelectedItem as SDInfo)?.Letra;
            if (string.IsNullOrEmpty(letraSD))
            {
                Dialogos.Advertencia("No hay ninguna SD seleccionada.");
                return;
            }

            Servicios.Sonidos.Reproducir(EventoSonido.Click);
            await EjecutarInstalacionRapidaAsync(modulo, letraSD);

            // Refrescar el diagnóstico tras reparar
            ActualizarDiagnosticoSD();
        }

        private async void Diagnostico_ClickInstalarDep(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not Button btn || btn.Tag is not ModuloConfig depModulo)
                return;

            string? letraSD = (InfoSD.ComboDrives.SelectedItem as SDInfo)?.Letra;
            if (string.IsNullOrEmpty(letraSD))
            {
                Dialogos.Advertencia("No hay ninguna SD seleccionada.");
                return;
            }

            Servicios.Sonidos.Reproducir(EventoSonido.Click);
            await EjecutarInstalacionRapidaAsync(depModulo, letraSD);

            ActualizarDiagnosticoSD();
        }
    }
}
