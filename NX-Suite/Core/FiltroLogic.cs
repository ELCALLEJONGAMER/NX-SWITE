using NX_Swite.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NX_Swite.Core
{
    /// <summary>
    /// Motor de filtrado del cat�logo sin conocimiento de la UI.
    /// Filtra exclusivamente por Etiquetas � no existe campo Mundo ni Categoria.
    /// </summary>
    public static class FiltroLogic
    {
        /// <summary>
        /// Filtra m�dulos cuyas Etiquetas contengan al menos una de las etiquetas del mundo.
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
        /// Filtra m�dulos que contengan una etiqueta espec�fica.
        /// Usado por el panel lateral de categor�as.
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
        /// Filtra m�dulos por texto libre en Nombre o Descripci�n.
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

        /// <summary>
        /// Filtro base del mundo: muestra solo los m�dulos que tengan al menos
        /// una de las etiquetas declaradas en <paramref name="etiquetasBase"/>
        /// (EtiquetasFiltro del mundo). Si <paramref name="etiquetasBase"/> es
        /// nulo o vac�o, se devuelven todos los m�dulos sin filtrar.
        /// </summary>
        public static IEnumerable<ModuloConfig> FiltrarPorEtiquetasMundo(
            IEnumerable<ModuloConfig> modulos,
            IReadOnlyCollection<string>? etiquetasBase)
        {
            if (modulos == null) return Enumerable.Empty<ModuloConfig>();
            if (etiquetasBase == null || etiquetasBase.Count == 0) return modulos;

            return modulos.Where(m =>
                m.Etiquetas != null &&
                m.Etiquetas.Any(t => etiquetasBase.Any(eb =>
                    string.Equals(t, eb, StringComparison.OrdinalIgnoreCase))));
        }

        /// <summary>
        /// Ordena m�dulos por prioridad de <see cref="AccionRapidaModulo"/>:
        /// Actualizar &gt; Reinstalar/Reparar &gt; Eliminar &gt; Instalar &gt; Ninguna.
        /// </summary>
        public static IEnumerable<ModuloConfig> OrdenarPorPrioridadAccion(
            IEnumerable<ModuloConfig> modulos)
        {
            if (modulos == null) return Enumerable.Empty<ModuloConfig>();

            return modulos.OrderBy(m => m.AccionRapida switch
            {
                AccionRapidaModulo.Actualizar  => 0,
                AccionRapidaModulo.Reinstalar  => 1,
                AccionRapidaModulo.Reparar     => 1,
                AccionRapidaModulo.Eliminar    => 2,
                AccionRapidaModulo.Instalar    => 3,
                _                              => 4,
            });
        }
    }
}