using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NX_Suite.Hardware;
using NX_Suite.Models;

namespace NX_Suite.Core
{
    public class SuiteControllerFacade : ISuiteController
    {
        private readonly SuiteController _inner;

        public SuiteControllerFacade(SuiteController inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public Task<GistData> SincronizarTodoAsync(string urlGist, string letraSD)
        {
            return _inner.SincronizarTodoAsync(urlGist, letraSD);
        }

        public Task<GistData> SincronizarTodoAsync(string urlGist, string letraSD, CancellationToken cancellationToken)
        {
            return _inner.SincronizarTodoAsync(urlGist, letraSD, cancellationToken);
        }

        public Task<List<SDInfo>> ObtenerUnidadesRemoviblesAsync()
        {
            return _inner.ObtenerUnidadesRemoviblesAsync();
        }

        public InfoPanelDerecho ObtenerInfoPanel(SDInfo unidad, List<ModuloConfig> modulos)
        {
            return _inner.ObtenerInfoPanel(unidad, modulos);
        }

        public Task<(bool Exito, string MensajeError)> InstalarModuloAsync(ModuloConfig modulo, string letraSD, IProgress<EstadoProgreso> progreso)
        {
            return _inner.InstalarModuloAsync(modulo, letraSD, progreso);
        }

        public Task<bool> DesinstalarModuloAsync(ModuloConfig modulo, string letraSD)
        {
            return _inner.DesinstalarModuloAsync(modulo, letraSD);
        }

        public void LimpiarCacheModulo(ModuloConfig modulo)
        {
            _inner.LimpiarCacheModulo(modulo);
        }

        public void ActualizarEstadoCacheCatalogo(IEnumerable<ModuloConfig> catalogo)
        {
            _inner.ActualizarEstadoCacheCatalogo(catalogo);
        }

        public IEnumerable<ModuloConfig> FiltrarPorMundo(IEnumerable<ModuloConfig> modulos, string mundoId)
        {
            return _inner.FiltrarPorMundo(modulos, mundoId);
        }

        public IEnumerable<ModuloConfig> FiltrarPorEtiqueta(IEnumerable<ModuloConfig> modulos, string etiqueta)
        {
            return _inner.FiltrarPorEtiqueta(modulos, etiqueta);
        }
    }
}