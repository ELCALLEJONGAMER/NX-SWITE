using NX_Suite.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NX_Suite.Core
{
    /// <summary>
    /// Motor de filtrado del catálogo sin conocimiento de la UI.
    /// Filtra exclusivamente por Etiquetas — no existe campo Mundo ni Categoria.
    /// </summary>
    public static class FiltroLogic
    {
        /// <summary>
        /// Filtra módulos cuyas Etiquetas contengan al menos una de las etiquetas del mundo.
        /// Reemplaza el antiguo FiltrarPorMundo que usaba m.Mundo.
        /// </summary>
        public static IEnumerable<ModuloConfig> FiltrarPorEtiquetas(
            IEnumerable<ModuloConfig> modulos,
            IEnumerable<string> etiquetas)
        {
            if (modulos == null) return Enumerable.Empty<ModuloConfig>();

            var lista = etiquetas?.ToList();
            if (lista == null || lista.Count == 0) return modulos;

            return modulos.Where(m =>
                m.Etiquetas != null &&
                m.Etiquetas.Any(t => lista.Any(e =>
                    string.Equals(t, e, StringComparison.OrdinalIgnoreCase))));
        }

        /// <summary>
        /// Filtra módulos que contengan una etiqueta específica.
        /// Usado por el panel lateral de categorías.
        /// </summary>
        public static IEnumerable<ModuloConfig> FiltrarPorEtiqueta(
            IEnumerable<ModuloConfig> modulos,
            string etiqueta)
        {
            if (modulos == null) return Enumerable.Empty<ModuloConfig>();

            if (string.IsNullOrWhiteSpace(etiqueta) ||
                string.Equals(etiqueta, "Todos", StringComparison.OrdinalIgnoreCase))
                return modulos;

            return modulos.Where(m =>
                m.Etiquetas != null &&
                m.Etiquetas.Contains(etiqueta, StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Filtra módulos por texto libre en Nombre o Descripción.
        /// </summary>
        public static IEnumerable<ModuloConfig> FiltrarPorTexto(
            IEnumerable<ModuloConfig> modulos,
            string busqueda)
        {
            if (modulos == null) return Enumerable.Empty<ModuloConfig>();
            if (string.IsNullOrWhiteSpace(busqueda)) return modulos;

            var termino = busqueda.ToLowerInvariant();
            return modulos.Where(m =>
                (m.Nombre?.ToLowerInvariant().Contains(termino) ?? false) ||
                (m.Descripcion?.ToLowerInvariant().Contains(termino) ?? false));
        }
    }
}