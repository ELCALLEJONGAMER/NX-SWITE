using NX_Suite.Core;
using NX_Suite.Hardware;
using NX_Suite.Models;
using NX_Suite.UI;
using System;
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
            var conDepsRotas = new List<HallazgoDependencia>();
            foreach (var modulo in instalados.Where(m => m.Dependencias?.Count > 0))
            {
                var deps = AnalizadorDependencias.Analizar(modulo, _catalogoModulos);
                var pendientes = deps.Where(d => d.Estado != EstadoDependencia.OK).ToList();
                if (pendientes.Count > 0)
                    conDepsRotas.Add(new HallazgoDependencia
                    {
                        Modulo = modulo,
                        DependenciasPendientes = pendientes
                    });
            }

            // ── 3. Incompatibilidades de versión cruzada ──
            var conIncompat = EscanearIncompatibilidades(instalados);

            if (conProblemas.Count == 0 && conDepsRotas.Count == 0 && conIncompat.Count == 0)
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
            if (conIncompat.Count > 0)
                partes.Add($"{conIncompat.Count} conflicto(s) de versión");

            TxtDiagSubtitulo.Text = string.Join(" · ", partes) + ".";
            PanelDiagSinSD.Visibility = Visibility.Collapsed;
            PanelDiagOK.Visibility = Visibility.Collapsed;
            ScrollDiag.Visibility = Visibility.Visible;

            ListaDiagnostico.ItemsSource = new ObservableCollection<ModuloConfig>(conProblemas);
            ListaDiagDeps.ItemsSource = new ObservableCollection<HallazgoDependencia>(conDepsRotas);
            ListaDiagIncompat.ItemsSource = new ObservableCollection<HallazgoIncompatibilidad>(conIncompat);

            SeccionDiagConfig.Visibility = conProblemas.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            SeccionDiagDeps.Visibility = conDepsRotas.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            SeccionDiagIncompat.Visibility = conIncompat.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // ── Escaneo #3 ────────────────────────────────────────────────────────

        private static List<HallazgoIncompatibilidad> EscanearIncompatibilidades(
            List<ModuloConfig> instalados)
        {
            var hallazgos = new List<HallazgoIncompatibilidad>();
            var paresVisto = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var modulo in instalados)
            {
                // Fuente A: IncompatibleCon — coexistencia binaria prohibida
                if (modulo.IncompatibleCon?.Count > 0)
                {
                    foreach (var idConflicto in modulo.IncompatibleCon)
                    {
                        var conflicto = instalados.FirstOrDefault(m =>
                            string.Equals(m.Id, idConflicto, StringComparison.OrdinalIgnoreCase));

                        if (conflicto == null) continue;
                        if (!paresVisto.Add(ClaveParDuplicado(modulo.Id, conflicto.Id))) continue;

                        hallazgos.Add(new HallazgoIncompatibilidad
                        {
                            Modulo = modulo,
                            ModuloConflicto = conflicto,
                            TipoConflicto = "incompatible",
                            VersionInstalada = conflicto.VersionInstalada,
                            VersionRequerida = string.Empty,
                            Mensaje = $"{modulo.Nombre} y {conflicto.Nombre} no pueden " +
                                               "coexistir. Elimina uno de los dos."
                        });
                    }
                }

                // Fuentes B y C usan los constraints de la VERSION INSTALADA (no la recomendada).
                // VersionCompatibleSeleccionada apunta a la version a instalar (0.15.1),
                // pero necesitamos los constraints de la instalada (0.15.0) para detectar conflictos actuales.
                var verInstalada = modulo.Versiones?.FirstOrDefault(v =>
                    string.Equals(v.Version, modulo.VersionInstalada, StringComparison.OrdinalIgnoreCase));
                var verSel = verInstalada ?? modulo.VersionCompatibleSeleccionada;

                // Fuente B: VersionDependencia con soporte de operadores <=, >=, <, >
                if (verSel?.VersionDependencia?.Count > 0)
                {
                    foreach (var (depId, constraintStr) in verSel.VersionDependencia)
                    {
                        var dep = instalados.FirstOrDefault(m =>
                            string.Equals(m.Id, depId, StringComparison.OrdinalIgnoreCase));

                        if (dep == null) continue;
                        if (string.IsNullOrWhiteSpace(dep.VersionInstalada) ||
                            dep.VersionInstalada is "No detectado" or "No instalado")
                            continue;

                        var constraintB = ParseConstraintVersion(constraintStr);
                        if (constraintB == null) continue;

                        var (opB, verReqB) = constraintB.Value;
                        if (!Version.TryParse(NormalizarVersion(dep.VersionInstalada), out var verActualB))
                            continue;

                        if (!ViolaConstraint(verActualB, opB, verReqB)) continue;
                        if (!paresVisto.Add($"verdep|{modulo.Id}|{dep.Id}|{opB}")) continue;

                        var tipoB = opB is "<=" or "<" ? "version_maxima" : "version_minima";
                        hallazgos.Add(new HallazgoIncompatibilidad
                        {
                            Modulo           = modulo,
                            ModuloConflicto  = dep,
                            TipoConflicto    = tipoB,
                            VersionInstalada = dep.VersionInstalada,
                            VersionRequerida = constraintStr,
                            Mensaje          = tipoB == "version_maxima"
                                ? $"{modulo.Nombre} {modulo.VersionInstalada} requiere {dep.Nombre} {opB} {verReqB}, tienes {dep.VersionInstalada}. Actualiza {modulo.Nombre}."
                                : $"{modulo.Nombre} {modulo.VersionInstalada} requiere {dep.Nombre} {opB} {verReqB}, tienes {dep.VersionInstalada}."
                        });
                    }
                }

                // Fuente C: Atmos — constraint de version de Atmosphere (campo dedicado en JSON)
                if (!string.IsNullOrWhiteSpace(verSel?.Atmos))
                {
                    var constraintAtmos = ParseConstraintVersion(verSel.Atmos);
                    if (constraintAtmos != null)
                    {
                        var (opC, verAtmosReq) = constraintAtmos.Value;
                        foreach (var atmosId in new[] { "atmosphere", "atmosphere_mod" })
                        {
                            var atmos = instalados.FirstOrDefault(m =>
                                string.Equals(m.Id, atmosId, StringComparison.OrdinalIgnoreCase));

                            if (atmos == null) continue;
                            if (string.IsNullOrWhiteSpace(atmos.VersionInstalada) ||
                                atmos.VersionInstalada is "No detectado" or "No instalado")
                                continue;

                            if (!Version.TryParse(NormalizarVersion(atmos.VersionInstalada), out var verAtmosActual))
                                continue;

                            if (!ViolaConstraint(verAtmosActual, opC, verAtmosReq)) break;

                            string claveC = $"atmos|{modulo.Id}|{atmos.Id}";
                            if (!paresVisto.Add(claveC)) break;

                            var tipoC = opC is "<=" or "<" ? "version_maxima" : "version_minima";
                            hallazgos.Add(new HallazgoIncompatibilidad
                            {
                                Modulo           = modulo,
                                ModuloConflicto  = atmos,
                                TipoConflicto    = tipoC,
                                VersionInstalada = atmos.VersionInstalada,
                                VersionRequerida = verSel.Atmos,
                                Mensaje          = $"{modulo.Nombre} {modulo.VersionInstalada} requiere Atmosphere {opC} {verAtmosReq}, " +
                                                   $"tienes {atmos.VersionInstalada}. Actualiza {modulo.Nombre}."
                            });
                            break;
                        }
                    }
                }
            }

            return hallazgos;
        }

        /// <summary>
        /// Parsea una expresion de constraint. Soporta prefijos: &lt;=, &gt;=, &lt;, &gt;.
        /// Sin prefijo se trata como &gt;=.
        /// </summary>
        private static (string Operador, Version Version)? ParseConstraintVersion(string expr)
        {
            expr = expr.Trim();
            string op, verStr;

            if      (expr.StartsWith("<=")) { op = "<="; verStr = expr[2..]; }
            else if (expr.StartsWith(">=")) { op = ">="; verStr = expr[2..]; }
            else if (expr.StartsWith("<"))  { op = "<";  verStr = expr[1..]; }
            else if (expr.StartsWith(">"))  { op = ">";  verStr = expr[1..]; }
            else                            { op = ">="; verStr = expr; }

            return Version.TryParse(NormalizarVersion(verStr.Trim()), out var ver)
                ? (op, ver)
                : null;
        }

        /// <summary>Devuelve true si la version instalada viola el constraint.</summary>
        private static bool ViolaConstraint(Version instalada, string operador, Version requerida) =>
            operador switch
            {
                "<=" => instalada > requerida,
                "<"  => instalada >= requerida,
                ">"  => instalada <= requerida,
                _    => instalada < requerida    // >= o sin prefijo
            };

        private static string NormalizarVersion(string v)
        {
            v = v.TrimStart('v', 'V').Trim();
            return v.Count(c => c == '.') == 0 ? v + ".0" : v;
        }

        private static string ClaveParDuplicado(string a, string b) =>
            string.Compare(a, b, StringComparison.OrdinalIgnoreCase) <= 0
                ? $"incompat|{a}|{b}"
                : $"incompat|{b}|{a}";

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
                await EjecutarEliminacionRapidaAsync(hallazgo.ModuloConflicto, letraSD);
            else
                await EjecutarInstalacionRapidaAsync(hallazgo.ModuloAAccionar, letraSD);
            ActualizarDiagnosticoSD();
        }
    }
}