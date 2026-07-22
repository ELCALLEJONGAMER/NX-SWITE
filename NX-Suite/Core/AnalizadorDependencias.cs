using NX_Swite.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NX_Swite.Core
{
    /// <summary>
    /// L�gica pura (sin UI) que analiza las dependencias declaradas de un m�dulo
    /// cruz�ndolas con el estado real del cat�logo en la SD activa.
    ///
    /// Soporta alternativas OR en el campo Dependencias del JSON.
    /// Formato: "atmosphere or atmosphere_mod"  o  "atmosphere|atmosphere_mod"
    /// Si cualquiera de las alternativas est� instalada, la dependencia se considera OK.
    /// </summary>
    public static class AnalizadorDependencias
    {
        // ?? API p�blica ???????????????????????????????????????????????????????

        /// <summary>
        /// Devuelve el estado de cada dependencia declarada en <paramref name="modulo"/>.
        /// Las entradas con alternativas OR se resuelven a UN �nico resultado:
        /// la primera alternativa instalada (OK) o la primera disponible (para instalar).
        /// </summary>
        public static List<ResultadoDependencia> Analizar(
            ModuloConfig modulo,
            IEnumerable<ModuloConfig> todosLosModulos)
        {
            if (modulo.Dependencias is not { Count: > 0 })
                return new List<ResultadoDependencia>();

            var catalogo = todosLosModulos.ToList();
            var resultado = new List<ResultadoDependencia>();

            foreach (var entradaDep in modulo.Dependencias)
            {
                var (dep, satisfecha) = ResolverEntrada(entradaDep, catalogo);
                if (dep == null) continue;

                var estado = satisfecha ? EstadoDependencia.OK : DeterminarEstado(dep);
                resultado.Add(new ResultadoDependencia
                {
                    Modulo       = dep,
                    Estado       = estado,
                    Seleccionada = estado != EstadoDependencia.OK
                });
            }

            return resultado;
        }

        /// <summary>
        /// Resuelve una entrada de dependencia que puede contener alternativas OR.
        /// <para>
        /// Separadores soportados: <c>" or "</c> (insensible a may�sculas) y <c>"|"</c>.
        /// </para>
        /// <param name="rootId">
        /// ID del m�dulo ra�z que el usuario est� instalando. Si alguna alternativa OR
        /// coincide con �l se considera satisfecha: ese m�dulo ser� instalado de todas formas.
        /// </param>
        /// <returns>
        /// El m�dulo resuelto y <c>true</c> si la dependencia ya est� satisfecha
        /// (alguna alternativa est� instalada y actualizada). Si no existe ninguna
        /// alternativa en el cat�logo devuelve <c>(null, false)</c>.
        /// </returns>
        /// </summary>
        public static (ModuloConfig? Modulo, bool Satisfecha) ResolverEntrada(
            string entradaDep,
            IEnumerable<ModuloConfig> catalogo,
            string? rootId = null)
        {
            var ids   = ParsearAlternativas(entradaDep);
            var lista = catalogo.ToList();

            // 0. Si el m�dulo ra�z que se va a instalar es una de las alternativas OR,
            //    la dependencia queda satisfecha de antemano: no hace falta instalar
            //    ninguna de las otras alternativas.
            if (rootId != null)
            {
                foreach (var id in ids)
                {
                    if (!string.Equals(id, rootId, StringComparison.OrdinalIgnoreCase)) continue;
                    var raiz = lista.FirstOrDefault(x =>
                        string.Equals(x.Id, rootId, StringComparison.OrdinalIgnoreCase));
                    if (raiz != null) return (raiz, true);
                }
            }

            // 1. �Alguna alternativa instalada y completamente actualizada?
            foreach (var id in ids)
            {
                var m = lista.FirstOrDefault(x =>
                    string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
                if (m != null && DeterminarEstado(m) == EstadoDependencia.OK)
                    return (m, true);
            }

            // 1b. Para dependencias OR: si ninguna alternativa est� perfecta,
            //     una alternativa instalada (aunque desactualizada) S� satisface la dep.
            //     Esto evita que "atmosphere_mod or atmosphere" pida atmosphere_mod
            //     cuando atmosphere ya est� instalado con update pendiente.
            if (ids.Count > 1)
            {
                foreach (var id in ids)
                {
                    var m = lista.FirstOrDefault(x =>
                        string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
                    if (m != null && m.EstadoSd != EstadoSdModulo.NoInstalado)
                        return (m, true);
                }
            }

            // 2. Ninguna instalada ? devolver la primera que exista para instalar
            foreach (var id in ids)
            {
                var m = lista.FirstOrDefault(x =>
                    string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
                if (m != null)
                    return (m, false);
            }

            return (null, false); // ninguna alternativa encontrada en el cat�logo
        }

        /// <summary>
        /// Devuelve true si el m�dulo tiene dependencias que requieren acci�n
        /// (al menos una no satisfecha).
        /// </summary>
        public static bool TieneDependenciasPendientes(
            ModuloConfig modulo,
            IEnumerable<ModuloConfig> todosLosModulos)
            => Analizar(modulo, todosLosModulos).Any(d => d.Estado != EstadoDependencia.OK);

        // ?? Helpers internos ?????????????????????????????????????????????????

        /// <summary>
        /// Igual que <see cref="Analizar"/> pero resuelve dependencias de forma
        /// recursiva (transitiva): si hekate requiere payload, payload aparece en
        /// la lista antes que hekate.
        ///
        /// El orden resultante es topol�gico (DFS post-order): las deps de m�s
        /// profundidad se devuelven primero, listas para instalarse antes que sus
        /// dependientes. Solo se incluyen las que necesitan acci�n (no OK).
        /// Los ciclos y duplicados se ignoran con un conjunto de visitados.
        /// </summary>
        public static List<ResultadoDependencia> AnalizarTransitivo(
            ModuloConfig modulo,
            IEnumerable<ModuloConfig> todosLosModulos)
        {
            var catalogo  = todosLosModulos.ToList();
            var visitados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var resultado = new List<ResultadoDependencia>();

            // Marcar el m�dulo ra�z como visitado para evitar que aparezca
            // como dependencia de s� mismo si existe un ciclo (A?B?A).
            visitados.Add(modulo.Id);

            ResolverRecursivo(modulo, catalogo, visitados, resultado, modulo.Id);

            return resultado;
        }

        /// <summary>
        /// DFS post-order sobre el grafo de dependencias.
        /// A�ade al resultado solo las deps que necesitan acci�n, en orden de
        /// instalaci�n correcto (las m�s profundas primero).
        /// </summary>
        private static void ResolverRecursivo(
            ModuloConfig               modulo,
            List<ModuloConfig>         catalogo,
            HashSet<string>            visitados,
            List<ResultadoDependencia> resultado,
            string                     rootId)
        {
            if (modulo.Dependencias is not { Count: > 0 }) return;

            foreach (var entradaDep in modulo.Dependencias)
            {
                var (dep, satisfecha) = ResolverEntrada(entradaDep, catalogo, rootId);
                if (dep == null) continue;

                // Evitar ciclos y duplicados
                if (!visitados.Add(dep.Id)) continue;

                // Primero las deps de la dep (DFS en profundidad)
                ResolverRecursivo(dep, catalogo, visitados, resultado, rootId);

                // Luego la dep misma, solo si necesita acci�n
                if (!satisfecha)
                {
                    resultado.Add(new ResultadoDependencia
                    {
                        Modulo       = dep,
                        Estado       = DeterminarEstado(dep),
                        Seleccionada = true
                    });
                }
            }
        }

        /// <summary>
        /// Parsea una entrada de dependencia y devuelve la lista de IDs alternativos.
        /// Soporta " or " (cualquier capitalizaci�n) y "|" como separadores.
        /// </summary>
        public static List<string> ParsearAlternativas(string entradaDep)
        {
            // Normalizar "|" ? " or " y luego dividir por " or " insensible a may�sculas
            var normalizada = entradaDep.Replace("|", " or ");

            // Split manual case-insensitive sin Regex
            var resultado = new List<string>();
            const string sep = " or ";
            int inicio = 0;

            while (inicio < normalizada.Length)
            {
                int idx = normalizada.IndexOf(sep, inicio, StringComparison.OrdinalIgnoreCase);
                if (idx < 0)
                {
                    var resto = normalizada[inicio..].Trim();
                    if (resto.Length > 0) resultado.Add(resto);
                    break;
                }
                var parte = normalizada[inicio..idx].Trim();
                if (parte.Length > 0) resultado.Add(parte);
                inicio = idx + sep.Length;
            }

            return resultado;
        }

        private static EstadoDependencia DeterminarEstado(ModuloConfig dep) =>
            dep.EstadoSd switch
            {
                EstadoSdModulo.NoInstalado           => EstadoDependencia.NoInstalada,
                EstadoSdModulo.ParcialmenteInstalado => EstadoDependencia.Parcial,
                EstadoSdModulo.Instalado when dep.RequiereUpdate => EstadoDependencia.Desactualizada,
                _                                    => EstadoDependencia.OK
            };
    }
}
