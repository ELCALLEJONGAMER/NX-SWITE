using System;
using System.Collections.Generic;
using System.Linq;
using NX_Suite.Models;

namespace NX_Suite.Core
{
    /// <summary>
    /// Motor de filtrado del catálogo sin conocimiento de la UI
    /// </summary>
    public static class FiltroLogic
    {
        public static IEnumerable<ModuloConfig> FiltrarPorMundo(IEnumerable<ModuloConfig> modulos, string mundoId)
        {
            if (string.IsNullOrWhiteSpace(mundoId)) return modulos;

            return modulos.Where(m => 
                !string.IsNullOrEmpty(m.Mundo) && 
                m.Mundo.Equals(mundoId, StringComparison.OrdinalIgnoreCase));
        }

        public static IEnumerable<ModuloConfig> FiltrarPorEtiqueta(IEnumerable<ModuloConfig> modulos, string etiqueta)
        {
            if (string.IsNullOrWhiteSpace(etiqueta) || etiqueta.Equals("Todos", StringComparison.OrdinalIgnoreCase))
                return modulos;

            return modulos.Where(m => 
                m.Etiquetas != null && 
                m.Etiquetas.Contains(etiqueta, StringComparer.OrdinalIgnoreCase));
        }

        public static IEnumerable<ModuloConfig> FiltrarPorTexto(IEnumerable<ModuloConfig> modulos, string busqueda)
        {
            if (string.IsNullOrWhiteSpace(busqueda)) return modulos;

            var termino = busqueda.ToLowerInvariant();
            return modulos.Where(m => 
                (m.Nombre?.ToLowerInvariant().Contains(termino) ?? false) ||
                (m.Descripcion?.ToLowerInvariant().Contains(termino) ?? false));
        }
    }
}