using NX_Suite.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NX_Suite.Core
{
    /// <summary>
    /// Lógica pura (sin UI) que analiza las dependencias declaradas de un módulo
    /// cruzándolas con el estado real del catálogo en la SD activa.
    /// </summary>
    public static class AnalizadorDependencias
    {
        /// <summary>
        /// Devuelve el estado de cada dependencia declarada en <paramref name="modulo"/>.
        /// La lista incluye TODAS las dependencias (también las OK) para que la UI
        /// pueda mostrar el panorama completo.
        /// </summary>
        public static List<ResultadoDependencia> Analizar(
            ModuloConfig modulo,
            IEnumerable<ModuloConfig> todosLosModulos)
        {
            if (modulo.Dependencias is not { Count: > 0 })
                return new List<ResultadoDependencia>();

            var catalogo = todosLosModulos.ToList();
            var resultado = new List<ResultadoDependencia>();

            foreach (var idDep in modulo.Dependencias)
            {
                var dep = catalogo.FirstOrDefault(m =>
                    string.Equals(m.Id, idDep, StringComparison.OrdinalIgnoreCase));

                if (dep == null) continue;

                var estado = DeterminarEstado(dep);

                resultado.Add(new ResultadoDependencia
                {
                    Modulo       = dep,
                    Estado       = estado,
                    // Preseleccionamos las que requieren acción
                    Seleccionada = estado != EstadoDependencia.OK
                });
            }

            return resultado;
        }

        /// <summary>
        /// Devuelve true si el módulo tiene dependencias que requieren acción
        /// (al menos una que no está en estado OK).
        /// </summary>
        public static bool TieneDependenciasPendientes(
            ModuloConfig modulo,
            IEnumerable<ModuloConfig> todosLosModulos)
        {
            var deps = Analizar(modulo, todosLosModulos);
            return deps.Any(d => d.Estado != EstadoDependencia.OK);
        }

        // ?? Helpers ??????????????????????????????????????????????????????????

        private static EstadoDependencia DeterminarEstado(ModuloConfig dep) =>
            dep.EstadoSd switch
            {
                EstadoSdModulo.NoInstalado           => EstadoDependencia.NoInstalada,
                EstadoSdModulo.ParcialmenteInstalado => EstadoDependencia.Parcial,
                EstadoSdModulo.Instalado when dep.RequiereUpdate => EstadoDependencia.Desactualizada,
                _ => EstadoDependencia.OK
            };
    }
}
