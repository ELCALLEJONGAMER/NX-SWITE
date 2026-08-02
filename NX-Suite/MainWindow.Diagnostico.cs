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
            var causasRaiz = new Dictionary<string, (ModuloConfig Causante, EstadoDependencia Estado, List<ModuloConfig> Afectados)>(StringComparer.OrdinalIgnoreCase);
            foreach (var modulo in instalados.Where(m => m.Dependencias?.Count > 0))
            {
                var deps = AnalizadorDependencias.Analizar(modulo, _catalogoModulos);
                foreach (var dep in deps.Where(d => d.Estado != EstadoDependencia.OK))
                {
                    if (causasRaiz.TryGetValue(dep.Modulo.Id, out var existente))
                    {
                        existente.Afectados.Add(modulo);
                    }
                    else
                    {
                        causasRaiz[dep.Modulo.Id] = (dep.Modulo, dep.Estado, new List<ModuloConfig> { modulo });
                    }
                }
            }

            var conDepsRotas = causasRaiz.Values
                .Select(c => new HallazgoDependencia
                {
                    ModuloCausante = c.Causante,
                    Estado = c.Estado,
                    ModulosAfectados = c.Afectados
                })
                .OrderByDescending(h => h.ModulosAfectados.Count)
                .ToList();

            // ── 3. Incompatibilidades de versión cruzada (+ Fuente D: firmware/Atmos reales) ──
            var todosIncompat = EscanearIncompatibilidades(instalados, FirmwareEmummcRealDetectado, AtmosRealDetectado);
            var conIncompat = todosIncompat.Where(h => h.TipoConflicto != "firmware_real").ToList();
            var conCompatRota = todosIncompat.Where(h => h.TipoConflicto == "firmware_real").ToList();

            if (conProblemas.Count == 0 && conDepsRotas.Count == 0 && conIncompat.Count == 0 && conCompatRota.Count == 0)
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
            if (conCompatRota.Count > 0)
                partes.Add($"{conCompatRota.Count} compatibilidad(es) rota(s)");
            if (conIncompat.Count > 0)
                partes.Add($"{conIncompat.Count} conflicto(s) de versión");

            TxtDiagSubtitulo.Text = string.Join(" · ", partes) + ".";
            PanelDiagSinSD.Visibility = Visibility.Collapsed;
            PanelDiagOK.Visibility = Visibility.Collapsed;
            ScrollDiag.Visibility = Visibility.Visible;

            ListaDiagnostico.ItemsSource = new ObservableCollection<ModuloConfig>(conProblemas);
            ListaDiagDeps.ItemsSource = new ObservableCollection<HallazgoDependencia>(conDepsRotas);
            ListaDiagIncompat.ItemsSource = new ObservableCollection<HallazgoIncompatibilidad>(conIncompat);
            ListaDiagCompatRota.ItemsSource = new ObservableCollection<HallazgoIncompatibilidad>(conCompatRota);

            SeccionDiagConfig.Visibility = conProblemas.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            SeccionDiagDeps.Visibility = conDepsRotas.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            SeccionDiagCompatRota.Visibility = conCompatRota.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            SeccionDiagIncompat.Visibility = conIncompat.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // ── Escaneo #3 ────────────────────────────────────────────────────────

        private static List<HallazgoIncompatibilidad> EscanearIncompatibilidades(
            List<ModuloConfig> instalados,
            string? firmwareEmummcReal,
            string? atmosReal)
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

                        var constraintB = VersionConstraintLogic.ParseConstraintVersion(constraintStr);
                        if (constraintB == null) continue;

                        var (opB, verReqB) = constraintB.Value;
                        if (!Version.TryParse(VersionConstraintLogic.NormalizarVersion(dep.VersionInstalada), out var verActualB))
                            continue;

                        if (!VersionConstraintLogic.ViolaConstraint(verActualB, opB, verReqB)) continue;
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
                    var constraintAtmos = VersionConstraintLogic.ParseConstraintVersion(verSel.Atmos);
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

                            if (!Version.TryParse(VersionConstraintLogic.NormalizarVersion(atmos.VersionInstalada), out var verAtmosActual))
                                continue;

                            if (!VersionConstraintLogic.ViolaConstraint(verAtmosActual, opC, verAtmosReq)) break;

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

                // Fuente D: firmware/Atmos REALES del sistema (emuMMC + Atmosphere instalado)
                // vs. los constraints declarados por la version instalada del propio modulo.
                // A diferencia de B/C (que comparan entre modulos del catalogo), aqui la
                // fuente de verdad es el hardware real. Si la version instalada es
                // incompatible, se ofrece ACTUALIZAR (si Versiones[0] resuelve el problema)
                // o ELIMINAR (si ni la ultima version disponible es compatible).
                if (verInstalada != null)
                {
                    string? claveFirmwareD = null;
                    string? mensajeFirmwareD = null;

                    if (!string.IsNullOrWhiteSpace(verInstalada.Firmware) &&
                        !string.IsNullOrWhiteSpace(firmwareEmummcReal))
                    {
                        var constraintFw = VersionConstraintLogic.ParseConstraintVersion(verInstalada.Firmware);
                        if (constraintFw != null &&
                            Version.TryParse(VersionConstraintLogic.NormalizarVersion(firmwareEmummcReal), out var verFwReal))
                        {
                            var (opFw, verFwReq) = constraintFw.Value;
                            if (VersionConstraintLogic.ViolaConstraint(verFwReal, opFw, verFwReq))
                            {
                                claveFirmwareD = $"fwreal|{modulo.Id}";
                                string etiquetaLimite = opFw switch
                                {
                                    "<=" or "<" => "Firmware máximo compatible",
                                    ">=" or ">" => "Firmware mínimo requerido",
                                    _           => "Firmware requerido"
                                };
                                mensajeFirmwareD = $"{modulo.Nombre} {modulo.VersionInstalada}\n" +
                                                   $"{etiquetaLimite}: {verFwReq}\n" +
                                                   $"Firmware detectado: {firmwareEmummcReal}";
                            }
                        }
                    }

                    if (claveFirmwareD != null && paresVisto.Add(claveFirmwareD))
                    {
                        var ultimaVersion = modulo.Versiones?.FirstOrDefault();
                        bool hayCompatible = false;
                        if (ultimaVersion != null &&
                            !string.IsNullOrWhiteSpace(ultimaVersion.Firmware) &&
                            Version.TryParse(VersionConstraintLogic.NormalizarVersion(firmwareEmummcReal!), out var verFwReal2))
                        {
                            var constraintUltima = VersionConstraintLogic.ParseConstraintVersion(ultimaVersion.Firmware);
                            if (constraintUltima != null)
                            {
                                var (opU, verU) = constraintUltima.Value;
                                hayCompatible = !VersionConstraintLogic.ViolaConstraint(verFwReal2, opU, verU);
                            }
                        }

                        hallazgos.Add(new HallazgoIncompatibilidad
                        {
                            Modulo               = modulo,
                            ModuloConflicto      = modulo,
                            TipoConflicto        = "firmware_real",
                            VersionInstalada     = firmwareEmummcReal!,
                            VersionRequerida     = verInstalada.Firmware,
                            Mensaje              = mensajeFirmwareD! + (hayCompatible
                                ? "\nActualiza el módulo a la última versión disponible."
                                : "\nNo hay ninguna versión disponible compatible: elimínalo."),
                            HayVersionCompatible = hayCompatible
                        });
                    }
                    else if (!string.IsNullOrWhiteSpace(verInstalada.Atmos) &&
                             !string.IsNullOrWhiteSpace(atmosReal))
                    {
                        var constraintAtmosD = VersionConstraintLogic.ParseConstraintVersion(verInstalada.Atmos);
                        if (constraintAtmosD != null &&
                            Version.TryParse(VersionConstraintLogic.NormalizarVersion(atmosReal), out var verAtmosRealD))
                        {
                            var (opD, verAtmosReqD) = constraintAtmosD.Value;
                            if (VersionConstraintLogic.ViolaConstraint(verAtmosRealD, opD, verAtmosReqD))
                            {
                                string claveAtmosD = $"atmosreal|{modulo.Id}";
                                if (paresVisto.Add(claveAtmosD))
                                {
                                    var ultimaVersion = modulo.Versiones?.FirstOrDefault();
                                    bool hayCompatible = false;
                                    if (ultimaVersion != null && !string.IsNullOrWhiteSpace(ultimaVersion.Atmos))
                                    {
                                        var constraintUltima = VersionConstraintLogic.ParseConstraintVersion(ultimaVersion.Atmos);
                                        if (constraintUltima != null)
                                        {
                                            var (opU, verU) = constraintUltima.Value;
                                            hayCompatible = !VersionConstraintLogic.ViolaConstraint(verAtmosRealD, opU, verU);
                                        }
                                    }

                                    hallazgos.Add(new HallazgoIncompatibilidad
                                    {
                                        Modulo               = modulo,
                                        ModuloConflicto      = modulo,
                                        TipoConflicto        = "firmware_real",
                                        VersionInstalada     = atmosReal,
                                        VersionRequerida     = verInstalada.Atmos,
                                        Mensaje              = $"{modulo.Nombre} {modulo.VersionInstalada}\n" +
                                                               $"{(opD is "<=" or "<" ? "Atmosphere máximo compatible" : opD is ">=" or ">" ? "Atmosphere mínimo requerido" : "Atmosphere requerido")}: {verAtmosReqD}\n" +
                                                               $"Atmosphere detectado: {atmosReal}" + (hayCompatible
                                                                   ? "\nActualiza el módulo a la última versión disponible."
                                                                   : "\nNo hay ninguna versión disponible compatible: elimínalo."),
                                        HayVersionCompatible = hayCompatible
                                    });
                                }
                            }
                        }
                    }
                }
            }

            return hallazgos;
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
            ListaDiagCompatRota.ItemsSource = null;
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