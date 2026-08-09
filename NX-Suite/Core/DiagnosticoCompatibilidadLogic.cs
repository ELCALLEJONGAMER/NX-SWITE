using NX_Swite.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NX_Swite.Core
{
    /// <summary>
    /// Lógica pura (sin WPF) del panel de Diagnóstico Rápido SD: escaneo de
    /// incompatibilidades de versión cruzada (Fuentes A/B/C/D) y agrupación de
    /// dependencias rotas por causa raíz.
    ///
    /// Extraído de MainWindow.Diagnostico.cs (FASE 1 del plan de migración).
    /// No debe depender de System.Windows ni de tipos de colección orientados a
    /// UI (ej. ObservableCollection): recibe y devuelve colecciones neutras.
    /// </summary>
    public static class DiagnosticoCompatibilidadLogic
    {
        /// <summary>
        /// Agrupa las dependencias insatisfechas de los módulos instalados por
        /// CAUSA PRINCIPAL (el módulo que origina el problema, ej. Hekate
        /// desactualizado) en lugar de por cada módulo afectado.
        /// </summary>
        public static List<HallazgoDependencia> AgruparDependenciasRotas(
            IEnumerable<ModuloConfig> instalados,
            IReadOnlyCollection<ModuloConfig> catalogoCompleto)
        {
            var causasRaiz = new Dictionary<string, (ModuloConfig Causante, EstadoDependencia Estado, List<ModuloConfig> Afectados)>(StringComparer.OrdinalIgnoreCase);
            foreach (var modulo in instalados.Where(m => m.Dependencias?.Count > 0))
            {
                var deps = AnalizadorDependencias.Analizar(modulo, catalogoCompleto);
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

            return causasRaiz.Values
                .Select(c => new HallazgoDependencia
                {
                    ModuloCausante = c.Causante,
                    Estado = c.Estado,
                    ModulosAfectados = c.Afectados
                })
                .OrderByDescending(h => h.ModulosAfectados.Count)
                .ToList();
        }

        // ?? Escaneo de incompatibilidades ???????????????????????????????????????

        public static List<HallazgoIncompatibilidad> EscanearIncompatibilidades(
            IReadOnlyCollection<ModuloConfig> instalados,
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
                            VersionRequerida = VersionConstraintLogic.LimpiarVersionMostrar(constraintStr),
                            EtiquetaLimite   = VersionConstraintLogic.EtiquetaLimitePorOperador(opB, dep.Nombre),
                            EtiquetaDetectado = $"{dep.Nombre} detectado",
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
                                VersionRequerida = VersionConstraintLogic.LimpiarVersionMostrar(verSel.Atmos),
                                EtiquetaLimite   = VersionConstraintLogic.EtiquetaLimitePorOperador(opC, "Atmosphere"),
                                EtiquetaDetectado = "Atmosphere detectado",
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
                    string? claveEtiquetaLimiteD = null;

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
                                string etiquetaLimite = VersionConstraintLogic.EtiquetaLimitePorOperador(opFw, "Firmware");
                                mensajeFirmwareD = $"{modulo.Nombre} {modulo.VersionInstalada}";
                                claveEtiquetaLimiteD = etiquetaLimite;
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
                            VersionRequerida     = VersionConstraintLogic.LimpiarVersionMostrar(verInstalada.Firmware),
                            Mensaje              = mensajeFirmwareD! + (hayCompatible
                                ? "\nActualiza el módulo a la última versión disponible."
                                : "\nNo hay ninguna versión disponible compatible: elimínalo."),
                            HayVersionCompatible = hayCompatible,
                            Origen               = "Firmware",
                            EtiquetaLimite       = claveEtiquetaLimiteD ?? "Firmware máximo compatible",
                            EtiquetaDetectado    = "Firmware detectado"
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

                                    string etiquetaLimiteAtmos = VersionConstraintLogic.EtiquetaLimitePorOperador(opD, "Atmosphere");
                                    hallazgos.Add(new HallazgoIncompatibilidad
                                    {
                                        Modulo               = modulo,
                                        ModuloConflicto      = modulo,
                                        TipoConflicto        = "firmware_real",
                                        VersionInstalada     = atmosReal,
                                        VersionRequerida     = VersionConstraintLogic.LimpiarVersionMostrar(verInstalada.Atmos),
                                        Mensaje              = $"{modulo.Nombre} {modulo.VersionInstalada}" + (hayCompatible
                                                                   ? "\nActualiza el módulo a la última versión disponible."
                                                                   : "\nNo hay ninguna versión disponible compatible: elimínalo."),
                                        HayVersionCompatible = hayCompatible,
                                        Origen               = "Atmosphere",
                                        EtiquetaLimite       = etiquetaLimiteAtmos,
                                        EtiquetaDetectado    = "Atmosphere detectado"
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
    }
}
